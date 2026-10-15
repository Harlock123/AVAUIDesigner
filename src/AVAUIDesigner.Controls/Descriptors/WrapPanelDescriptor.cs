using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class WrapPanelDescriptor : StandardDescriptor
{
    public WrapPanelDescriptor() : base("WrapPanel", typeof(WrapPanel), ToolboxCategories.Containers)
    {
        Description = "Arranges children in lines, wrapping to the next line when out of space.";
        LayoutMode = ContainerLayoutMode.Wrap;
        ContentProperty = "Children";
        DefaultSize = new Size(200, 150);
        Properties = Combine(
        [
            P("Orientation", typeof(Orientation), C.Layout, Orientation.Horizontal, "The direction children flow in."),
            P("ItemWidth", typeof(double), C.Layout, double.NaN, "Width applied to every child (NaN = own size)."),
            P("ItemHeight", typeof(double), C.Layout, double.NaN, "Height applied to every child (NaN = own size)."),
            P("ItemSpacing", typeof(double), C.Layout, 0d, "Space between children on the same line."),
            P("LineSpacing", typeof(double), C.Layout, 0d, "Space between lines."),
            P("ItemsAlignment", typeof(WrapPanelItemsAlignment), C.Layout, WrapPanelItemsAlignment.Start, "How children are aligned within each line."),
            P("Background", typeof(IBrush), C.Brush, null, "The background brush."),
        ], Control());
        Events = EventSets.Control().ToList();
    }
}
