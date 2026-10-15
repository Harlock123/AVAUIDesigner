using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class ButtonDescriptor : StandardDescriptor
{
    public ButtonDescriptor() : base("Button", typeof(Button), ToolboxCategories.Common)
    {
        Description = "A clickable button.";
        DefaultSize = new Size(75, 23);
        ContentProperty = "Content";
        DefaultValues = new Dictionary<string, string> { ["Content"] = "Button" };
        Properties = Combine(
            Content("The button caption or content."),
            [
                P("IsDefault", typeof(bool), C.Behavior, false, "Clicked when the user presses Enter."),
                P("IsCancel", typeof(bool), C.Behavior, false, "Clicked when the user presses Escape."),
                P("ClickMode", typeof(ClickMode), C.Behavior, ClickMode.Release, "When the Click event is raised."),
            ],
            Command(), Control(), Templated());
        Events = EventSets.Combine([EventSets.E("Click", description: "Occurs when the button is clicked.")], EventSets.Control());
        DefaultEvent = "Click";
    }
}
