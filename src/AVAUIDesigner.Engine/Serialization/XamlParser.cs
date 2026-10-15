using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Properties;

namespace AVAUIDesigner.Engine.Serialization;

/// <summary>Result of a detailed parse: the document plus diagnostics and source positions.</summary>
public class XamlParseResult
{
    public required DesignDocument Document { get; init; }
    public List<XamlParseWarning> Warnings { get; } = [];

    /// <summary>1-based line/column of each element's start tag.</summary>
    public Dictionary<DesignElement, (int Line, int Column)> ElementLocations { get; } = new(ReferenceEqualityComparer.Instance);
}

/// <summary>Parses .axaml text into a <see cref="DesignDocument"/>.</summary>
public class XamlParser
{
    private const string FragmentRoot = "AVAUIDesignerFragment";

    /// <summary>Parses axaml text. Throws <see cref="XamlParseException"/> on failure.</summary>
    public DesignDocument Parse(string axamlText, ControlDescriptorRegistry registry) =>
        ParseDetailed(axamlText, registry).Document;

    /// <summary>Attempts a parse; returns false and error info on failure (never throws).</summary>
    public bool TryParse(string axamlText, ControlDescriptorRegistry registry,
        out DesignDocument? document, out XamlParseError? error)
    {
        if (TryParseDetailed(axamlText, registry, out var result, out error))
        {
            document = result!.Document;
            return true;
        }
        document = null;
        return false;
    }

    public bool TryParseDetailed(string axamlText, ControlDescriptorRegistry registry,
        out XamlParseResult? result, out XamlParseError? error)
    {
        try
        {
            result = ParseDetailed(axamlText, registry);
            error = null;
            return true;
        }
        catch (XamlParseException ex)
        {
            result = null;
            error = ex.Error;
            return false;
        }
    }

