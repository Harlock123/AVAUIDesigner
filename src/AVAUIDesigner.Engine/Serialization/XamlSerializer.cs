using System.Text;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Properties;

namespace AVAUIDesigner.Engine.Serialization;

/// <summary>Writes a <see cref="DesignDocument"/> as well-formatted .axaml text.</summary>
public class XamlSerializer
{
    private const string Indent = "    ";
    private const int MaxSingleLineLength = 110;

    private static readonly string[] LayoutOrder =
    [
        "Width", "Height", "MinWidth", "MinHeight", "MaxWidth", "MaxHeight",
        "Margin", "Padding", "HorizontalAlignment", "VerticalAlignment",
    ];

    private static readonly HashSet<string> ContentNames = ["Content", "Text", "Header", "Title"];

    /// <summary>
    /// When false, event attributes of named (non-root) elements are not written because the code-behind
    /// wires them in WireEvents() (see <c>CodeGenOptions.WireEventsInCode</c>); otherwise handlers would run twice.
    /// </summary>
    public bool EmitEventAttributesForNamedElements { get; set; } = true;

    /// <summary>Serializes the full document.</summary>
    public string Serialize(DesignDocument document) => SerializeWithMap(document, out _);

    /// <summary>Serializes the document and reports each element's character span in the output.</summary>
    public string SerializeWithMap(DesignDocument document, out Dictionary<DesignElement, (int Start, int Length)> spans)
    {
        var writer = new Writer(document, EmitEventAttributesForNamedElements);
        writer.WriteElement(document.RootElement, 0, isRoot: true);
        spans = writer.Spans;
        return writer.ToString();
    }

    /// <summary>Serializes a single element subtree without namespace declarations (used for the clipboard).</summary>
    public string SerializeElement(DesignElement element)
    {
        var writer = new Writer(null, EmitEventAttributesForNamedElements);
        writer.WriteElement(element, 0, isRoot: false);
        return writer.ToString().TrimEnd();
    }

    /// <summary>Serializes the element values of a property element (e.g. an ItemTemplate) as a fragment.</summary>
    public string SerializePropertyElementContent(DesignProperty property)
    {
        var writer = new Writer(null, EmitEventAttributesForNamedElements);
        foreach (var e in property.ElementValues) writer.WriteElement(e, 0, isRoot: false);
        if (property.ElementText is { } text) writer.AppendRaw(EscapeText(text) + "\n");
        return writer.ToString().TrimEnd();
    }

    /// <summary>The attributes of an element in output order, after default-value omission.</summary>
    public static List<KeyValuePair<string, string>> GetOrderedAttributes(DesignElement element, DesignDocument? document, bool isRoot,
        bool emitNamedEvents = true)
    {
        var result = new List<KeyValuePair<string, string>>();
        var d = element.Descriptor;

        if (isRoot && document is not null)
        {
            foreach (var ns in document.Namespaces.OrderBy(NamespaceRank))
                result.Add(new(ns.AttributeName, ns.Uri));
            if (element.GetValue("mc:Ignorable") is { } ignorable) result.Add(new("mc:Ignorable", ignorable));
            if (!string.IsNullOrEmpty(document.XClass)) result.Add(new("x:Class", document.XClass));
        }

        if (element.Name is { } name) result.Add(new("x:Name", name));

        var attrs = element.Properties.Where(p => p.IsAttributeForm && p.StringValue is not null && !(isRoot && p.Name == "mc:Ignorable"))
            .Where(p => !IsDefault(element, p.Name, p.StringValue!))
            .ToList();

        // x: directives (x:DataType, x:CompileBindings...) next.
        foreach (var p in attrs.Where(p => p.Name.StartsWith("x:", StringComparison.Ordinal)))
            result.Add(new(p.Name, p.StringValue!));

        var rest = attrs.Where(p => !p.Name.StartsWith("x:", StringComparison.Ordinal)).ToList();
        var layout = rest.Where(p => IsLayout(d, p.Name))
            .OrderBy(p => Array.IndexOf(LayoutOrder, p.Name) is var i && i >= 0 ? i : LayoutOrder.Length).ToList();
        var content = rest.Where(p => !layout.Contains(p) && IsContent(d, p.Name)).ToList();
        var prefixed = rest.Where(p => p.Name.Contains(':')).ToList();
        var others = rest.Except(layout).Except(content).Except(prefixed)
            .OrderBy(p => d?.FindProperty(p.Name) is { } pd ? PropertyCategories.IndexOf(pd.Category) : PropertyCategories.Order.Count)
            .ToList();

        foreach (var p in layout.Concat(content).Concat(others).Concat(prefixed))
            result.Add(new(p.Name, p.StringValue!));

        foreach (var ap in element.AttachedProperties.Where(a => a.StringValue is not null)
                     .OrderBy(a => a.FullName, StringComparer.Ordinal))
        {
            if (!IsDefaultAttached(element, ap)) result.Add(new(ap.FullName, ap.StringValue!));
        }

        // Handlers wired in code-behind (see CodeBehindGenerator.IsWiredInCode) are omitted when requested;
        // template elements and invalid handler names are never wired in code, so they always stay in XAML.
        foreach (var ev in element.EventHandlers.Where(e => !string.IsNullOrWhiteSpace(e.HandlerName)))
            if (emitNamedEvents || isRoot || !CodeBehindGenerator.IsWiredInCode(element, ev))
                result.Add(new(ev.EventName, ev.HandlerName));

        return result;
    }

