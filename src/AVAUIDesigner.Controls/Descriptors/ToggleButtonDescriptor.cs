using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class ToggleButtonDescriptor : StandardDescriptor
{
    public ToggleButtonDescriptor() : base("ToggleButton", typeof(ToggleButton), ToolboxCategories.Input)
    {
        Description = "A button that stays pressed until clicked again.";
        DefaultSize = new Size(75, 23);
        ContentProperty = "Content";
        DefaultValues = new Dictionary<string, string> { ["Content"] = "ToggleButton" };
        Properties = Combine(
            Content("The button caption or content."),
            [
                P("IsChecked", typeof(bool?), C.Common, false, "Whether the button is toggled on."),
                P("IsThreeState", typeof(bool), C.Behavior, false, "Whether the indeterminate state can be selected."),
                P("ClickMode", typeof(ClickMode), C.Behavior, ClickMode.Release, "When the Click event is raised."),
            ],
            Command(), Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("IsCheckedChanged", description: "Occurs when IsChecked changes."),
            EventSets.E("Checked", description: "Occurs when the button is toggled on."),
            EventSets.E("Unchecked", description: "Occurs when the button is toggled off."),
            EventSets.E("Click", description: "Occurs when the button is clicked."),
        ], EventSets.Control());
        DefaultEvent = "IsCheckedChanged";
    }
}
