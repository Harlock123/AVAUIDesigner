using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class PathDescriptor : StandardDescriptor
{
    public PathDescriptor() : base("Path", typeof(Avalonia.Controls.Shapes.Path), ToolboxCategories.Media)
    {
        Description = "A shape drawn from path geometry markup.";
        DefaultValues = new Dictionary<string, string> { ["Data"] = "M0,0 L50,50 L100,0", ["Stroke"] = "Black", ["StrokeThickness"] = "1" };
        Properties = Combine(
        [
            P("Data", typeof(string), C.Common, null, "Path geometry markup, e.g. \"M0,0 L50,50 L100,0\"."),
        ], Shape(strokeThickness: 0), Control());
        Events = EventSets.Control().ToList();
    }
}