    private static int NamespaceRank(XamlNamespaceDeclaration ns) => ns.Prefix switch
    {
        "" => 0,
        "x" => 1,
        "d" => 2,
        "mc" => 3,
        _ => 4,
    };

    private static bool IsLayout(ControlDescriptor? d, string name) =>
        Array.IndexOf(LayoutOrder, name) >= 0 || d?.FindProperty(name)?.Category == PropertyCategories.Layout;

    private static bool IsContent(ControlDescriptor? d, string name) =>
        name == d?.ContentProperty || ContentNames.Contains(name);

    private static bool IsDefault(DesignElement element, string name, string value)
    {
        var pd = element.Descriptor?.FindProperty(name);
        if (pd?.DefaultValue is null) return false;
        var def = XamlValueConverter.Serialize(pd.DefaultValue, pd.PropertyType);
        return XamlValueConverter.AreEquivalent(value, def, pd.PropertyType);
    }

    private static bool IsDefaultAttached(DesignElement element, DesignAttachedProperty ap)
    {
        var pd = element.Parent?.Descriptor?.ChildAttachedProperties.FirstOrDefault(p => p.FullName == ap.FullName);
        if (pd?.DefaultValue is null) return false;
        var def = XamlValueConverter.Serialize(pd.DefaultValue, pd.PropertyType);
        return XamlValueConverter.AreEquivalent(ap.StringValue, def, pd.PropertyType);
    }

    internal static string EscapeAttribute(string value) => value
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;")
        .Replace("\r", "&#13;").Replace("\n", "&#10;").Replace("\t", "&#9;");

    internal static string EscapeText(string value) => value
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private sealed class Writer(DesignDocument? document, bool emitNamedEvents)
    {
        private readonly StringBuilder _sb = new();

        public Dictionary<DesignElement, (int, int)> Spans { get; } = new(ReferenceEqualityComparer.Instance);

        public void AppendRaw(string s) => _sb.Append(s);

        public override string ToString() => _sb.ToString();

        public void WriteElement(DesignElement element, int depth, bool isRoot)
        {
            var pad = string.Concat(Enumerable.Repeat(Indent, depth));
            var start = _sb.Length + pad.Length;
            var attrs = GetOrderedAttributes(element, document, isRoot, emitNamedEvents);
            var tag = element.ElementType;

            _sb.Append(pad).Append('<').Append(tag);
            var formatted = attrs.Select(a => $"{a.Key}=\"{EscapeAttribute(a.Value)}\"").ToList();
            var singleLine = !isRoot && (formatted.Count <= 1 ||
                                         pad.Length + tag.Length + 4 + formatted.Sum(f => f.Length + 1) <= MaxSingleLineLength);
            if (formatted.Count > 0)
            {
                if (singleLine)
                {
                    foreach (var f in formatted) _sb.Append(' ').Append(f);
                }
                else
                {
                    var align = new string(' ', pad.Length + tag.Length + 2);
                    _sb.Append(' ').Append(formatted[0]);
                    for (var i = 1; i < formatted.Count; i++) _sb.Append('\n').Append(align).Append(formatted[i]);
                }
            }

            var propertyElements = element.Properties.Where(p => !p.IsAttributeForm).ToList();
            var hasBody = propertyElements.Count > 0 || element.Children.Count > 0;
            if (!hasBody && element.InnerText is { Length: > 0 } inner)
            {
                _sb.Append('>').Append(EscapeText(inner)).Append("</").Append(tag).Append(">\n");
                Spans[element] = (start, _sb.Length - 1 - start);
                return;
            }
            if (!hasBody)
            {
                _sb.Append(" />\n");
                Spans[element] = (start, _sb.Length - 1 - start);
                return;
            }

            _sb.Append(">\n");
            var childPad = pad + Indent;
            if (element.InnerText is { Length: > 0 } text) _sb.Append(childPad).Append(EscapeText(text)).Append('\n');

            foreach (var prop in propertyElements)
            {
                var propTag = prop.Name.Contains('.') ? prop.Name : $"{tag}.{prop.Name}";
                if (prop.ElementValues.Count == 0 && prop.ElementText is null)
                {
                    _sb.Append(childPad).Append('<').Append(propTag).Append(" />\n");
                    continue;
                }
                if (prop.ElementValues.Count == 0)
                {
                    _sb.Append(childPad).Append('<').Append(propTag).Append('>').Append(EscapeText(prop.ElementText!))
                        .Append("</").Append(propTag).Append(">\n");
                    continue;
                }
                _sb.Append(childPad).Append('<').Append(propTag).Append(">\n");
                if (prop.ElementText is { } pt) _sb.Append(childPad).Append(Indent).Append(EscapeText(pt)).Append('\n');
                foreach (var v in prop.ElementValues) WriteElement(v, depth + 2, false);
                _sb.Append(childPad).Append("</").Append(propTag).Append(">\n");
            }

            foreach (var child in element.Children) WriteElement(child, depth + 1, false);

            _sb.Append(pad).Append("</").Append(tag).Append(">\n");
            Spans[element] = (start, _sb.Length - 1 - start);
        }
    }
}
