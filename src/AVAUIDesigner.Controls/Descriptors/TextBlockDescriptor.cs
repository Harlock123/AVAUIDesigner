using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class TextBlockDescriptor : StandardDescriptor
{
    public TextBlockDescriptor() : base("TextBlock", typeof(TextBlock), ToolboxCategories.Common)
    {
        Description = "Displays read-only text.";
        ContentProperty = "Text";
        DefaultSize = new Size(120, 20);
        DefaultValues = new Dictionary<string, string> { ["Text"] = "TextBlock" };
        Properties = Combine(
        [
            P("Text", typeof(string), C.Common, null, "The text to display."),
            P("Foreground", typeof(IBrush), C.Brush, null, "The text brush."),
            P("Background", typeof(IBrush), C.Brush, null, "The background brush."),
        ],
        Font(),
        TextLayout(),
        [
            WithValues(P("TextTrimming", typeof(TextTrimming), C.Text, null, "How text that overflows is trimmed."),
                "None", "CharacterEllipsis", "WordEllipsis", "PrefixCharacterEllipsis", "LeadingCharacterEllipsis"),
            WithValues(P("TextDecorations", typeof(string), C.Text, null, "Text decorations (Underline, Strikethrough...)."),
                "Underline", "Strikethrough", "Overline", "Baseline"),
            P("LineHeight", typeof(double), C.Text, double.NaN, "Height of each line (NaN = automatic)."),
            P("LetterSpacing", typeof(double), C.Text, 0d, "Extra space between characters."),
            P("MaxLines", typeof(int), C.Text, 0, "Maximum number of lines (0 = unlimited)."),
            P("Padding", typeof(Thickness), C.Layout, default(Thickness), "The inner padding."),
        ], Control());
        Events = EventSets.Control().ToList();
    }
}
