using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class SliderDescriptor : StandardDescriptor
{
    public SliderDescriptor() : base("Slider", typeof(Slider), ToolboxCategories.Common)
    {
        Description = "Selects a value from a range by dragging a thumb.";
        DefaultSize = new Size(200, 20);
        Properties = Combine(
            PropertySets.Range(),
            [
                P("SmallChange", typeof(double), C.Behavior, 1d, "Change applied by the arrow keys."),
                P("LargeChange", typeof(double), C.Behavior, 10d, "Change applied by Page Up/Down or clicking the track."),
                P("TickFrequency", typeof(double), C.Appearance, 0d, "Interval between tick marks."),
                P("TickPlacement", typeof(TickPlacement), C.Appearance, TickPlacement.None, "Where tick marks are drawn."),
                P("IsSnapToTickEnabled", typeof(bool), C.Behavior, false, "Whether the thumb snaps to tick marks."),
                P("Orientation", typeof(Orientation), C.Layout, Orientation.Horizontal, "Horizontal or vertical."),
                P("IsDirectionReversed", typeof(bool), C.Behavior, false, "Whether the maximum is at the start."),
            ],
            Control(), Templated());
        Events = EventSets.Combine(
            [EventSets.E("ValueChanged", "RangeBaseValueChangedEventArgs", EventSets.Primitives, "Occurs when the value changes.")],
            EventSets.Control());
        DefaultEvent = "ValueChanged";
    }
}
