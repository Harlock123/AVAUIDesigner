using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class ScrollViewerDescriptor : StandardDescriptor
{
    public ScrollViewerDescriptor() : base("ScrollViewer", typeof(ScrollViewer), ToolboxCategories.Containers)
    {
        Description = "Scrolls a single child that is larger than the available space.";
        LayoutMode = ContainerLayoutMode.Single;
        ContentProperty = "Content";
        MaxChildren = 1;
        DefaultSize = new Size(200, 150);
        Properties = Combine(
        [
            P("HorizontalScrollBarVisibility", typeof(ScrollBarVisibility), C.Behavior, ScrollBarVisibility.Disabled, "Visibility of the horizontal scroll bar."),
            P("VerticalScrollBarVisibility", typeof(ScrollBarVisibility), C.Behavior, ScrollBarVisibility.Auto, "Visibility of the vertical scroll bar."),
            P("AllowAutoHide", typeof(bool), C.Behavior, true, "Whether scroll bars hide when not in use."),
            P("IsScrollInertiaEnabled", typeof(bool), C.Behavior, true, "Whether scrolling continues with inertia after a swipe."),
            P("HorizontalContentAlignment", typeof(HorizontalAlignment), C.Layout, null, "Horizontal alignment of the content."),
            P("VerticalContentAlignment", typeof(VerticalAlignment), C.Layout, null, "Vertical alignment of the content."),
        ], Control(), Templated());
        Events = EventSets.Combine(
            [EventSets.E("ScrollChanged", "ScrollChangedEventArgs", EventSets.Controls, "Occurs when the scroll position, extent or viewport changes.")],
            EventSets.Control());
        DefaultEvent = "ScrollChanged";
    }
}
