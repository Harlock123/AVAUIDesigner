using Avalonia.Controls;
using AVAUIDesigner.Engine.Descriptors;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Common;

/// <summary>Attached properties contributed by containers to their children.</summary>
public static class AttachedPropertySets
{
    private static PropertyDescriptor A(string owner, string name, Type type, object? def, string description) =>
        new(name, type, C.Attached, def, description) { AttachedPropertyOwner = owner };

    public static IReadOnlyList<PropertyDescriptor> Grid { get; } =
    [
        A("Grid", "Row", typeof(int), 0, "The row the element occupies."),
        A("Grid", "Column", typeof(int), 0, "The column the element occupies."),
        A("Grid", "RowSpan", typeof(int), 1, "How many rows the element spans."),
        A("Grid", "ColumnSpan", typeof(int), 1, "How many columns the element spans."),
    ];

    public static IReadOnlyList<PropertyDescriptor> Canvas { get; } =
    [
        A("Canvas", "Left", typeof(double), double.NaN, "Distance from the canvas' left edge."),
        A("Canvas", "Top", typeof(double), double.NaN, "Distance from the canvas' top edge."),
        A("Canvas", "Right", typeof(double), double.NaN, "Distance from the canvas' right edge."),
        A("Canvas", "Bottom", typeof(double), double.NaN, "Distance from the canvas' bottom edge."),
    ];

    public static IReadOnlyList<PropertyDescriptor> DockPanel { get; } =
    [
        A("DockPanel", "Dock", typeof(Dock), Dock.Left, "The edge of the DockPanel the element docks to."),
    ];

    /// <summary>Available on every visual control.</summary>
    public static IReadOnlyList<PropertyDescriptor> Global { get; } =
    [
        A("ToolTip", "Tip", typeof(string), null, "Tooltip text shown when hovering the control."),
        A("ToolTip", "Placement", typeof(PlacementMode), PlacementMode.Pointer, "Where the tooltip appears."),
        A("ToolTip", "ShowDelay", typeof(int), 400, "Delay in milliseconds before the tooltip shows."),
    ];
}
