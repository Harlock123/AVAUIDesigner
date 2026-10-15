using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class ProgressBarDescriptor : StandardDescriptor
{
    public ProgressBarDescriptor() : base("ProgressBar", typeof(ProgressBar), ToolboxCategories.Common)
    {
        Description = "Shows the progress of an operation.";
        DefaultSize = new Size(200, 20);
        Properties = Combine(
            PropertySets.Range(),
            [
                P("IsIndeterminate", typeof(bool), C.Behavior, false, "Shows an animation instead of a value."),
                P("Orientation", typeof(Orientation), C.Layout, Orientation.Horizontal, "Horizontal or vertical."),
                P("ShowProgressText", typeof(bool), C.Appearance, false, "Whether the progress text is shown."),
                P("ProgressTextFormat", typeof(string), C.Appearance, "{1:0}%", "Format of the progress text ({0} = value, {1} = percentage)."),
            ],
            Control(), Templated());
        Events = EventSets.Combine(
            [EventSets.E("ValueChanged", "RangeBaseValueChangedEventArgs", EventSets.Primitives, "Occurs when the value changes.")],
            EventSets.Control());
    }
}
