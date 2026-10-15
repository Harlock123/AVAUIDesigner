using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class WindowDescriptor : StandardDescriptor
{
    public WindowDescriptor() : base("Window", typeof(Window), ToolboxCategories.Root)
    {
        Description = "A top-level window.";
        IsRootOnly = true;
        IsToolboxVisible = false;
        LayoutMode = ContainerLayoutMode.Single;
        ContentProperty = "Content";
        MaxChildren = 1;
        DefaultSize = new Size(800, 450);
        Properties = Combine(
        [
            P("Title", typeof(string), C.Window, null, "The window title."),
            // Window.IsVisible defaults to False in Avalonia (it becomes visible when shown); never omit it.
            P("IsVisible", typeof(bool), C.Appearance, null, "Whether the window is visible."),
            P("WindowStartupLocation", typeof(WindowStartupLocation), C.Window, WindowStartupLocation.Manual, "Where the window first appears."),
            P("Icon", typeof(string), C.Window, null, "Path to the window icon (e.g. /Assets/app.ico)."),
            P("CanResize", typeof(bool), C.Window, true, "Whether the user can resize the window."),
            P("SizeToContent", typeof(SizeToContent), C.Window, SizeToContent.Manual, "Whether the window sizes itself to its content."),
            P("WindowState", typeof(WindowState), C.Window, WindowState.Normal, "Normal, minimized, maximized or full screen."),
            P("ShowInTaskbar", typeof(bool), C.Window, true, "Whether the window has a taskbar button."),
            P("Topmost", typeof(bool), C.Window, false, "Whether the window stays above other windows."),
            P("SystemDecorations", typeof(SystemDecorations), C.Window, SystemDecorations.Full, "Window chrome."),
        ], Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("Opened", "EventArgs", "System", "Occurs when the window is opened."),
            EventSets.E("Closing", "WindowClosingEventArgs", EventSets.Controls, "Occurs before the window closes; can be cancelled."),
            EventSets.E("Closed", "EventArgs", "System", "Occurs after the window has closed."),
            EventSets.E("Activated", "EventArgs", "System", "Occurs when the window becomes active."),
            EventSets.E("Deactivated", "EventArgs", "System", "Occurs when the window stops being active."),
        ], EventSets.Control());
        DefaultEvent = "Opened";
    }
}