    public XamlParseResult ParseDetailed(string axamlText, ControlDescriptorRegistry registry)
    {
        XDocument xdoc;
        try
        {
            xdoc = XDocument.Parse(axamlText, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            throw new XamlParseException(new XamlParseError(ex.Message, ex.LineNumber, ex.LinePosition), ex);
        }

        var rootX = xdoc.Root ?? throw new XamlParseException(new XamlParseError("Document has no root element.", 1, 1));
        var context = new ParseContext(registry);
        var root = ReadElement(rootX, context);

        var document = new DesignDocument(root);
        document.Namespaces.Clear();
        foreach (var attr in rootX.Attributes().Where(a => a.IsNamespaceDeclaration))
        {
            var prefix = attr.Name.Namespace == XNamespace.None ? "" : attr.Name.LocalName;
            document.Namespaces.Add(new XamlNamespaceDeclaration(prefix, attr.Value));
        }
        // Declarations on nested elements are hoisted to the root (the serializer only writes them there).
        foreach (var attr in rootX.Descendants().SelectMany(e => e.Attributes()).Where(a => a.IsNamespaceDeclaration))
        {
            var prefix = attr.Name.Namespace == XNamespace.None ? "" : attr.Name.LocalName;
            if (document.Namespaces.All(n => n.Prefix != prefix))
                document.Namespaces.Add(new XamlNamespaceDeclaration(prefix, attr.Value));
        }

        // x:Class is document-level metadata.
        if (root.Properties.FirstOrDefault(p => p.Name == "x:Class") is { } xClass)
        {
            document.XClass = xClass.StringValue;
            root.Properties.Remove(xClass);
        }

        document.CodeStyle = root.GetValue("x:DataType") is not null || root.GetPropertyElement("DataContext") is not null
            ? CodeBehindStyle.MVVM
            : CodeBehindStyle.CodeBehind;

        var result = new XamlParseResult { Document = document };
        result.Warnings.AddRange(context.Warnings);
        foreach (var (e, loc) in context.Locations) result.ElementLocations[e] = loc;
        return result;
    }

    /// <summary>
    /// Parses a XAML fragment (one or more sibling elements, e.g. from the clipboard) using the
    /// namespace declarations of <paramref name="context"/>.
    /// </summary>
    public List<DesignElement> ParseFragment(string xaml, DesignDocument context, ControlDescriptorRegistry registry)
    {
        var decls = string.Join(" ", context.Namespaces.Select(n => $"{n.AttributeName}=\"{EscapeAttr(n.Uri)}\""));
        if (!context.Namespaces.Any(n => n.Prefix == "")) decls += $" xmlns=\"{XamlNamespaceMap.Avalonia}\"";
        if (!context.Namespaces.Any(n => n.Prefix == "x")) decls += $" xmlns:x=\"{XamlNamespaceMap.Xaml}\"";
        var wrapped = $"<{FragmentRoot} {decls}>{xaml}</{FragmentRoot}>";

        XElement wrapper;
        try
        {
            wrapper = XElement.Parse(wrapped, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            throw new XamlParseException(new XamlParseError(ex.Message, ex.LineNumber, ex.LinePosition), ex);
        }

        var ctx = new ParseContext(registry);
        return wrapper.Elements().Where(x => !x.Name.LocalName.Contains('.')).Select(x => ReadElement(x, ctx)).ToList();
    }

    private sealed class ParseContext(ControlDescriptorRegistry registry)
    {
        public ControlDescriptorRegistry Registry { get; } = registry;
        public List<XamlParseWarning> Warnings { get; } = [];
        public Dictionary<DesignElement, (int, int)> Locations { get; } = new(ReferenceEqualityComparer.Instance);
    }

    private static DesignElement ReadElement(XElement x, ParseContext ctx)
    {
        var elementType = QualifiedName(x, x.Name);
        var descriptor = ctx.Registry.GetByElementName(elementType);
        // A prefixed element only matches a descriptor when it is in the Avalonia namespace.
        if (descriptor is not null && elementType.Contains(':') && x.Name.NamespaceName != XamlNamespaceMap.Avalonia)
            descriptor = null;

        var element = new DesignElement(elementType, descriptor);
        if (x is IXmlLineInfo li && li.HasLineInfo()) ctx.Locations[element] = (li.LineNumber, li.LinePosition);

        foreach (var attr in x.Attributes())
        {
            if (attr.IsNamespaceDeclaration) continue;
            ReadAttribute(element, attr, ctx);
        }

        var text = string.Concat(x.Nodes().OfType<XText>().Select(t => t.Value)).Trim();
        var hasElementChildren = false;

        foreach (var childX in x.Elements())
        {
            var local = childX.Name.LocalName;
            if (local.Contains('.'))
            {
                ReadPropertyElement(element, childX, ctx);
                continue;
            }
            hasElementChildren = true;
            element.AddChild(ReadElement(childX, ctx));
        }

        if (text.Length > 0)
        {
            var contentProp = descriptor?.ContentProperty;
            if (!hasElementChildren && contentProp is not null && contentProp is "Content" or "Text" or "Header"
                && element.GetValue(contentProp) is null)
                element.SetValue(contentProp, text);
            else
                element.InnerText = text;
        }

        return element;
    }

    private static void ReadPropertyElement(DesignElement owner, XElement px, ParseContext ctx)
    {
        var local = px.Name.LocalName;
        var dot = local.IndexOf('.');
        var ownerType = local[..dot];
        var propName = local[(dot + 1)..];
        var bareOwner = owner.ElementType.Contains(':') ? owner.ElementType[(owner.ElementType.IndexOf(':') + 1)..] : owner.ElementType;
        var name = ownerType == bareOwner ? propName : local;

        var prop = owner.GetOrCreatePropertyElement(name);
        foreach (var valueX in px.Elements())
            owner.AddToPropertyElement(prop, ReadElement(valueX, ctx));

        var text = string.Concat(px.Nodes().OfType<XText>().Select(t => t.Value)).Trim();
        if (text.Length > 0)
        {
            if (prop.ElementValues.Count == 0)
            {
                // <Button.Content>OK</Button.Content> is just an attribute value in element form.
                owner.Properties.Remove(prop);
                owner.SetValue(name, text);
            }
            else prop.ElementText = text;
        }
    }

    private static void ReadAttribute(DesignElement element, XAttribute attr, ParseContext ctx)
    {
        var ns = attr.Name.Namespace;
        var local = attr.Name.LocalName;

        if (ns == XNamespace.None)
        {
            if (local == "Name")
            {
                element.Name = attr.Value;
                return;
            }
            var descriptor = element.Descriptor;
            var isEvent = descriptor?.FindEvent(local) is not null && !XamlValueConverter.IsMarkupExtension(attr.Value);
            if (isEvent)
            {
                element.SetEventHandler(local, attr.Value);
                return;
            }
            if (DesignElement.IsAttachedName(local, out var owner, out _))
            {
                var bare = element.ElementType.Contains(':') ? element.ElementType[(element.ElementType.IndexOf(':') + 1)..] : element.ElementType;
                if (owner != bare)
                {
                    element.SetValue(local, attr.Value);
                    return;
                }
                local = local[(owner.Length + 1)..];
            }
            element.Properties.Add(new DesignProperty(local, attr.Value));
            WarnIfUnknown(element, local, attr, ctx);
            return;
        }

        var prefix = attr.Parent?.GetPrefixOfNamespace(ns) ?? "x";
        if (ns.NamespaceName == XamlNamespaceMap.Xaml && local == "Name")
        {
            element.Name = attr.Value;
            return;
        }
        element.Properties.Add(new DesignProperty($"{prefix}:{local}", attr.Value));
    }

    private static void WarnIfUnknown(DesignElement element, string name, XAttribute attr, ParseContext ctx)
    {
        var clr = element.Descriptor?.ClrType;
        if (clr is null) return;
        if (element.Descriptor!.FindProperty(name) is not null || element.Descriptor.FindEvent(name) is not null) return;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        if (clr.GetProperty(name, flags) is not null || clr.GetEvent(name, flags) is not null) return;
        if (attr is IXmlLineInfo li && li.HasLineInfo())
            ctx.Warnings.Add(new XamlParseWarning($"'{name}' is not a known property or event of {element.ElementType}.",
                li.LineNumber, li.LinePosition, name.Length));
    }

    /// <summary>"Button" for default-namespace elements, "local:MyControl" otherwise.</summary>
    private static string QualifiedName(XElement scope, XName name)
    {
        if (name.Namespace == XNamespace.None || name.Namespace == scope.GetDefaultNamespace())
            return name.LocalName;
        var prefix = scope.GetPrefixOfNamespace(name.Namespace);
        return string.IsNullOrEmpty(prefix) ? name.LocalName : $"{prefix}:{name.LocalName}";
    }

    private static string EscapeAttr(string s) => s.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;");
}
