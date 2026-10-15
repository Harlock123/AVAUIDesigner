using System.Collections;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Transformation;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Properties;

namespace AVAUIDesigner.Shell.Design;

/// <summary>
/// Builds live Avalonia controls from the design DOM for the design surface. Property values are
/// applied generically through the Avalonia property system (falling back to CLR reflection), so
/// any control the registry knows about renders without bespoke code.
/// </summary>
public class PreviewBuilder
{
    private static readonly Assembly[] TypeAssemblies =
    [
        typeof(Button).Assembly, typeof(Layoutable).Assembly, typeof(Rectangle).Assembly, typeof(DataGrid).Assembly,
    ];

    private static readonly Dictionary<string, Type?> TypeCache = new();

    private readonly ControlDescriptorRegistry _registry;

    public PreviewBuilder(ControlDescriptorRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>Directory used to resolve relative image paths.</summary>
    public string? BaseDirectory { get; set; }

    /// <summary>Element → the control that represents it on the surface (for the root: its client area).</summary>
    public Dictionary<DesignElement, Control> Map { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>The frame drawn around the root element (title bar, border).</summary>
    public Control? RootFrame { get; private set; }

    /// <summary>The root's client area panel; children of the root are hosted here.</summary>
    public Panel? ClientArea { get; private set; }

    /// <summary>Layer inside the client area under the content, for the background dot grid.</summary>
    public Panel? GridLayer { get; private set; }

    public Size RootSize { get; private set; }

    /// <summary>Builds the whole document. The returned control is the root frame.</summary>
    public Control BuildRoot(DesignDocument document)
    {
        Map.Clear();
        var root = document.RootElement;
        var bare = Bare(root.ElementType);
        var isWindow = bare == "Window";
        var defaultSize = root.Descriptor?.DefaultSize ?? new Size(400, 300);

        var width = FirstNumber(root.GetValue("Width"), root.GetValue("d:DesignWidth"))
                    ?? (double.IsNaN(defaultSize.Width) ? 400 : defaultSize.Width);
        var height = FirstNumber(root.GetValue("Height"), root.GetValue("d:DesignHeight"))
                     ?? (double.IsNaN(defaultSize.Height) ? 300 : defaultSize.Height);
        RootSize = new Size(width, height);

        GridLayer = new Panel { IsHitTestVisible = false };
        ClientArea = new Panel
        {
            Width = width,
            Height = height,
            ClipToBounds = true,
        };
        if (ResolveBrush(root.GetValue("Background")) is { } rootBackground) ClientArea.Background = rootBackground;
        else ClientArea.Bind(Panel.BackgroundProperty, ClientArea.GetResourceObservable("SystemRegionBrush"));
        ClientArea.Children.Add(GridLayer);
        ApplyInheritedText(ClientArea, root);

        if (bare is "Window" or "UserControl")
        {
            var content = new Panel();
            if (root.GetValue("Padding") is { } padding && XamlValueConverter.TryDeserialize(padding, typeof(Thickness), out var pad))
                content.Margin = (Thickness)pad!;
            foreach (var child in root.Children)
                if (BuildElement(child) is { } c) content.Children.Add(c);
            ClientArea.Children.Add(content);
        }
        else if (root.Descriptor?.ClrType is { } t && typeof(Control).IsAssignableFrom(t) && !typeof(TopLevel).IsAssignableFrom(t))
        {
            // A bare control as the document root (e.g. a Grid in a resource-less file).
            if (BuildElement(root) is { } c) ClientArea.Children.Add(c);
        }
        else
        {
            ClientArea.Children.Add(new TextBlock
            {
                Text = $"<{root.ElementType}> cannot be previewed on the design surface.\nEdit it in the XAML view.",
                Margin = new Thickness(16),
                Foreground = Brushes.Gray,
            });
        }
        Map[root] = ClientArea;

        Control frame;
        if (isWindow)
        {
            var title = root.GetValue("Title") ?? "Window";
            var titleBar = new DockPanel { Height = 30 };
            titleBar.Bind(Panel.BackgroundProperty, titleBar.GetResourceObservable("SystemControlBackgroundChromeMediumBrush"));
            titleBar.Bind(TextElement.ForegroundProperty, titleBar.GetResourceObservable("SystemControlForegroundBaseHighBrush"));
            var caption = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 14,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            foreach (var glyph in new[] { "—", "☐", "✕" })
                caption.Children.Add(new TextBlock { Text = glyph, FontSize = 12, Opacity = 0.7 });
            DockPanel.SetDock(caption, Dock.Right);
            titleBar.Children.Add(caption);
            titleBar.Children.Add(new TextBlock
            {
                Text = title,
                Margin = new Thickness(10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            var stack = new DockPanel();
            DockPanel.SetDock(titleBar, Dock.Top);
            stack.Children.Add(titleBar);
            stack.Children.Add(ClientArea);
            frame = new Border
            {
                Child = stack,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.Parse("#808080")),
                BoxShadow = BoxShadows.Parse("0 4 16 0 #40000000"),
            };
        }
        else
        {
            frame = new Border
            {
                Child = ClientArea,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.Parse("#A0A0A0")),
                BoxShadow = BoxShadows.Parse("0 2 10 0 #30000000"),
            };
        }

        frame.IsHitTestVisible = false;
        RootFrame = frame;
        return frame;
    }

    /// <summary>Builds the control for one element and its subtree; null for non-visual elements.</summary>
    public Control? BuildElement(DesignElement element)
    {
        Control control;
        var descriptor = element.Descriptor;
        if (descriptor is null || descriptor.RenderAsPlaceholder || descriptor.ClrType is null
            || !typeof(Control).IsAssignableFrom(descriptor.ClrType) || typeof(TopLevel).IsAssignableFrom(descriptor.ClrType))
        {
            control = CreatePlaceholder(element);
            ApplyLayoutOnly(control, element);
        }
        else
        {
            try
            {
                control = descriptor.ControlFactory?.Invoke(element) ?? (Control)Activator.CreateInstance(descriptor.ClrType)!;
            }
            catch (Exception)
            {
                control = CreatePlaceholder(element);
                ApplyLayoutOnly(control, element);
                Map[element] = control;
                return control;
            }
            ApplyProperties(control, element);
            ApplyPropertyElements(control, element);
            AddChildren(control, element);
        }

        if (!element.IsVisible) control.IsVisible = false;
        control.Focusable = false;
        Map[element] = control;
        return control;
    }

    // ---------------------------------------------------------------- properties

    private void ApplyProperties(Control control, DesignElement element)
    {
        foreach (var prop in element.Properties)
        {
            if (!prop.IsAttributeForm || prop.StringValue is null) continue;
            if (prop.Name.Contains(':')) continue; // x:, d:, mc: directives
            ApplyValue(control, prop.Name, prop.StringValue);
        }
        foreach (var ap in element.AttachedProperties)
            if (ap.StringValue is not null) ApplyValue(control, ap.FullName, ap.StringValue);

        if (element.InnerText is { Length: > 0 } text)
        {
            switch (control)
            {
                case TextBlock tb when tb.Text is null: tb.Text = text; break;
                case ContentControl cc when cc.Content is null: cc.Content = text; break;
            }
        }
    }

    private static void ApplyLayoutOnly(Control control, DesignElement element)
    {
        foreach (var name in new[] { "Width", "Height", "Margin", "HorizontalAlignment", "VerticalAlignment" })
            if (element.GetValue(name) is { } v) ApplyValue(control, name, v);
        foreach (var ap in element.AttachedProperties)
            if (ap.StringValue is not null) ApplyValue(control, ap.FullName, ap.StringValue);
    }

    /// <summary>
    /// Applies one XAML attribute value to a live control. Used both for building and for live
    /// feedback while dragging. Unknown names, markup extensions and invalid values are ignored.
    /// </summary>
    public static bool ApplyValue(AvaloniaObject target, string name, string? value)
    {
        try
        {
            if (DesignElement.IsAttachedName(name, out var owner, out var bare))
                return ApplyAttached(target, owner, bare, value);

            switch (name)
            {
                case "Classes" when target is StyledElement se:
                    se.Classes.Clear();
                    if (value is not null) se.Classes.AddRange(value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                    return true;
                case "RowDefinitions" when target is Grid g:
                    g.RowDefinitions = value is null ? new RowDefinitions() : new RowDefinitions(value);
                    return true;
                case "ColumnDefinitions" when target is Grid g:
                    g.ColumnDefinitions = value is null ? new ColumnDefinitions() : new ColumnDefinitions(value);
                    return true;
                case "Points" when target is Polygon or Polyline:
                    var points = ParsePoints(value);
                    if (target is Polygon pg) pg.Points = points;
                    else ((Polyline)target).Points = points;
                    return true;
                case "Cursor" or "Name" or "Icon" or "Command" or "CommandParameter" or "ItemsSource" or "DataContext":
                    return false;
            }

            var ap = AvaloniaPropertyRegistry.Instance.FindRegistered(target, name);
            if (ap is not null && !ap.IsReadOnly)
            {
                if (value is null)
                {
                    target.ClearValue(ap);
                    return true;
                }
                if (XamlValueConverter.IsMarkupExtension(value)) return false;
                if (ConvertValue(value, ap.PropertyType, out var converted))
                {
                    target.SetValue(ap, converted);
                    return true;
                }
                return false;
            }

            var clr = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (clr is { CanWrite: true } && value is not null && !XamlValueConverter.IsMarkupExtension(value)
                && ConvertValue(value, clr.PropertyType, out var v))
            {
                clr.SetValue(target, v);
                return true;
            }
        }
        catch (Exception)
        {
            // A bad design-time value must never break the surface.
        }
        return false;
    }

    private static bool ApplyAttached(AvaloniaObject target, string owner, string name, string? value)
    {
        var ownerType = ResolveType(owner);
        var field = ownerType?.GetField(name + "Property", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        if (field?.GetValue(null) is not AvaloniaProperty ap) return false;
        if (value is null)
        {
            target.ClearValue(ap);
            return true;
        }
        if (XamlValueConverter.IsMarkupExtension(value)) return false;
        if (!ConvertValue(value, ap.PropertyType, out var converted)) return false;
        target.SetValue(ap, converted);
        return true;
    }

    private static bool ConvertValue(string text, Type type, out object? value)
    {
        if (type == typeof(IImage))
        {
            value = null;
            return false;
        }
        if (type == typeof(Geometry))
        {
            value = Geometry.Parse(text);
            return true;
        }
        if (type == typeof(IList<Point>))
        {
            value = ParsePoints(text);
            return true;
        }
        if (type == typeof(BoxShadows))
        {
            value = BoxShadows.Parse(text);
            return true;
        }
        if (type == typeof(ITransform))
        {
            value = TransformOperations.Parse(text);
            return true;
        }
        return XamlValueConverter.TryDeserialize(text, type, out value);
    }

    private static Avalonia.Collections.AvaloniaList<Point> ParsePoints(string? text)
    {
        var list = new Avalonia.Collections.AvaloniaList<Point>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        var numbers = text.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => XamlValueConverter.TryParseDouble(s, out var d) ? d : 0).ToList();
        for (var i = 0; i + 1 < numbers.Count; i += 2) list.Add(new Point(numbers[i], numbers[i + 1]));
        return list;
    }

    private void ApplyInheritedText(Control target, DesignElement root)
    {
        foreach (var name in new[] { "FontSize", "FontFamily", "FontWeight", "FontStyle", "Foreground" })
        {
            if (root.GetValue(name) is not { } v || XamlValueConverter.IsMarkupExtension(v)) continue;
            var ap = name switch
            {
                "FontSize" => (AvaloniaProperty)TextElement.FontSizeProperty,
                "FontFamily" => TextElement.FontFamilyProperty,
                "FontWeight" => TextElement.FontWeightProperty,
                "FontStyle" => TextElement.FontStyleProperty,
                _ => TextElement.ForegroundProperty,
            };
            if (ConvertValue(v, ap.PropertyType, out var value)) target.SetValue(ap, value);
        }
    }

    // ---------------------------------------------------------------- property elements

    private void ApplyPropertyElements(Control control, DesignElement element)
    {
        foreach (var prop in element.PropertyElements)
        {
            if (prop.Name.Contains('.')) continue; // attached (ToolTip.Tip ...) – not previewed
            try
            {
                ApplyPropertyElement(control, prop);
            }
            catch (Exception)
            {
                // ignore – best effort preview
            }
        }
    }

    private void ApplyPropertyElement(Control control, DesignProperty prop)
    {
        var clr = control.GetType().GetProperty(prop.Name, BindingFlags.Public | BindingFlags.Instance);
        if (clr is null) return;
        var current = clr.GetValue(control);

        if (current is IList list && !(current is string))
        {
            if (prop.Name is "Items" && current is ItemCollection items)
            {
                foreach (var v in prop.ElementValues)
                    if (BuildElement(v) is { } c) items.Add(c);
                return;
            }
            foreach (var v in prop.ElementValues)
            {
                if (CreateObject(v) is { } obj) list.Add(obj);
            }
            return;
        }

        // Single-valued: only controls are meaningful in the preview (Content, Child, Pane, Header...).
        if (prop.ElementValues.Count == 1 && clr.CanWrite && clr.PropertyType.IsAssignableFrom(typeof(Control))
            && prop.ElementValues[0].Descriptor?.ClrType is { } t && typeof(Control).IsAssignableFrom(t)
            && prop.ElementValues[0].Descriptor is { RenderAsPlaceholder: false })
        {
            if (BuildElement(prop.ElementValues[0]) is { } c) clr.SetValue(control, c);
        }
        else if (prop.ElementValues.Count == 0 && prop.ElementText is { } text && clr.CanWrite
                 && ConvertValue(text, clr.PropertyType, out var value))
        {
            clr.SetValue(control, value);
        }
    }

    /// <summary>Creates a non-control object (RowDefinition, DataGridTextColumn...) from an element.</summary>
    private object? CreateObject(DesignElement element)
    {
        var type = element.Descriptor?.ClrType ?? ResolveType(Bare(element.ElementType));
        if (type is null || type.GetConstructor(Type.EmptyTypes) is null) return null;
        if (typeof(Control).IsAssignableFrom(type)) return BuildElement(element);
        var obj = Activator.CreateInstance(type);
        if (obj is AvaloniaObject ao)
        {
            foreach (var p in element.Properties.Where(p => p.IsAttributeForm && p.StringValue is not null && !p.Name.Contains(':')))
                ApplyValue(ao, p.Name, p.StringValue);
        }
        else if (obj is not null)
        {
            foreach (var p in element.Properties.Where(p => p.IsAttributeForm && p.StringValue is not null))
            {
                var clr = type.GetProperty(p.Name);
                if (clr is { CanWrite: true } && ConvertValue(p.StringValue!, clr.PropertyType, out var v)) clr.SetValue(obj, v);
            }
        }
        return obj;
    }

    // ---------------------------------------------------------------- children

    private void AddChildren(Control control, DesignElement element)
    {
        if (element.Children.Count == 0) return;
        var built = element.Children.Select(BuildElement).Where(c => c is not null).Cast<Control>().ToList();
        if (built.Count == 0) return;

        switch (control)
        {
            case Panel panel:
                foreach (var c in built) panel.Children.Add(c);
                return;
            case ItemsControl itemsControl:
                foreach (var c in built) itemsControl.Items.Add(c);
                return;
            case ContentControl cc:
                cc.Content = built[0];
                return;
            case Decorator d:
                d.Child = built[0];
                return;
            case Viewbox vb:
                vb.Child = built[0];
                return;
            case Popup popup:
                popup.Child = built[0];
                return;
        }

        // Fall back to the [Content] property found by reflection.
        var contentProp = control.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.GetCustomAttribute<Avalonia.Metadata.ContentAttribute>() is not null);
        if (contentProp is null) return;
        var value = contentProp.GetValue(control);
        if (value is IList list)
        {
            foreach (var c in built) list.Add(c);
        }
        else if (contentProp.CanWrite)
        {
            contentProp.SetValue(control, built[0]);
        }
    }

    // ---------------------------------------------------------------- helpers

    private Control CreatePlaceholder(DesignElement element)
    {
        var label = element.Name is null ? element.ElementType : $"{element.ElementType} \"{element.Name}\"";
        var w = FirstNumber(element.GetValue("Width"));
        var h = FirstNumber(element.GetValue("Height"));
        return new Border
        {
            MinWidth = w is null ? 80 : 0,
            MinHeight = h is null ? 28 : 0,
            Background = new SolidColorBrush(Color.Parse("#30808080")),
            BorderBrush = new SolidColorBrush(Color.Parse("#90808080")),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = label,
                FontSize = 11,
                Opacity = 0.8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 2),
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
        };
    }

    private IBrush? ResolveBrush(string? text) =>
        text is not null && XamlValueConverter.TryDeserialize(text, typeof(IBrush), out var b) ? (IBrush?)b : null;

    private static double? FirstNumber(params string?[] values)
    {
        foreach (var v in values)
            if (v is not null && XamlValueConverter.TryParseDouble(v, out var d) && !double.IsNaN(d) && d > 0) return d;
        return null;
    }

    private static string Bare(string elementType) =>
        elementType.Contains(':') ? elementType[(elementType.IndexOf(':') + 1)..] : elementType;

    /// <summary>Resolves an Avalonia type by its short name ("Grid", "RowDefinition", "DataGridTextColumn").</summary>
    public static Type? ResolveType(string shortName)
    {
        lock (TypeCache)
        {
            if (TypeCache.TryGetValue(shortName, out var cached)) return cached;
            Type? found = null;
            foreach (var asm in TypeAssemblies)
            {
                found = asm.GetExportedTypes().FirstOrDefault(t => t.Name == shortName && t.Namespace?.StartsWith("Avalonia") == true);
                if (found is not null) break;
            }
            TypeCache[shortName] = found;
            return found;
        }
    }

    /// <summary>Loads a bitmap for Image.Source from a path relative to the document, if possible.</summary>
    public IImage? TryLoadImage(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || XamlValueConverter.IsMarkupExtension(source)) return null;
        try
        {
            var path = source.TrimStart('/');
            var candidates = new List<string>();
            if (System.IO.Path.IsPathRooted(source)) candidates.Add(source);
            if (BaseDirectory is not null)
            {
                candidates.Add(System.IO.Path.Combine(BaseDirectory, path));
                var parent = Directory.GetParent(BaseDirectory)?.FullName;
                if (parent is not null) candidates.Add(System.IO.Path.Combine(parent, path));
            }
            var file = candidates.FirstOrDefault(File.Exists);
            return file is null ? null : new Bitmap(file);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Post-build fix-ups that need the builder's context (images).</summary>
    public void ApplyImages()
    {
        foreach (var (element, control) in Map)
        {
            if (control is Image img && element.GetValue("Source") is { } src && TryLoadImage(src) is { } bmp)
                img.Source = bmp;
        }
    }
}
