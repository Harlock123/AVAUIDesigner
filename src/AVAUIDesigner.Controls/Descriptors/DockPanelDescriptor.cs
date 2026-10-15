using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class DockPanelDescriptor : StandardDescriptor
{
    public DockPanelDescriptor() : base("DockPanel", typeof(DockPanel), ToolboxCategories.Containers)
    {
        Description = "Docks children to its edges; the last child can fill the remaining space.";
        LayoutMode = ContainerLayoutMode.Dock;
        ContentProperty = "Children";
        DefaultSize = new Size(200, 150);
        ChildAttachedProperties = AttachedPropertySets.DockPanel;
        Properties = Combine(
        [
            P("LastChildFill", typeof(bool), C.Layout, true, "Whether the last child fills the remaining space."),
            P("HorizontalSpacing", typeof(double), C.Layout, 0d, "Horizontal space between docked children."),
            P("VerticalSpacing", typeof(double), C.Layout, 0d, "Vertical space between docked children."),
            P("Background", typeof(IBrush), C.Brush, null, "The background brush."),
        ], Control());
        Events = EventSets.Control().ToList();
    }
}
