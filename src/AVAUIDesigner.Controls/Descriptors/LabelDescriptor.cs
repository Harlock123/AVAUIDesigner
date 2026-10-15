using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class LabelDescriptor : StandardDescriptor
{
    public LabelDescriptor() : base("Label", typeof(Label), ToolboxCategories.Common)
    {
        Description = "A caption that can move focus to a target control via its access key.";
        DefaultSize = new Size(120, 20);
        ContentProperty = "Content";
        DefaultValues = new Dictionary<string, string> { ["Content"] = "Label" };
        Properties = Combine(
            Content("The label text or content (use _ for an access key)."),
            [
                P("IsTabStop", typeof(bool), C.Behavior, false, "Whether the element is included in tab navigation."),
                P("Target", typeof(string), C.Behavior, null, "The control focused by the access key, e.g. {Binding ElementName=NameBox}."),
            ],
            Control(), Templated());
        Events = EventSets.Control().ToList();
    }
}
