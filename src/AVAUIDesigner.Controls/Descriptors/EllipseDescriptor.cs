using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class EllipseDescriptor : StandardDescriptor
{
    public EllipseDescriptor() : base("Ellipse", typeof(Avalonia.Controls.Shapes.Ellipse), ToolboxCategories.Media)
    {
        Description = "An ellipse or circle.";
        DefaultSize = new Size(80, 80);
        DefaultValues = new Dictionary<string, string> { ["Fill"] = "LightSteelBlue", ["Stroke"] = "SteelBlue", ["StrokeThickness"] = "1" };
        Properties = Combine(Shape(strokeThickness: 0), Control());
        Events = EventSets.Control().ToList();
    }
}
