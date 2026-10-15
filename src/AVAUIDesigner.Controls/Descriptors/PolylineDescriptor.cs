using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class PolylineDescriptor : StandardDescriptor
{
    public PolylineDescriptor() : base("Polyline", typeof(Avalonia.Controls.Shapes.Polyline), ToolboxCategories.Media)
    {
        Description = "A series of connected straight lines.";
        DefaultValues = new Dictionary<string, string> { ["Points"] = "0,30 30,0 60,30 90,0", ["Stroke"] = "Black" };
        Properties = Combine(
        [
            P("Points", typeof(string), C.Common, null, "Points, e.g. \"0,30 30,0 60,30 90,0\"."),
        ], Shape(fill: false, strokeThickness: 1), Control());
        Events = EventSets.Control().ToList();
    }
}
