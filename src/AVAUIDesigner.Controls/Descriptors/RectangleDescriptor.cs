using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class RectangleDescriptor : StandardDescriptor
{
    public RectangleDescriptor() : base("Rectangle", typeof(Avalonia.Controls.Shapes.Rectangle), ToolboxCategories.Media)
    {
        Description = "A rectangle, optionally with rounded corners.";
        DefaultSize = new Size(100, 60);
        DefaultValues = new Dictionary<string, string> { ["Fill"] = "LightSteelBlue", ["Stroke"] = "SteelBlue", ["StrokeThickness"] = "1" };
        Properties = Combine(
        [
            P("RadiusX", typeof(double), C.Appearance, 0d, "Horizontal corner radius."),
            P("RadiusY", typeof(double), C.Appearance, 0d, "Vertical corner radius."),
        ], Shape(strokeThickness: 0), Control());
        Events = EventSets.Control().ToList();
    }
}
