using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class CheckBoxDescriptor : StandardDescriptor
{
    public CheckBoxDescriptor() : base("CheckBox", typeof(CheckBox), ToolboxCategories.Common)
    {
        Description = "A box the user can check or uncheck.";
        DefaultSize = new Size(120, 20);
        ContentProperty = "Content";
        DefaultValues = new Dictionary<string, string> { ["Content"] = "CheckBox" };
        Properties = Combine(
            Content("The caption shown next to the box."),
            [
                P("IsChecked", typeof(bool?), C.Common, false, "Checked (True), unchecked (False) or indeterminate (null)."),
                P("IsThreeState", typeof(bool), C.Behavior, false, "Whether the indeterminate state can be selected."),
            ],
            Command(), Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("IsCheckedChanged", description: "Occurs when IsChecked changes."),
            EventSets.E("Checked", description: "Occurs when the box becomes checked."),
            EventSets.E("Unchecked", description: "Occurs when the box becomes unchecked."),
            EventSets.E("Indeterminate", description: "Occurs when the box becomes indeterminate."),
            EventSets.E("Click", description: "Occurs when the box is clicked."),
        ], EventSets.Control());
        DefaultEvent = "IsCheckedChanged";
    }
}
