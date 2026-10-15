using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class ImageDescriptor : StandardDescriptor
{
    public ImageDescriptor() : base("Image", typeof(Image), ToolboxCategories.Common)
    {
        Description = "Displays a bitmap or vector image.";
        DefaultSize = new Size(100, 100);
        Properties = Combine(
        [
            P("Source", typeof(string), C.Common, null, "Path or URI of the image (e.g. /Assets/logo.png)."),
            P("Stretch", typeof(Stretch), C.Layout, Stretch.Uniform, "How the image fills its space."),
            P("StretchDirection", typeof(StretchDirection), C.Layout, StretchDirection.Both, "Whether the image may be scaled up, down or both."),
        ], Control());
        Events = EventSets.Control().ToList();
    }
}
