using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class PolygonDescriptor : StandardDescriptor
{
    public PolygonDescriptor() : base("Polygon", typeof(Avalonia.Controls.Shapes.Polygon), ToolboxCategories.Media)
    {
        Description = "A closed shape through a list of points.";
        DefaultValues = new Dictionary<string, string> { ["Points"] = "0,40 40,0 80,40", ["Fill"] = "LightSteelBlue", ["Stroke"] = "SteelBlue", ["StrokeThickness"] = "1" };
        Properties = Combine(
        [
            P("Points", typeof(string), C.Common, null, "Vertices, e.g. \"0,40 40,0 80,40\"."),
            P("FillRule", typeof(FillRule), C.Appearance, FillRule.EvenOdd, "How the interior is determined."),
        ], Shape(strokeThickness: 0), Control());
        Events = EventSets.Control().ToList();
    }
}
