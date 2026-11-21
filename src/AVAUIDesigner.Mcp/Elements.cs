using System.Globalization;
using System.Text.Json.Nodes;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Mcp;

/// <summary>Finding, describing, naming and placing a design's elements, as the tools need.</summary>
public static class Elements
{
    /// <summary>
    /// An element by its x:Name, its path of child positions ("0/2/1", with "@Columns/0" for an element inside a
    /// property element), or "root". Throws an error naming what there is.
    /// </summary>
    public static DesignElement Find(DesignDocument doc, string target)
    {
        var t = target.Trim();
        if (t.Length == 0) throw new ArgumentException("Name an element: its x:Name, its path (\"0/1\"), or \"root\".");
        if (t is "root" or "/" or "") return doc.RootElement;
        if (doc.NamedElements.TryGetValue(t, out var named)) return named;
        if (t.Contains('/') || t.All(char.IsDigit) || t.StartsWith('@'))
        {
            var e = doc.RootElement;
            var parts = t.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].StartsWith('@'))
                {
                    var prop = e.GetPropertyElement(parts[i][1..]) ?? throw new ArgumentException($"{Label(e)} has no {parts[i][1..]} property element (path {t}).");
                    if (i + 1 >= parts.Length || !int.TryParse(parts[i + 1], out var pi) || pi < 0 || pi >= prop.ElementValues.Count)
                        throw new ArgumentException($"Path {t}: {parts[i][1..]} has {prop.ElementValues.Count} element(s); give its position after it.");
                    e = prop.ElementValues[pi];
                    i++;
                    continue;
                }
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= e.Children.Count)
                    throw new ArgumentException($"There's no element at path {t}: {Label(e)} has {e.Children.Count} child{(e.Children.Count == 1 ? "" : "ren")}.");
                e = e.Children[index];
            }
            return e;
        }
        var near = doc.NamedElements.Keys.Where(k => k.Contains(t, StringComparison.OrdinalIgnoreCase) || t.Contains(k, StringComparison.OrdinalIgnoreCase)).Take(6).ToList();
        throw new ArgumentException($"There's no element called \"{t}\"." + (near.Count > 0 ? $" Did you mean {string.Join(", ", near)}?" : " get_document shows the elements, their names and paths."));
    }

    public static List<DesignElement> Targets(DesignDocument doc, string? target, List<string>? targets)
    {
        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(target)) names.Add(target);
        if (targets != null) names.AddRange(targets);
        if (names.Count == 0) throw new ArgumentException("Name the element (target) or elements (targets).");
        var list = names.Select(n => Find(doc, n)).Distinct().ToList();
        // An element inside another one in the list goes with it.
        return list.Where(e => !list.Any(o => !ReferenceEquals(o, e) && o.IsAncestorOf(e))).ToList();
    }

    /// <summary>The path to an element: child positions from the root, with "@Property/n" for property elements.</summary>
    public static string PathOf(DesignElement e)
    {
        var parts = new List<string>();
        for (var x = e; x.Parent is { } p; x = p)
        {
            if (x.OwnerProperty is { } prop) parts.Add($"@{prop.Name}/{prop.ElementValues.IndexOf(x)}");
            else parts.Add(p.Children.IndexOf(x).ToString(CultureInfo.InvariantCulture));
        }
        parts.Reverse();
        return parts.Count == 0 ? "root" : string.Join('/', parts);
    }

    /// <summary>"TextBox firstNameBox" or "Grid at 0/1".</summary>
    public static string Label(DesignElement e) =>
        e.Name is { } n ? $"{CodeIdentifiers.BareTypeName(e.ElementType)} {n}" : e.Parent is null ? $"{e.ElementType} (root)" : $"{CodeIdentifiers.BareTypeName(e.ElementType)} at {PathOf(e)}";

    /// <summary>An element and what's inside it, for get_document.</summary>
    public static JsonObject Describe(DesignElement e, int depth)
    {
        var o = new JsonObject { ["type"] = e.ElementType };
        if (e.Name != null) o["name"] = e.Name;
        o["path"] = PathOf(e);
        var props = new JsonObject();
        foreach (var p in e.Properties.Where(p => p.IsAttributeForm && p.Name is not ("x:Name" or "xmlns")))
            props[p.Name] = Short(p.StringValue);
        foreach (var a in e.AttachedProperties) props[a.FullName] = Short(a.StringValue);
        if (e.InnerText is { Length: > 0 } text) props["(text)"] = Short(text);
        if (props.Count > 0) o["props"] = props;
        if (e.EventHandlers.Count > 0) o["events"] = new JsonObject(e.EventHandlers.Select(h => KeyValuePair.Create(h.EventName, (JsonNode?)h.HandlerName)));
        var elements = e.PropertyElements.Where(p => p.ElementValues.Count > 0 || p.ElementText != null).ToList();
        if (elements.Count > 0)
            o["property_elements"] = new JsonObject(elements.Select(p => KeyValuePair.Create(p.Name, (JsonNode?)(p.ElementValues.Count > 0
                ? depth > 1 ? new JsonArray(p.ElementValues.Select(c => (JsonNode)Describe(c, depth - 1)).ToArray()) : $"{p.ElementValues.Count} element(s)"
                : Short(p.ElementText)))));
        if (e.Children.Count > 0)
            o["children"] = depth > 1
                ? new JsonArray(e.Children.Select(c => (JsonNode)Describe(c, depth - 1)).ToArray())
                : $"{e.Children.Count} child element(s): ask for this element to see them";
        return o;
    }

    private static string? Short(string? s) => s is { Length: > 120 } ? s[..117] + "…" : s;

    /// <summary>Gives every visual element under <paramref name="e"/> (itself too) a unique x:Name, keeping ones it has (made unique).</summary>
    public static void NameAll(DesignDocument doc, DesignElement e)
    {
        foreach (var x in e.SelfAndDescendants().ToList())
        {
            if (x.OwnerProperty != null) continue;
            var bare = CodeIdentifiers.BareTypeName(x.ElementType);
            if (x.Name is { } n)
            {
                var unique = doc.MakeUniqueName(n, x);
                if (unique != n) x.Name = unique;
                continue;
            }
            if (x.Parent is null || bare is "ComboBoxItem" or "ListBoxItem" or "RowDefinition" or "ColumnDefinition" or "Separator" or "Run" or "LineBreak") continue;
            x.Name = doc.GenerateUniqueName(bare);
        }
    }

    /// <summary>Puts elements into a parent's children, checking it can hold them.</summary>
    public static void Insert(DesignElement parent, List<DesignElement> items, int? index, bool replace, ControlDescriptorRegistry registry)
    {
        var d = parent.Descriptor;
        if (d is { IsContainer: false })
        {
            var content = d.ContentProperty;
            if (content is not null && items.Count == 1 && parent.Children.Count == 0 && !parent.HasValue(content))
            {
                // A Button or Label takes one element as its content.
                parent.AddChild(items[0]);
                return;
            }
            throw new ArgumentException($"{Label(parent)} can't hold other controls. Put them in a panel (Grid, StackPanel…) instead.");
        }
        foreach (var item in items)
            if (d != null && !d.AcceptsChild(item.ElementType))
                throw new ArgumentException($"A {d.ElementName} holds only {string.Join(", ", d.AllowedChildTypes ?? [])}, not a {item.ElementType}.");
        if (replace)
            foreach (var c in parent.Children.ToList()) parent.RemoveChild(c);
        var max = d?.MaxChildren ?? int.MaxValue;
        if (parent.Children.Count + items.Count > max)
        {
            var only = parent.Children.Count == 1 ? parent.Children[0] : null;
            throw new ArgumentException(max == 1
                ? $"{Label(parent)} holds one child" + (only != null ? $" and already has {Label(only)}: add into that (its name or path {PathOf(only)}), or pass replace true to rebuild it." : items.Count > 1 ? ": wrap the new controls in one panel (a Grid or StackPanel)." : ".")
                : $"{Label(parent)} holds at most {max} children.");
        }
        var at = index is { } i ? Math.Clamp(i, 0, parent.Children.Count) : parent.Children.Count;
        foreach (var item in items)
        {
            if (item.Parent != null) item.Detach();
            parent.AddChild(item, at++);
            item.AttachDescriptors(registry);
        }
    }

    /// <summary>Things worth telling an agent about an element and its contents: unknown controls and properties.</summary>
    public static IEnumerable<string> Check(DesignElement root, ControlDescriptorRegistry registry)
    {
        foreach (var e in root.SelfAndAllDescendants())
        {
            var bare = CodeIdentifiers.BareTypeName(e.ElementType);
            if (e.Descriptor is null && !e.ElementType.Contains(':') && registry.GetByElementName(bare) is null && !KnownExtras.Contains(bare))
                yield return $"The designer doesn't know {e.ElementType}: it is kept in the XAML and shown as a placeholder. Check the spelling (list_controls).";
            foreach (var p in e.Properties.Where(p => p.IsAttributeForm))
                foreach (var w in CheckProperty(e, p.Name, registry))
                    yield return w;
        }
    }

    private static readonly HashSet<string> KnownExtras =
    [
        "RowDefinition", "ColumnDefinition", "DataGridCheckBoxColumn", "DataGridTemplateColumn", "SelectableTextBlock", "HeaderedContentControl",
        "Run", "LineBreak", "Bold", "Italic", "Underline", "Span", "InlineUIContainer", "Panel", "ContentControl", "Carousel", "TransitioningContentControl",
        "RelativePanel", "LayoutTransformControl", "ButtonSpinner", "SplitButton", "ToggleSplitButton", "RepeatButton", "HyperlinkButton", "MaskedTextBox",
        "ItemsRepeater", "Label", "NativeMenu", "ThemeVariantScope", "RefreshContainer", "DropDownButton",
    ];

    private static readonly Dictionary<string, string> Hints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Placeholder"] = "Watermark", ["PlaceholderText"] = "Watermark (on a TextBox) or PlaceholderText (on a ComboBox)", ["Visibility"] = "IsVisible",
        ["MaxLines"] = "MaxLines on a TextBlock", ["Enabled"] = "IsEnabled", ["Checked"] = "IsChecked", ["ReadOnly"] = "IsReadOnly", ["Children"] = "child elements",
    };

    /// <summary>A note if <paramref name="name"/> isn't a property the designer knows for the element (with a likely fix), else nothing.</summary>
    public static IEnumerable<string> CheckProperty(DesignElement e, string name, ControlDescriptorRegistry registry)
    {
        if (e.Descriptor is not { } d || name.Contains(':') || name.Contains('.') || name is "Classes" or "Name" or "Tag" or "Theme" or "Styles" or "Resources" or "DataContext") yield break;
        if (d.FindProperty(name) != null) yield break;
        if (d.ClrType is { } t && t.GetProperty(name) != null) yield break;
        if (d.ClrType == null && !Hints.ContainsKey(name)) yield break;
        var hint = Hints.TryGetValue(name, out var h) ? $" (did you mean {h}?)" : "";
        if (name == "PlaceholderText" && d.ElementName == "ComboBox") yield break;
        yield return $"{d.ElementName} has no property {name}{hint}: it is kept in the XAML, but Avalonia will reject it when the app builds.";
    }

    public static double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
}
