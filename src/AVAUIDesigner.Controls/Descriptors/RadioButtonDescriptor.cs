using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class RadioButtonDescriptor : StandardDescriptor
{
    public RadioButtonDescriptor() : base("RadioButton", typeof(RadioButton), ToolboxCategories.Common)
    {
        Description = "One option of a mutually exclusive group.";
        DefaultSize = new Size(120, 20);
        ContentProperty = "Content";
        DefaultValues = new Dictionary<string, string> { ["Content"] = "RadioButton" };
        Properties = Combine(
            Content("The caption shown next to the button."),
            [
                P("IsChecked", typeof(bool?), C.Common, false, "Whether this option is selected."),
                P("GroupName", typeof(string), C.Behavior, null, "Radio buttons with the same group name are mutually exclusive."),
                P("IsThreeState", typeof(bool), C.Behavior, false, "Whether the indeterminate state can be selected."),
            ],
            Command(), Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("IsCheckedChanged", description: "Occurs when IsChecked changes."),
            EventSets.E("Checked", description: "Occurs when the button becomes checked."),
            EventSets.E("Unchecked", description: "Occurs when the button becomes unchecked."),
            EventSets.E("Click", description: "Occurs when the button is clicked."),
        ], EventSets.Control());
        DefaultEvent = "IsCheckedChanged";
    }
}
