using AVAUIDesigner.Engine.Document;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Scriban.Runtime;

namespace AVAUIDesigner.Engine.CodeGeneration;

/// <summary>
/// Generates (or merges into) the .axaml.cs partial class of a designed form. Hand-written code
/// inside event handlers, extra constructor statements and other user members are preserved.
/// </summary>
public class CodeBehindGenerator
{
    private const string DefaultHandlerBody = "        // TODO: implement";

    private static readonly HashSet<string> TemplateElementTypes =
        ["DataTemplate", "ControlTemplate", "ItemsPanelTemplate", "TreeDataTemplate", "Template", "Style", "Styles"];

    /// <summary>
    /// Generates or merges code-behind for <paramref name="document"/>.
    /// <paramref name="existingSource"/> is the current .axaml.cs contents, or null for a new file.
    /// </summary>
    public string Generate(DesignDocument document, string? existingSource, CodeGenOptions options)
    {
        var model = BuildModel(document, options);
        if (!string.IsNullOrWhiteSpace(existingSource)) MergeExisting(model, existingSource, options);
        return TemplateRenderer.Render("CodeBehind.sbntxt", model.ToScriptObject(options));
    }

    /// <summary>Named elements that get fields (excluding the root and anything inside templates).</summary>
    public static IEnumerable<DesignElement> GetFieldElements(DesignDocument document) =>
        document.AllElements.Where(e => e.Name is not null && !ReferenceEquals(e, document.RootElement) && !IsInsideTemplate(e))
            .GroupBy(e => e.Name).Select(g => g.First());

    /// <summary>The event-args type of a wired event (from the descriptor), with its namespace.</summary>
    public static (string Type, string Namespace) GetEventArgs(DesignElement element, string eventName)
    {
        var ev = element.Descriptor?.FindEvent(eventName);
        return ev is null ? ("RoutedEventArgs", "Avalonia.Interactivity") : (ev.EventArgsTypeName, ev.EventArgsNamespace);
    }

    /// <summary>
    /// True when <see cref="CodeGenOptions.WireEventsInCode"/> wires this handler in WireEvents(): named,
    /// non-root elements outside templates with a valid handler name. The serializer omits exactly these
    /// event attributes so every handler is wired once.
    /// </summary>
    public static bool IsWiredInCode(DesignElement element, DesignEventHandler handler) =>
        element.Name is not null && element.Parent is not null && !IsInsideTemplate(element)
        && CodeIdentifiers.IsValidIdentifier(handler.HandlerName);

    private static bool IsInsideTemplate(DesignElement e)
    {
        for (var p = e.Parent; p is not null; p = p.Parent)
            if (TemplateElementTypes.Contains(CodeIdentifiers.BareTypeName(p.ElementType))) return true;
        return e.OwnerProperty?.Name is "ItemTemplate" or "Template" or "ContentTemplate" or "DataTemplates";
    }

    private static GenerationModel BuildModel(DesignDocument document, CodeGenOptions options)
    {
        var model = new GenerationModel
        {
            Namespace = options.Namespace,
            ClassName = options.ClassName,
            BaseClass = options.BaseClass,
        };
        model.Usings.UnionWith(["Avalonia", "Avalonia.Controls", "Avalonia.Interactivity"]);
        if (options.GenerateFieldDeclarations) model.Usings.Add("Avalonia.Markup.Xaml");

        var fieldElements = GetFieldElements(document).ToList();
        if (options.GenerateFieldDeclarations)
        {
            foreach (var e in fieldElements)
                model.Fields.Add((FieldType(e, document), e.Name!));
        }

        // Handlers inside templates still need a stub (the XAML compiler resolves them on the class);
        // only wiring in code is limited to the root namescope, which IsWiredInCode checks.
        foreach (var element in document.AllElements)
        {
            foreach (var handler in element.EventHandlers)
            {
                if (!CodeIdentifiers.IsValidIdentifier(handler.HandlerName)) continue;
                var (argsType, argsNs) = GetEventArgs(element, handler.EventName);
                model.Usings.Add(argsNs);
                if (model.Handlers.All(h => h.Name != handler.HandlerName))
                    model.Handlers.Add(new HandlerModel(handler.HandlerName, argsType));
                // Only named elements are wired in code; the root and unnamed elements keep their XAML attribute
                // (the serializer omits attributes exactly for the elements wired here).
                if (options.WireEventsInCode && IsWiredInCode(element, handler) && !ReferenceEquals(element, document.RootElement))
                    model.Wirings.Add((element.Name!, handler.EventName, handler.HandlerName));
            }
        }
        return model;
    }

