using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class CanvasDescriptor : StandardDescriptor
{
    public CanvasDescriptor() : base("Canvas", typeof(Canvas), ToolboxCategories.Containers)
    {
        Description = "Positions children at absolute coordinates.";
        LayoutMode = ContainerLayoutMode.Canvas;
        ContentProperty = "Children";
        DefaultSize = new Size(200, 150);
        ChildAttachedProperties = AttachedPropertySets.Canvas;
        Properties = Combine(
        [
            P("Background", typeof(IBrush), C.Brush, null, "The background brush."),
        ], Control());
        Events = EventSets.Control().ToList();
    }
}
