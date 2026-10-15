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
public sealed class StackPanelDescriptor : StandardDescriptor
{
    public StackPanelDescriptor() : base("StackPanel", typeof(StackPanel), ToolboxCategories.Containers)
    {
        Description = "Arranges children in a single horizontal or vertical line.";
        LayoutMode = ContainerLayoutMode.Stack;
        ContentProperty = "Children";
        DefaultSize = new Size(200, 150);
        Properties = Combine(
        [
            P("Orientation", typeof(Orientation), C.Layout, Orientation.Vertical, "The direction children are stacked in."),
            P("Spacing", typeof(double), C.Layout, 0d, "Space between adjacent children."),
            P("Background", typeof(IBrush), C.Brush, null, "The background brush."),
        ], Control());
        Events = EventSets.Control().ToList();
    }
}
