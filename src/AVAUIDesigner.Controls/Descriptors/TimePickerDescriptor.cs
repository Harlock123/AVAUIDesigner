using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class TimePickerDescriptor : StandardDescriptor
{
    public TimePickerDescriptor() : base("TimePicker", typeof(TimePicker), ToolboxCategories.Input)
    {
        Description = "Selects a time of day.";
        DefaultSize = new Size(200, 30);
        Properties = Combine(
        [
            P("SelectedTime", typeof(TimeSpan?), C.Common, null, "The selected time (empty = none)."),
            WithValues(P("ClockIdentifier", typeof(string), C.Behavior, "12HourClock", "12- or 24-hour clock."), "12HourClock", "24HourClock"),
            P("MinuteIncrement", typeof(int), C.Behavior, 1, "Step of the minute spinner."),
            P("UseSeconds", typeof(bool), C.Behavior, false, "Whether a seconds column is shown."),
            P("SecondIncrement", typeof(int), C.Behavior, 1, "Step of the seconds spinner."),
        ], Control(), Templated());
        Events = EventSets.Combine(
            [EventSets.E("SelectedTimeChanged", "TimePickerSelectedValueChangedEventArgs", EventSets.Controls, "Occurs when the selected time changes.")],
            EventSets.Control());
        DefaultEvent = "SelectedTimeChanged";
    }
}
