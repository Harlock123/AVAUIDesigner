using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class CalendarDatePickerDescriptor : StandardDescriptor
{
    public CalendarDatePickerDescriptor() : base("CalendarDatePicker", typeof(CalendarDatePicker), ToolboxCategories.Input)
    {
        Description = "A date text box with a drop-down calendar.";
        DefaultSize = new Size(200, 30);
        Properties = Combine(
        [
            P("SelectedDate", typeof(DateTime?), C.Common, null, "The selected date (empty = none)."),
            P("Watermark", typeof(string), C.Common, null, "Placeholder text shown while no date is selected."),
            P("DisplayDateStart", typeof(DateTime?), C.Behavior, null, "First selectable date."),
            P("DisplayDateEnd", typeof(DateTime?), C.Behavior, null, "Last selectable date."),
            P("SelectedDateFormat", typeof(CalendarDatePickerFormat), C.Text, CalendarDatePickerFormat.Short, "Short, long or custom date format."),
            P("CustomDateFormatString", typeof(string), C.Text, "d", "Format used when SelectedDateFormat is Custom."),
            P("IsTodayHighlighted", typeof(bool), C.Appearance, null, "Whether today's date is highlighted in the calendar."),
            P("FirstDayOfWeek", typeof(DayOfWeek), C.Behavior, null, "First day of the week in the calendar."),
            P("UseFloatingWatermark", typeof(bool), C.Appearance, false, "Whether the watermark floats above the text once a date is entered."),
        ], Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("SelectedDateChanged", "SelectionChangedEventArgs", EventSets.Controls, "Occurs when the selected date changes."),
            EventSets.E("CalendarOpened", "EventArgs", "System", "Occurs when the drop-down calendar opens."),
            EventSets.E("CalendarClosed", "EventArgs", "System", "Occurs when the drop-down calendar closes."),
        ], EventSets.Control());
        DefaultEvent = "SelectedDateChanged";
    }
}
