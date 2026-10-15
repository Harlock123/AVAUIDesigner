using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class ViewboxDescriptor : StandardDescriptor
{
    public ViewboxDescriptor() : base("Viewbox", typeof(Viewbox), ToolboxCategories.Layout)
    {
        Description = "Scales a single child to fill the available space.";
        LayoutMode = ContainerLayoutMode.Single;
        ContentProperty = "Child";
        MaxChildren = 1;
        DefaultSize = new Size(200, 150);
        Properties = Combine(
        [
            P("Stretch", typeof(Stretch), C.Layout, Stretch.Uniform, "How the child is scaled."),
            P("StretchDirection", typeof(StretchDirection), C.Layout, StretchDirection.Both, "Whether the child may be scaled up, down or both."),
        ], Control());
        Events = EventSets.Control().ToList();
    }
}
