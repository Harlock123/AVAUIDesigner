using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class AutoCompleteBoxDescriptor : StandardDescriptor
{
    public AutoCompleteBoxDescriptor() : base("AutoCompleteBox", typeof(AutoCompleteBox), ToolboxCategories.Input)
    {
        Description = "A text box that suggests matching items as the user types.";
        DefaultSize = new Size(200, 23);
        Properties = Combine(
        [
            P("Text", typeof(string), C.Common, string.Empty, "The text content."),
            P("ItemsSource", typeof(string), C.Data, null, "The suggestions (usually a {Binding})."),
            P("SelectedItem", typeof(string), C.Data, null, "The selected suggestion (usually a {Binding})."),
            P("FilterMode", typeof(AutoCompleteFilterMode), C.Behavior, AutoCompleteFilterMode.StartsWith, "How suggestions are matched against the text."),
            P("Watermark", typeof(string), C.Common, null, "Placeholder text shown while the box is empty."),
            P("MinimumPrefixLength", typeof(int), C.Behavior, 1, "Characters typed before suggestions appear."),
            P("MinimumPopulateDelay", typeof(TimeSpan), C.Behavior, TimeSpan.Zero, "Delay after typing before suggestions are populated."),
            P("IsTextCompletionEnabled", typeof(bool), C.Behavior, false, "Whether the first match is completed inline."),
            P("MaxLength", typeof(int), C.Behavior, 0, "Maximum number of characters (0 = unlimited)."),
            P("IsTabStop", typeof(bool), C.Behavior, false, "Whether the element is included in tab navigation."),
            P("MaxDropDownHeight", typeof(double), C.Layout, double.PositiveInfinity, "Maximum height of the suggestion list."),
            new PropertyDescriptor("ItemTemplate", typeof(object), C.Data, null, "The template used to display each suggestion.") { IsXamlFragment = true },
        ], Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("TextChanged", "TextChangedEventArgs", EventSets.Controls, "Occurs when the text changes."),
            EventSets.E("SelectionChanged", "SelectionChangedEventArgs", EventSets.Controls, "Occurs when the selected suggestion changes."),
            EventSets.E("DropDownOpened", "EventArgs", "System", "Occurs when the suggestion list opens."),
            EventSets.E("DropDownClosed", "EventArgs", "System", "Occurs when the suggestion list closes."),
        ], EventSets.Control());
        DefaultEvent = "TextChanged";
    }
}
