using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class TextBoxDescriptor : StandardDescriptor
{
    public TextBoxDescriptor() : base("TextBox", typeof(TextBox), ToolboxCategories.Common)
    {
        Description = "An editable text field.";
        ContentProperty = "Text";
        DefaultSize = new Size(120, 23);
        Properties = Combine(
        [
            P("Text", typeof(string), C.Common, null, "The text content."),
            P("Watermark", typeof(string), C.Common, null, "Placeholder text shown while the box is empty."),
            P("IsReadOnly", typeof(bool), C.Behavior, false, "Whether the user can edit the text."),
            P("MaxLength", typeof(int), C.Behavior, 0, "Maximum number of characters (0 = unlimited)."),
            P("AcceptsReturn", typeof(bool), C.Behavior, false, "Whether Enter inserts a new line."),
            P("AcceptsTab", typeof(bool), C.Behavior, false, "Whether Tab inserts a tab character."),
            P("PasswordChar", typeof(char), C.Behavior, null, "Character used to mask the text (e.g. *)."),
            P("UseFloatingWatermark", typeof(bool), C.Appearance, false, "Whether the watermark floats above the text once typing starts."),
            P("MaxLines", typeof(int), C.Layout, 0, "Maximum number of visible lines (0 = unlimited)."),
            P("MinLines", typeof(int), C.Layout, 0, "Minimum number of visible lines."),
            P("CaretBrush", typeof(IBrush), C.Brush, null, "The brush of the caret."),
            P("SelectionBrush", typeof(IBrush), C.Brush, null, "The brush of the selection highlight."),
            P("HorizontalContentAlignment", typeof(HorizontalAlignment), C.Layout, null, "Horizontal alignment of the text area."),
            P("VerticalContentAlignment", typeof(VerticalAlignment), C.Layout, null, "Vertical alignment of the text area."),
        ], TextLayout(), Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("TextChanged", "TextChangedEventArgs", EventSets.Controls, "Occurs after the text has changed."),
            EventSets.E("TextChanging", "TextChangingEventArgs", EventSets.Controls, "Occurs while the text is changing, before TextChanged."),
            EventSets.E("KeyDown", "KeyEventArgs", EventSets.Input, "Occurs when a key is pressed while the text box has focus."),
        ], EventSets.Control());
        DefaultEvent = "TextChanged";
    }
}
