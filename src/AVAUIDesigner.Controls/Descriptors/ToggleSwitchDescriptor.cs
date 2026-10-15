using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class ToggleSwitchDescriptor : StandardDescriptor
{
    public ToggleSwitchDescriptor() : base("ToggleSwitch", typeof(ToggleSwitch), ToolboxCategories.Input)
    {
        Description = "An on/off switch.";
        DefaultSize = new Size(120, 30);
        ContentProperty = "Content";
        Properties = Combine(
        [
            P("IsChecked", typeof(bool?), C.Common, false, "Whether the switch is on."),
            P("OnContent", typeof(string), C.Common, "On", "Content shown when the switch is on."),
            P("OffContent", typeof(string), C.Common, "Off", "Content shown when the switch is off."),
        ],
        Content("Header shown above the switch."),
        Command(), Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("IsCheckedChanged", description: "Occurs when IsChecked changes."),
            EventSets.E("Checked", description: "Occurs when the switch is turned on."),
            EventSets.E("Unchecked", description: "Occurs when the switch is turned off."),
        ], EventSets.Control());
        DefaultEvent = "IsCheckedChanged";
    }
}
