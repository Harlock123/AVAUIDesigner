using Avalonia.Media;
using AVAUIDesigner.Engine.Document;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Scriban.Runtime;

namespace AVAUIDesigner.Engine.CodeGeneration;

/// <summary>
/// Generates a CommunityToolkit.Mvvm ViewModel skeleton from the bindings found in a document:
/// one [ObservableProperty] per bound property path and one [RelayCommand] per bound command.
/// </summary>
public class ViewModelGenerator
{
    public record BoundProperty(string Path, string Type, string Initializer, string Source);
    public record BoundCommand(string Path, string Method, string Source);

    public string GetViewModelClassName(CodeGenOptions options) => $"{options.ClassName}ViewModel";

    /// <summary>Collects bound properties/commands in tree order (first occurrence wins).</summary>
    public (List<BoundProperty> Properties, List<BoundCommand> Commands) Analyze(DesignDocument document)
    {
        var props = new List<BoundProperty>();
        var commands = new List<BoundCommand>();
        foreach (var element in document.AllElements)
        {
            foreach (var p in element.Properties.Where(p => p.IsAttributeForm))
            {
                var path = MvvmBindingHeuristics.GetBindingPath(p.StringValue);
                if (path is null) continue;
                var source = $"{element.DisplayName}.{p.Name}";
                if (p.Name == "Command")
                {
                    var method = path.EndsWith("Command", StringComparison.Ordinal) && path.Length > "Command".Length
                        ? path[..^"Command".Length]
                        : path;
                    if (commands.All(c => c.Path != path)) commands.Add(new BoundCommand(path, method, source));
                    continue;
                }
                if (props.Any(x => x.Path == path)) continue;
                var type = element.ResolvePropertyType(p.Name);
                var (typeName, init) = CSharpType(type, p.Name);
                props.Add(new BoundProperty(path, typeName, init, source));
            }
        }
        return (props, commands);
    }

    /// <summary>Generates the ViewModel; when <paramref name="existingSource"/> is given, only missing members are added.</summary>
    public string Generate(DesignDocument document, CodeGenOptions options, string? existingSource = null)
    {
        var (props, commands) = Analyze(document);
        var className = GetViewModelClassName(options);

        if (!string.IsNullOrWhiteSpace(existingSource))
            return Merge(existingSource, className, props, commands);

        var so = new ScriptObject
        {
            ["namespace"] = options.EffectiveViewModelNamespace,
            ["class_name"] = className,
            ["properties"] = props.Select(p => new ScriptObject
            {
                ["type"] = p.Type,
                ["field"] = FieldName(p.Path),
                ["initializer"] = p.Initializer,
                ["comment"] = $"Bound to {p.Source}",
            }).ToList(),
            ["commands"] = commands.Select(c => new ScriptObject
            {
                ["method"] = c.Method,
                ["comment"] = $"Bound to {c.Source}",
            }).ToList(),
        };
        return TemplateRenderer.Render("ViewModel.sbntxt", so);
    }

    /// <summary>"TxtName" → "_txtName" (CommunityToolkit generates the TxtName property).</summary>
    public static string FieldName(string path) => "_" + CodeIdentifiers.ToCamelCase(path);

    private static string Merge(string source, string className, List<BoundProperty> props, List<BoundCommand> commands)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var cls = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.Text == className)
                  ?? root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (cls is null) return source;

        var existing = new HashSet<string>(cls.Members.SelectMany(MemberNames));
        var additions = new List<string>();
        foreach (var p in props)
        {
            var field = FieldName(p.Path);
            if (existing.Contains(field) || existing.Contains(p.Path)) continue;
            var init = p.Initializer.Length > 0 ? $" = {p.Initializer}" : string.Empty;
            additions.Add($"    [ObservableProperty]\n    private {p.Type} {field}{init};");
        }
        foreach (var c in commands)
        {
            if (existing.Contains(c.Method) || existing.Contains(c.Path)) continue;
            additions.Add($"    [RelayCommand]\n    private void {c.Method}()\n    {{\n        // TODO: implement\n    }}");
        }
        if (additions.Count == 0) return source;

        var insertAt = cls.CloseBraceToken.SpanStart;
        // Insert before the closing brace, at the start of its line.
        var lineStart = source.LastIndexOf('\n', Math.Max(0, insertAt - 1)) + 1;
        if (source[lineStart..insertAt].Trim().Length > 0) lineStart = insertAt;
        var block = "\n" + string.Join("\n\n", additions) + "\n";
        return source[..lineStart].TrimEnd('\n', '\r', ' ') + "\n" + block + source[lineStart..];
    }

    private static IEnumerable<string> MemberNames(MemberDeclarationSyntax m) => m switch
    {
        FieldDeclarationSyntax f => f.Declaration.Variables.Select(v => v.Identifier.Text),
        PropertyDeclarationSyntax p => [p.Identifier.Text],
        MethodDeclarationSyntax me => [me.Identifier.Text],
        _ => [],
    };

    private static (string Type, string Initializer) CSharpType(Type? type, string propertyName)
    {
        if (propertyName is "ItemsSource") return ("ObservableCollection<object>", "new()");
        if (type is null) return ("object?", "");
        var underlying = Nullable.GetUnderlyingType(type);
        var isNullable = underlying is not null;
        var t = underlying ?? type;
        var q = isNullable ? "?" : "";
        if (t == typeof(string)) return ("string", "string.Empty");
        if (t == typeof(bool)) return ("bool" + q, "");
        if (t == typeof(double)) return ("double" + q, "");
        if (t == typeof(int)) return ("int" + q, "");
        if (t == typeof(decimal)) return ("decimal" + q, "");
        if (t == typeof(DateTimeOffset)) return ("DateTimeOffset" + q, "");
        if (t == typeof(DateTime)) return ("DateTime" + q, "");
        if (t == typeof(TimeSpan)) return ("TimeSpan" + q, "");
        if (typeof(IBrush).IsAssignableFrom(t)) return ("Avalonia.Media.IBrush?", "");
        if (t.IsEnum) return (t.FullName!.Replace('+', '.'), "");
        return ("object?", "");
    }
}
