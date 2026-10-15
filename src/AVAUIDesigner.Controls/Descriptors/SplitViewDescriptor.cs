using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class SplitViewDescriptor : StandardDescriptor
{
    public SplitViewDescriptor() : base("SplitView", typeof(SplitView), ToolboxCategories.Containers)
    {
        Description = "A collapsible side pane next to the main content.";
        LayoutMode = ContainerLayoutMode.Single;
        ContentProperty = "Content";
        MaxChildren = 1;
        DefaultSize = new Size(200, 150);
        Properties = Combine(
        [
            P("PanePlacement", typeof(SplitViewPanePlacement), C.Layout, SplitViewPanePlacement.Left, "Which side the pane is on."),
            P("IsPaneOpen", typeof(bool), C.Common, false, "Whether the pane is expanded."),
            P("OpenPaneLength", typeof(double), C.Layout, 320d, "Width of the pane when open."),
            P("CompactPaneLength", typeof(double), C.Layout, 48d, "Width of the pane when closed in a compact mode."),
            P("DisplayMode", typeof(SplitViewDisplayMode), C.Behavior, SplitViewDisplayMode.Overlay, "How the pane is shown (inline, overlay, compact...)."),
            P("PaneBackground", typeof(IBrush), C.Brush, null, "The pane background brush."),
            P("UseLightDismissOverlayMode", typeof(bool), C.Behavior, false, "Whether a dimming overlay is shown behind an open overlay pane."),
        ], Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("PaneOpened", description: "Occurs after the pane has opened."),
            EventSets.E("PaneClosed", description: "Occurs after the pane has closed."),
            EventSets.E("PaneOpening", "CancelRoutedEventArgs", EventSets.Interactivity, "Occurs before the pane opens; can be cancelled."),
            EventSets.E("PaneClosing", "CancelRoutedEventArgs", EventSets.Interactivity, "Occurs before the pane closes; can be cancelled."),
        ], EventSets.Control());
    }
}
