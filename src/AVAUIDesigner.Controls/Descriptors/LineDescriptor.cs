using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class LineDescriptor : StandardDescriptor
{
    public LineDescriptor() : base("Line", typeof(Avalonia.Controls.Shapes.Line), ToolboxCategories.Media)
    {
        Description = "A straight line between two points.";
        DefaultValues = new Dictionary<string, string> { ["StartPoint"] = "0,0", ["EndPoint"] = "100,0", ["Stroke"] = "Black", ["StrokeThickness"] = "1" };
        // Avalonia 11 uses StartPoint/EndPoint (Point) instead of WPF's X1/Y1/X2/Y2.
        Properties = Combine(
        [
            P("StartPoint", typeof(Point), C.Common, default(Point), "Start of the line, e.g. \"0,0\"."),
            P("EndPoint", typeof(Point), C.Common, default(Point), "End of the line, e.g. \"100,0\"."),
        ], Shape(fill: false, strokeThickness: 1), Control());
        Events = EventSets.Control().ToList();
    }
}
