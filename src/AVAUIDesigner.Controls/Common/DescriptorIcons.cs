using Avalonia;
using Avalonia.Media;
using AVAUIDesigner.Engine.Descriptors;

namespace AVAUIDesigner.Controls.Common;

/// <summary>
/// Vector toolbox icons drawn on a 16×16 grid. Each icon is an outline geometry (stroked) plus an
/// optional accent geometry (filled), in colors readable on both light and dark themes.
/// </summary>
public static class DescriptorIcons
{
    private static readonly Color Outline = Color.Parse("#7A8699");
    private static readonly Color Accent = Color.Parse("#3B82F6");

    private static readonly Dictionary<string, (string Outline, string? Accent)> Glyphs = new()
    {
        ["Button"] = ("M1.5,4.5 h13 v7 h-13 z", "M5,7.5 h6 v1 h-6 z"),
        ["ToggleButton"] = ("M1.5,4.5 h13 v7 h-13 z", "M3,6 h5 v4 h-5 z"),
        ["TextBox"] = ("M1.5,4.5 h13 v7 h-13 z", "M4,6 v4 M3,6 h2 M3,10 h2"),
        ["TextBlock"] = ("M3,3.5 h10 M8,3.5 v9", "M5,12.5 h6 v0.8 h-6 z"),
        ["Label"] = ("M2,4.5 h8 l3.5,3.5 l-3.5,3.5 h-8 z", "M4,7.5 h4 v1 h-4 z"),
        ["CheckBox"] = ("M2.5,2.5 h11 v11 h-11 z", "M4.5,8 l2.5,2.5 l4.5,-5 l-0.9,-0.8 l-3.6,4 l-1.6,-1.6 z"),
        ["RadioButton"] = ("M8,2 a6,6 0 1 0 0.01,0 z", "M8,5.5 a2.5,2.5 0 1 0 0.01,0 z"),
        ["ComboBox"] = ("M1.5,4.5 h13 v7 h-13 z M10.5,4.5 v7", "M11.5,7 h2 l-1,1.5 z"),
        ["ListBox"] = ("M2.5,1.5 h11 v13 h-11 z", "M4,4 h8 v1.5 h-8 z M4,7 h8 v1.5 h-8 z M4,10 h5 v1.5 h-5 z"),
        ["Image"] = ("M1.5,2.5 h13 v11 h-13 z", "M3,12 l3.5,-4.5 l2.5,3 l1.5,-1.5 l2.5,3 z M10.5,5 a1.2,1.2 0 1 0 0.01,0 z"),
        ["ProgressBar"] = ("M1.5,6.5 h13 v3 h-13 z", "M2.5,7.5 h7 v1 h-7 z"),
        ["Slider"] = ("M1.5,8 h13", "M6,4.5 h3 v7 h-3 z"),
        ["ToggleSwitch"] = ("M5,4.5 h6 a3.5,3.5 0 0 1 0,7 h-6 a3.5,3.5 0 0 1 0,-7 z", "M11,6 a2,2 0 1 0 0.01,0 z"),
        ["Grid"] = ("M1.5,1.5 h13 v13 h-13 z M1.5,6 h13 M1.5,10.5 h13 M6,1.5 v13 M10.5,1.5 v13", null),
        ["UniformGrid"] = ("M1.5,1.5 h13 v13 h-13 z M1.5,8 h13 M8,1.5 v13", "M3,3 h3.5 v3.5 h-3.5 z"),
        ["StackPanel"] = ("M2.5,1.5 h11 v13 h-11 z", "M4,3 h8 v2.5 h-8 z M4,6.75 h8 v2.5 h-8 z M4,10.5 h8 v2.5 h-8 z"),
        ["DockPanel"] = ("M1.5,1.5 h13 v13 h-13 z M1.5,5 h13 M5,5 v9.5", "M2.5,2.5 h11 v1.5 h-11 z"),
        ["WrapPanel"] = ("M1.5,1.5 h13 v13 h-13 z", "M3,3 h4 v3 h-4 z M8.5,3 h4.5 v3 h-4.5 z M3,7.5 h6 v3 h-6 z"),
        ["Canvas"] = ("M1.5,1.5 h13 v13 h-13 z", "M3.5,3.5 h4 v3 h-4 z M8,9 h4.5 v3.5 h-4.5 z"),
        ["Border"] = ("M3.5,1.5 h9 a2,2 0 0 1 2,2 v9 a2,2 0 0 1 -2,2 h-9 a2,2 0 0 1 -2,-2 v-9 a2,2 0 0 1 2,-2 z", "M4.5,4.5 h7 v7 h-7 z"),
        ["ScrollViewer"] = ("M1.5,1.5 h13 v13 h-13 z M11.5,1.5 v13", "M12.5,4 h1 v5 h-1 z"),
        ["Expander"] = ("M1.5,1.5 h13 v13 h-13 z M1.5,5.5 h13", "M3,2.75 l2,1.5 l-2,1.5 z"),
        ["TabControl"] = ("M1.5,4.5 h13 v10 h-13 z M1.5,4.5 v-3 h5 v3", "M7,2.5 h4 v2 h-4 z"),
        ["TabItem"] = ("M1.5,6 v-4.5 h7 v4.5 h6 v8.5 h-13 z", null),
        ["SplitView"] = ("M1.5,1.5 h13 v13 h-13 z M6,1.5 v13", "M2.5,2.5 h3 v11 h-3 z"),
        ["DataGrid"] = ("M1.5,1.5 h13 v13 h-13 z M1.5,5 h13 M1.5,8.5 h13 M1.5,12 h13 M6,1.5 v13 M10.5,1.5 v13", "M1.5,1.5 h13 v3.5 h-13 z"),
        ["TreeView"] = ("M3,2 v10.5 h3 M3,7 h3", "M7,5.5 h6 v3 h-6 z M7,11 h6 v3 h-6 z M1.5,0.5 h3 v3 h-3 z"),
        ["ItemsControl"] = ("M2.5,1.5 h11 v13 h-11 z", "M4,4 h3 v3 h-3 z M9,4 h3 v3 h-3 z M4,9 h3 v3 h-3 z"),
        ["Menu"] = ("M0.5,2.5 h15 v4 h-15 z", "M2,4 h3 v1 h-3 z M6.5,4 h3 v1 h-3 z M11,4 h3 v1 h-3 z"),
        ["MenuItem"] = ("M2.5,2.5 h11 v11 h-11 z", "M4,5 h8 v1 h-8 z M4,7.5 h8 v1 h-8 z M4,10 h5 v1 h-5 z"),
        ["ContextMenu"] = ("M4.5,3.5 h10 v11 h-10 z", "M1,1 l3,6 l1,-2.2 l2.2,-1 z"),
        ["ToolTip"] = ("M1.5,2.5 h13 v8 h-7 l-3,3 v-3 h-3 z", "M4,5 h8 v1 h-8 z M4,7.5 h5 v1 h-5 z"),
        ["Separator"] = ("M1,8 h14", null),
        ["Popup"] = ("M3.5,3.5 h11 v11 h-11 z M1.5,1.5 h11 v2", "M5,6 h8 v1 h-8 z"),
        ["Flyout"] = ("M1.5,1.5 h6 v4 h-6 z M4,5.5 v3 h3", "M7,8.5 h7.5 v6 h-7.5 z"),
        ["NumericUpDown"] = ("M1.5,4.5 h13 v7 h-13 z M10.5,4.5 v7 M10.5,8 h4", "M11.5,7 l1,-1.5 l1,1.5 z M11.5,9 l1,1.5 l1,-1.5 z"),
        ["DatePicker"] = ("M1.5,3.5 h13 v11 h-13 z M1.5,6.5 h13 M5,1.5 v3 M11,1.5 v3", "M4,8.5 h2 v2 h-2 z M7,8.5 h2 v2 h-2 z M10,8.5 h2 v2 h-2 z"),
        ["CalendarDatePicker"] = ("M1.5,3.5 h13 v11 h-13 z M1.5,6.5 h13 M5,1.5 v3 M11,1.5 v3", "M7,8.5 h2 v2 h-2 z"),
        ["TimePicker"] = ("M8,1.5 a6.5,6.5 0 1 0 0.01,0 z", "M7.5,4 h1 v4.3 l2.8,1.6 l-0.5,0.9 l-3.3,-1.9 z"),
        ["AutoCompleteBox"] = ("M1.5,2.5 h13 v5 h-13 z M1.5,9 h13 v5.5 h-13 z", "M3,4.5 h6 v1 h-6 z M3,11 h8 v1.5 h-8 z"),
        ["GridSplitter"] = ("M1.5,1.5 h13 v13 h-13 z", "M7,1.5 h2 v13 h-2 z"),
        ["Viewbox"] = ("M1.5,1.5 h13 v13 h-13 z M1.5,1.5 l13,13 M14.5,1.5 l-13,13", null),
        ["Rectangle"] = ("M2.5,3.5 h11 v9 h-11 z", "M4,5 h8 v6 h-8 z"),
        ["Ellipse"] = ("M8,3 a6,5 0 1 0 0.01,0 z", "M8,5 a4,3 0 1 0 0.01,0 z"),
        ["Line"] = ("M2,13 L14,3", null),
        ["Path"] = ("M2,12 C4,2 8,14 14,4", null),
        ["Polygon"] = ("M8,1.5 L14.5,6.5 L12,14.5 L4,14.5 L1.5,6.5 z", "M8,4 L12,7 L10.5,12 L5.5,12 L4,7 z"),
        ["Polyline"] = ("M1.5,12 L5,5 L9,10 L14.5,3", null),
        ["Window"] = ("M1.5,2.5 h13 v11 h-13 z M1.5,5.5 h13", "M11,3.5 h1 v1 h-1 z M12.8,3.5 h1 v1 h-1 z"),
        ["UserControl"] = ("M1.5,1.5 h13 v13 h-13 z", "M8,4 a2,2 0 1 0 0.01,0 z M4.5,12.5 a3.5,3 0 0 1 7,0 z"),
        ["Unknown"] = ("M1.5,1.5 h13 v13 h-13 z", "M6,5.5 a2,2 0 1 1 3,1.7 c-0.6,0.4 -1,0.8 -1,1.5 v0.8 M8,11.5 v1"),
    };

    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["ListBoxItem"] = "ListBox", ["ComboBoxItem"] = "ComboBox", ["TreeViewItem"] = "TreeView",
        ["ContentControl"] = "Border", ["Panel"] = "Canvas", ["RelativePanel"] = "Canvas",
    };

    public static IImage? Create(ControlDescriptor descriptor) => Create(descriptor.ElementName);

    public static IImage? Create(string elementName)
    {
        if (!Glyphs.TryGetValue(elementName, out var glyph) &&
            !(Aliases.TryGetValue(elementName, out var alias) && Glyphs.TryGetValue(alias, out glyph)))
            glyph = Glyphs["Unknown"];

        try
        {
            var group = new DrawingGroup();
            // Transparent 16×16 backdrop keeps every icon the same size.
            group.Children.Add(new GeometryDrawing
            {
                Brush = Brushes.Transparent,
                Geometry = new RectangleGeometry(new Rect(0, 0, 16, 16)),
            });
            group.Children.Add(new GeometryDrawing
            {
                Geometry = Geometry.Parse(glyph.Outline),
                Pen = new Pen(new SolidColorBrush(Outline), 1.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round),
            });
            if (glyph.Accent is { } accent)
            {
                group.Children.Add(new GeometryDrawing
                {
                    Geometry = Geometry.Parse(accent),
                    Brush = new SolidColorBrush(Accent),
                    Pen = new Pen(new SolidColorBrush(Accent), 0.8, lineCap: PenLineCap.Round),
                });
            }
            return new DrawingImage(group);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException or FormatException)
        {
            // No platform render interface (e.g. unit tests): run without icons.
            return null;
        }
    }
}
