using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

/// <summary>Flyout is not a <c>Control</c> (it derives from PopupFlyoutBase), so it has no live factory.</summary>
[ControlDescriptor]
public sealed class FlyoutDescriptor : StandardDescriptor
{
    public FlyoutDescriptor() : base("Flyout", typeof(Flyout).FullName!, ToolboxCategories.Dialogs)
    {
        ClrType = typeof(Flyout);
        Description = "A light-dismiss pop-up; drop it onto a Button to set its Flyout.";
        AttachToPropertyOnDrop = "Flyout";
        RenderAsPlaceholder = true;
        LayoutMode = ContainerLayoutMode.Single;
        ContentProperty = "Content";
        MaxChildren = 1;
        Properties =
        [
            P("Placement", typeof(PlacementMode), C.Layout, PlacementMode.Bottom, "Where the flyout opens relative to its target."),
            P("ShowMode", typeof(FlyoutShowMode), C.Behavior, FlyoutShowMode.Standard, "How the flyout behaves when shown (standard, transient...)."),
            P("HorizontalOffset", typeof(double), C.Layout, 0d, "Horizontal offset from the placement position."),
            P("VerticalOffset", typeof(double), C.Layout, 0d, "Vertical offset from the placement position."),
            P("FlyoutPresenterClasses", typeof(string), C.Appearance, null, "Style classes applied to the flyout presenter."),
        ];
        Events =
        [
            EventSets.E("Opening", "EventArgs", "System", "Occurs before the flyout opens."),
            EventSets.E("Opened", "EventArgs", "System", "Occurs after the flyout has opened."),
            EventSets.E("Closing", "CancelEventArgs", "System.ComponentModel", "Occurs before the flyout closes; can be cancelled."),
            EventSets.E("Closed", "EventArgs", "System", "Occurs after the flyout has closed."),
        ];
    }
}
