using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class NumericUpDownDescriptor : StandardDescriptor
{
    public NumericUpDownDescriptor() : base("NumericUpDown", typeof(NumericUpDown), ToolboxCategories.Input)
    {
        Description = "A numeric text box with spin buttons.";
        DefaultSize = new Size(120, 23);
        Properties = Combine(
        [
            P("Value", typeof(decimal?), C.Common, null, "The current value (empty = no value)."),
            P("Minimum", typeof(decimal), C.Common, decimal.MinValue, "The minimum value."),
            P("Maximum", typeof(decimal), C.Common, decimal.MaxValue, "The maximum value."),
            P("Increment", typeof(decimal), C.Common, 1m, "The amount added or removed by the spin buttons."),
            P("FormatString", typeof(string), C.Text, string.Empty, "Display format, e.g. \"N2\" or \"0.00\" (controls the decimal places)."),
            P("Watermark", typeof(string), C.Common, null, "Placeholder text shown while there is no value."),
            P("IsReadOnly", typeof(bool), C.Behavior, false, "Whether the user can change the value."),
            P("ShowButtonSpinner", typeof(bool), C.Appearance, true, "Whether the spin buttons are shown."),
            P("AllowSpin", typeof(bool), C.Behavior, true, "Whether the spin buttons and arrow keys change the value."),
            P("ClipValueToMinMax", typeof(bool), C.Behavior, false, "Whether out-of-range input is clamped instead of rejected."),
            P("IsTabStop", typeof(bool), C.Behavior, false, "Whether the element is included in tab navigation."),
            P("ButtonSpinnerLocation", typeof(Location), C.Appearance, Location.Right, "Which side the spin buttons are on."),
        ], Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("ValueChanged", "NumericUpDownValueChangedEventArgs", EventSets.Controls, "Occurs when the value changes."),
            EventSets.E("Spinned", "SpinEventArgs", EventSets.Controls, "Occurs when a spin button or arrow key is used."),
        ], EventSets.Control());
        DefaultEvent = "ValueChanged";
    }
}