    /// <summary>
    /// The C# type of a field. Avalonia.Controls types use their short name (imported); others are fully
    /// qualified, e.g. Avalonia.Controls.Shapes.Path, which would otherwise clash with System.IO.Path.
    /// </summary>
    private static string FieldType(DesignElement e, DesignDocument document)
    {
        var bare = CodeIdentifiers.BareTypeName(e.ElementType);
        if (e.Descriptor is { } d)
        {
            var dot = d.FullTypeName.LastIndexOf('.');
            var ns = dot > 0 ? d.FullTypeName[..dot] : string.Empty;
            return ns == "Avalonia.Controls" ? bare : d.FullTypeName;
        }
        // Prefixed custom controls: resolve the prefix's clr-namespace.
        var colon = e.ElementType.IndexOf(':');
        if (colon > 0 && document.GetNamespaceUri(e.ElementType[..colon]) is { } uri
                      && uri.StartsWith("clr-namespace:", StringComparison.Ordinal))
            return $"{uri["clr-namespace:".Length..].Split(';')[0]}.{bare}";
        return "Control";
    }

    private static void MergeExisting(GenerationModel model, string source, CodeGenOptions options)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();

        foreach (var u in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            if (u.Name is not null && u.Alias is null && u.StaticKeyword == default) model.Usings.Add(u.Name.ToString());

        var cls = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                      .FirstOrDefault(c => c.Identifier.Text == options.ClassName)
                  ?? root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (cls is null) return;

        var fieldNames = model.Fields.Select(f => f.Name).ToHashSet();
        var designerRegions = DesignerRegions(cls);
        var handlerNames = model.Handlers.Select(h => h.Name).ToHashSet();

        foreach (var member in cls.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field when field.Declaration.Variables.All(v => fieldNames.Contains(v.Identifier.Text))
                                                     || designerRegions.Any(r => r.Contains(field.SpanStart)):
                    // Designer-owned; regenerated.
                    continue;
                case ConstructorDeclarationSyntax ctor when ctor.ParameterList.Parameters.Count == 0 && !ctor.Modifiers.Any(SyntaxKind.StaticKeyword):
                    model.ConstructorBody = ExtractConstructorExtras(ctor);
                    continue;
                case MethodDeclarationSyntax m when m.Identifier.Text == "WireEvents" && m.ParameterList.Parameters.Count == 0:
                    continue;
                case MethodDeclarationSyntax m when m.Identifier.Text == "InitializeComponent" && options.GenerateFieldDeclarations
                                                                                                 && m.ParameterList.Parameters.Count == 0:
                    continue;
                case MethodDeclarationSyntax m when handlerNames.Contains(m.Identifier.Text) && LooksLikeHandler(m):
                {
                    var h = model.Handlers.First(x => x.Name == m.Identifier.Text);
                    h.Body = ExtractBody(m) ?? DefaultHandlerBody;
                    // Keep the user's parameter type if it differs (e.g. a more specific EventArgs).
                    h.Args = m.ParameterList.Parameters[1].Type?.ToString() ?? h.Args;
                    continue;
                }
                case MethodDeclarationSyntax m when LooksLikeHandler(m):
                    model.Orphans.Add(MemberText(member));
                    continue;
                default:
                    model.UserMembers.Add(MemberText(member));
                    continue;
            }
        }
    }

    /// <summary>Spans enclosed by "#region Designer-generated ..." / "#endregion".</summary>
    private static List<Microsoft.CodeAnalysis.Text.TextSpan> DesignerRegions(ClassDeclarationSyntax cls)
    {
        var result = new List<Microsoft.CodeAnalysis.Text.TextSpan>();
        var stack = new Stack<(int Start, bool Designer)>();
        foreach (var trivia in cls.DescendantTrivia())
        {
            if (trivia.IsKind(SyntaxKind.RegionDirectiveTrivia))
                stack.Push((trivia.SpanStart, trivia.ToString().Contains("Designer-generated", StringComparison.Ordinal)));
            else if (trivia.IsKind(SyntaxKind.EndRegionDirectiveTrivia) && stack.TryPop(out var open) && open.Designer)
                result.Add(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(open.Start, trivia.Span.End));
        }
        return result;
    }

    private static bool LooksLikeHandler(MethodDeclarationSyntax m)
    {
        if (m.ParameterList.Parameters.Count != 2) return false;
        var first = m.ParameterList.Parameters[0].Type?.ToString().TrimEnd('?');
        var second = m.ParameterList.Parameters[1].Type?.ToString() ?? string.Empty;
        return first is "object" or "Object" or "System.Object" && second.EndsWith("EventArgs", StringComparison.Ordinal)
               && m.ReturnType.ToString() == "void";
    }

    private static string? ExtractBody(MethodDeclarationSyntax m)
    {
        if (m.Body is { } body)
        {
            var text = body.SyntaxTree.GetText().ToString(
                Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(body.OpenBraceToken.Span.End, body.CloseBraceToken.SpanStart));
            var indented = CodeIdentifiers.Reindent(text, "        ");
            return indented.Length == 0 ? null : indented;
        }
        if (m.ExpressionBody is { } expr)
            return "        " + expr.Expression.ToString() + ";";
        return null;
    }

    private static string ExtractConstructorExtras(ConstructorDeclarationSyntax ctor)
    {
        if (ctor.Body is null) return string.Empty;
        var kept = ctor.Body.Statements.Where(s =>
            s is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax inv } ||
            inv.Expression.ToString() is not ("InitializeComponent" or "WireEvents" or "this.InitializeComponent"));
        var text = string.Join("\n", kept.Select(s => s.ToFullString().TrimEnd()));
        return CodeIdentifiers.Reindent(text, "        ");
    }

    /// <summary>Member text with #region trivia removed, re-indented to class-member level.</summary>
    private static string MemberText(MemberDeclarationSyntax member)
    {
        var leading = member.GetLeadingTrivia()
            .Where(t => !t.IsKind(SyntaxKind.RegionDirectiveTrivia) && !t.IsKind(SyntaxKind.EndRegionDirectiveTrivia));
        var cleaned = member.WithLeadingTrivia(leading);
        // Region directives can also hide in the trailing trivia of the last token.
        var text = cleaned.ToFullString();
        var lines = text.Replace("\r\n", "\n").Split('\n')
            .Where(l => !l.TrimStart().StartsWith("#region", StringComparison.Ordinal)
                        && !l.TrimStart().StartsWith("#endregion", StringComparison.Ordinal)
                        && !l.TrimStart().StartsWith("// These handlers are no longer wired", StringComparison.Ordinal)
                        && !l.TrimStart().StartsWith("// delete them once they are no longer needed", StringComparison.Ordinal));
        return CodeIdentifiers.Reindent(string.Join("\n", lines), "    ");
    }

    private sealed class HandlerModel(string name, string args)
    {
        public string Name { get; } = name;
        public string Args { get; set; } = args;
        public string Body { get; set; } = DefaultHandlerBody;
    }

    private sealed class GenerationModel
    {
        public string Namespace { get; init; } = string.Empty;
        public string ClassName { get; init; } = string.Empty;
        public string BaseClass { get; init; } = string.Empty;
        public SortedSet<string> Usings { get; } = new(StringComparer.Ordinal);
        public List<(string Type, string Name)> Fields { get; } = [];
        public List<(string Field, string Event, string Handler)> Wirings { get; } = [];
        public List<HandlerModel> Handlers { get; } = [];
        public List<string> Orphans { get; } = [];
        public List<string> UserMembers { get; } = [];
        public string ConstructorBody { get; set; } = string.Empty;

        public ScriptObject ToScriptObject(CodeGenOptions options)
        {
            var so = new ScriptObject
            {
                ["usings"] = OrderUsings(Usings),
                ["namespace"] = Namespace,
                ["class_name"] = ClassName,
                ["base_class"] = BaseClass,
                ["nullable"] = options.GenerateNullableAnnotations,
                ["generate_fields"] = options.GenerateFieldDeclarations && Fields.Count > 0,
                ["fields"] = Fields.Select(f => new ScriptObject { ["type"] = f.Type, ["name"] = f.Name }).ToList(),
                ["wire_events"] = options.WireEventsInCode,
                ["wirings"] = Wirings.Select(w => new ScriptObject { ["field"] = w.Field, ["event"] = w.Event, ["handler"] = w.Handler }).ToList(),
                ["handlers"] = Handlers.Select(h => new ScriptObject { ["name"] = h.Name, ["args"] = h.Args, ["body"] = h.Body }).ToList(),
                ["orphans"] = Orphans,
                ["user_members"] = UserMembers,
                ["ctor_body"] = ConstructorBody,
            };
            return so;
        }

        /// <summary>System usings first, then the rest alphabetically.</summary>
        private static List<string> OrderUsings(IEnumerable<string> usings) =>
            usings.OrderBy(u => u == "System" || u.StartsWith("System.", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(u => u, StringComparer.Ordinal).ToList();
    }
}
