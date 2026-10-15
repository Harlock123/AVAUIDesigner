using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using AVAUIDesigner.Engine.Descriptors;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Common;

/// <summary>Reusable property descriptor sets shared by the standard control descriptors.</summary>
public static class PropertySets
{
    public static PropertyDescriptor P(string name, Type type, string category, object? defaultValue = null, string? description = null) =>
        new(name, type, category, defaultValue, description);

    /// <summary>Layout + common properties every Control has.</summary>
    public static IEnumerable<PropertyDescriptor> Control()
    {
        yield return P("Width", typeof(double), C.Layout, double.NaN, "The requested width of the element.");
        yield return P("Height", typeof(double), C.Layout, double.NaN, "The requested height of the element.");
        yield return P("MinWidth", typeof(double), C.Layout, 0d, "The minimum width of the element.");
        yield return P("MinHeight", typeof(double), C.Layout, 0d, "The minimum height of the element.");
        yield return P("MaxWidth", typeof(double), C.Layout, double.PositiveInfinity, "The maximum width of the element.");
        yield return P("MaxHeight", typeof(double), C.Layout, double.PositiveInfinity, "The maximum height of the element.");
        yield return P("Margin", typeof(Thickness), C.Layout, default(Thickness), "The outer margin of the element.");
        yield return P("HorizontalAlignment", typeof(HorizontalAlignment), C.Layout, HorizontalAlignment.Stretch,
            "How the element is aligned horizontally within its parent.");
        yield return P("VerticalAlignment", typeof(VerticalAlignment), C.Layout, VerticalAlignment.Stretch,
            "How the element is aligned vertically within its parent.");
        yield return P("IsEnabled", typeof(bool), C.Common, true, "Whether the element accepts user input.");
        yield return P("IsVisible", typeof(bool), C.Appearance, true, "Whether the element is visible.");
        yield return P("Opacity", typeof(double), C.Appearance, 1d, "The opacity of the element (0–1).");
        yield return P("FlowDirection", typeof(FlowDirection), C.Layout, FlowDirection.LeftToRight, "Text and layout direction.");
        yield return P("Classes", typeof(string), C.Appearance, null, "Style classes, separated by spaces.");
        yield return P("Tag", typeof(string), C.Misc, null, "Arbitrary user data.");
        yield return P("TabIndex", typeof(int), C.Behavior, null, "Keyboard navigation order.");
        yield return P("IsTabStop", typeof(bool), C.Behavior, true, "Whether the element is included in tab navigation.");
        yield return P("Focusable", typeof(bool), C.Behavior, null, "Whether the element can receive keyboard focus.");
        yield return WithValues(P("Cursor", typeof(string), C.Appearance, null, "Mouse cursor shown over the element (e.g. Hand, Ibeam)."),
            "Arrow", "Ibeam", "Wait", "Cross", "UpArrow", "SizeWestEast", "SizeNorthSouth", "SizeAll", "No", "Hand", "Help");
    }

    /// <summary>Brush/border/font properties of TemplatedControl.</summary>
    public static IEnumerable<PropertyDescriptor> Templated()
    {
        yield return P("Background", typeof(IBrush), C.Brush, null, "The background brush.");
        yield return P("Foreground", typeof(IBrush), C.Brush, null, "The foreground (text) brush.");
        yield return P("BorderBrush", typeof(IBrush), C.Brush, null, "The border brush.");
        yield return P("BorderThickness", typeof(Thickness), C.Appearance, null, "The border thickness.");
        yield return P("CornerRadius", typeof(CornerRadius), C.Appearance, null, "The corner radius.");
        yield return P("Padding", typeof(Thickness), C.Layout, null, "The inner padding.");
        foreach (var p in Font()) yield return p;
    }

    public static IEnumerable<PropertyDescriptor> Font()
    {
        yield return P("FontFamily", typeof(FontFamily), C.Text, null, "The font family.");
        yield return P("FontSize", typeof(double), C.Text, null, "The font size in device-independent pixels.");
        yield return P("FontWeight", typeof(FontWeight), C.Text, FontWeight.Normal, "The font weight.");
        yield return P("FontStyle", typeof(FontStyle), C.Text, FontStyle.Normal, "The font style.");
    }

    /// <summary>ContentControl: Content + content alignment.</summary>
    public static IEnumerable<PropertyDescriptor> Content(string description = "The content of the control.")
    {
        yield return P("Content", typeof(string), C.Common, null, description);
        yield return P("HorizontalContentAlignment", typeof(HorizontalAlignment), C.Layout, null, "Horizontal alignment of the content.");
        yield return P("VerticalContentAlignment", typeof(VerticalAlignment), C.Layout, null, "Vertical alignment of the content.");
    }

