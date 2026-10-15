using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace AVAUIDesigner.Shell.Controls;

/// <summary>One completion entry (attribute or element name) in the XAML editor.</summary>
public class XamlCompletionData(string text, string description, bool isAttribute, double priority = 0) : ICompletionData
{
    public IImage? Image => null;
    public string Text { get; } = text;
    public object Content => Text;
    public object Description { get; } = description;
    public double Priority { get; } = priority;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        if (isAttribute)
        {
            textArea.Document.Replace(completionSegment, Text + "=\"\"");
            textArea.Caret.Offset = completionSegment.Offset + Text.Length + 2;
        }
        else
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }
}
