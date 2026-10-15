using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class DatePickerDescriptor : StandardDescriptor
{
    public DatePickerDescriptor() : base("DatePicker", typeof(DatePicker), ToolboxCategories.Input)
    {
        Description = "Selects a date using day/month/year spinners.";
        DefaultSize = new Size(200, 30);
        Properties = Combine(
        [
            P("SelectedDate", typeof(DateTimeOffset?), C.Common, null, "The selected date (empty = none)."),
            P("DayVisible", typeof(bool), C.Appearance, true, "Whether the day column is shown."),
            P("MonthVisible", typeof(bool), C.Appearance, true, "Whether the month column is shown."),
            P("YearVisible", typeof(bool), C.Appearance, true, "Whether the year column is shown."),
            P("DayFormat", typeof(string), C.Text, "%d", "Format of the day column."),
            P("MonthFormat", typeof(string), C.Text, "MMMM", "Format of the month column."),
            P("YearFormat", typeof(string), C.Text, "yyyy", "Format of the year column."),
        ], Control(), Templated());
        Events = EventSets.Combine(
            [EventSets.E("SelectedDateChanged", "DatePickerSelectedValueChangedEventArgs", EventSets.Controls, "Occurs when the selected date changes.")],
            EventSets.Control());
        DefaultEvent = "SelectedDateChanged";
    }
}
