using System.Text.RegularExpressions;
using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Engine.CodeGeneration;

/// <summary>
/// Default MVVM bindings for named controls (applied when MVVM mode is active):
/// TextBox.Text → {Binding {Name}Text, Mode=TwoWay}, Button.Command → {Binding {Name}Command}, etc.
/// </summary>
public static partial class MvvmBindingHeuristics
{
    /// <summary>(element type, property, path format, extra binding arguments)</summary>
    private static readonly (string Type, string Property, string Format, string Extra)[] Rules =
    [
        ("TextBox", "Text", "{0}Text", ", Mode=TwoWay"),
        ("Button", "Command", "{0}Command", ""),
        ("CheckBox", "IsChecked", "Is{0}Checked", ""),
        ("ComboBox", "SelectedItem", "Selected{0}Item", ""),
        ("Slider", "Value", "{0}Value", ""),
        ("ProgressBar", "Value", "{0}Value", ""),
    ];

    /// <summary>The binding the heuristics would assign to <paramref name="element"/>, if any.</summary>
    public static (string Property, string Binding)? GetDefaultBinding(DesignElement element)
    {
        if (element.Name is not { } name) return null;
        var type = CodeIdentifiers.BareTypeName(element.ElementType);
        foreach (var rule in Rules)
        {
            if (rule.Type != type) continue;
            var path = string.Format(rule.Format, CodeIdentifiers.ToPascalCase(name));
            return (rule.Property, $"{{Binding {path}{rule.Extra}}}");
        }
        return null;
    }

    /// <summary>Property values to set on every named element lacking a binding (for a CompositeCommand).</summary>
    public static IEnumerable<(DesignElement Element, string Property, string Binding)> Suggest(DesignDocument document)
    {
        foreach (var e in document.AllElements)
        {
            if (GetDefaultBinding(e) is not { } b) continue;
            var current = e.GetValue(b.Property);
            if (current is not null && Properties.XamlValueConverter.IsMarkupExtension(current)) continue;
            yield return (e, b.Property, b.Binding);
        }
    }

    /// <summary>Extracts the path of a simple binding: "{Binding Foo, Mode=TwoWay}" → "Foo".</summary>
    public static string? GetBindingPath(string? value)
    {
        if (value is null) return null;
        var m = BindingRegex().Match(value);
        if (!m.Success) return null;
        var path = m.Groups["path"].Success ? m.Groups["path"].Value : m.Groups["named"].Value;
        return CodeIdentifiers.IsValidIdentifier(path) ? path : null;
    }

    [GeneratedRegex(@"^\{\s*(?:Binding|CompiledBinding|ReflectionBinding)(?:\s+(?:Path=)?(?<path>[A-Za-z_][A-Za-z0-9_]*)\s*(?:,|\})|.*?\bPath=(?<named>[A-Za-z_][A-Za-z0-9_]*))")]
    private static partial Regex BindingRegex();
}