    /// <summary>Command + CommandParameter (Button, MenuItem...).</summary>
    public static IEnumerable<PropertyDescriptor> Command()
    {
        yield return P("Command", typeof(string), C.Common, null, "The command executed when activated (usually a {Binding}).");
        yield return P("CommandParameter", typeof(string), C.Common, null, "The parameter passed to the command.");
    }

    /// <summary>ItemsControl data properties.</summary>
    public static IEnumerable<PropertyDescriptor> Items(string itemElement, string itemProperty = "Content")
    {
        yield return new PropertyDescriptor("Items", typeof(string), C.Common, null, "The items, one per line.")
        {
            CollectionItemElement = itemElement,
            CollectionItemProperties = [itemProperty],
        };
        yield return P("ItemsSource", typeof(string), C.Data, null, "The collection used to generate items (usually a {Binding}).");
        yield return new PropertyDescriptor("ItemTemplate", typeof(object), C.Data, null, "The template used to display each item.")
        {
            IsXamlFragment = true,
        };
    }

    /// <summary>Selecting items control properties (SelectedIndex, SelectedItem, SelectionMode).</summary>
    public static IEnumerable<PropertyDescriptor> Selection(bool includeMode = true)
    {
        yield return P("SelectedIndex", typeof(int), C.Common, -1, "Index of the selected item (-1 for none).");
        yield return P("SelectedItem", typeof(string), C.Common, null, "The selected item (usually a {Binding}).");
        if (includeMode) yield return P("SelectionMode", typeof(SelectionMode), C.Behavior, SelectionMode.Single, "Single or multiple selection.");
    }

    /// <summary>RangeBase: Value, Minimum, Maximum.</summary>
    public static IEnumerable<PropertyDescriptor> Range(double max = 100)
    {
        yield return P("Value", typeof(double), C.Common, 0d, "The current value.");
        yield return P("Minimum", typeof(double), C.Common, 0d, "The minimum value.");
        yield return P("Maximum", typeof(double), C.Common, max, "The maximum value.");
    }

    /// <summary>Shape: Fill, Stroke, StrokeThickness, Stretch.</summary>
    public static IEnumerable<PropertyDescriptor> Shape(bool fill = true, double strokeThickness = 0)
    {
        if (fill) yield return P("Fill", typeof(IBrush), C.Brush, null, "The brush that fills the shape.");
        yield return P("Stroke", typeof(IBrush), C.Brush, null, "The brush of the outline.");
        yield return P("StrokeThickness", typeof(double), C.Appearance, strokeThickness, "The outline width.");
        yield return P("Stretch", typeof(Stretch), C.Layout, Stretch.None, "How the shape fills its space.");
        yield return P("StrokeDashArray", typeof(string), C.Appearance, null, "Dash pattern of the outline, e.g. \"4,2\".");
        yield return P("StrokeLineCap", typeof(PenLineCap), C.Appearance, PenLineCap.Flat, "Shape at the ends of open lines.");
        yield return P("StrokeJoin", typeof(PenLineJoin), C.Appearance, PenLineJoin.Miter, "Shape of the outline corners.");
    }

    /// <summary>Text properties of TextBlock/TextBox.</summary>
    public static IEnumerable<PropertyDescriptor> TextLayout()
    {
        yield return P("TextWrapping", typeof(TextWrapping), C.Text, TextWrapping.NoWrap, "How text wraps.");
        yield return P("TextAlignment", typeof(TextAlignment), C.Text, TextAlignment.Start, "Horizontal alignment of text.");
    }

    /// <summary>HeaderedContentControl / HeaderedItemsControl: Header.</summary>
    public static IEnumerable<PropertyDescriptor> Header(string description = "The header content.")
    {
        yield return P("Header", typeof(string), C.Common, null, description);
    }

    /// <summary>Concatenates sets, keeping the first descriptor of each name.</summary>
    public static IReadOnlyList<PropertyDescriptor> Combine(params IEnumerable<PropertyDescriptor>[] sets)
    {
        var result = new List<PropertyDescriptor>();
        var seen = new HashSet<string>();
        foreach (var set in sets)
        foreach (var p in set)
            if (seen.Add(p.Name)) result.Add(p);
        return result;
    }

    /// <summary>Like <see cref="Combine"/> but later sets override earlier descriptors of the same name.</summary>
    public static IReadOnlyList<PropertyDescriptor> Override(IEnumerable<PropertyDescriptor> baseSet, params PropertyDescriptor[] overrides)
    {
        var list = baseSet.ToList();
        foreach (var o in overrides)
        {
            var i = list.FindIndex(p => p.Name == o.Name);
            if (i >= 0) list[i] = o;
            else list.Add(o);
        }
        return list;
    }

    public static PropertyDescriptor WithValues(PropertyDescriptor p, params string[] values) =>
        new(p.Name, p.PropertyType, p.Category, p.DefaultValue, p.Description) { StandardValues = values };
}
