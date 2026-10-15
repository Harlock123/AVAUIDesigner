using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class PopupDescriptor : StandardDescriptor
{
    public PopupDescriptor() : base("Popup", typeof(Popup), ToolboxCategories.Dialogs)
    {
        Description = "Displays a single child in a floating layer above other content.";
        RenderAsPlaceholder = true;
        LayoutMode = ContainerLayoutMode.Single;
        ContentProperty = "Child";
        MaxChildren = 1;
        Properties = Combine(
        [
            P("IsOpen", typeof(bool), C.Common, false, "Whether the popup is shown."),
            P("Placement", typeof(PlacementMode), C.Layout, PlacementMode.Bottom, "Where the popup opens relative to its target."),
            P("PlacementTarget", typeof(string), C.Layout, null, "The control the popup is positioned against, e.g. {Binding ElementName=MyButton}."),
            P("HorizontalOffset", typeof(double), C.Layout, 0d, "Horizontal offset from the placement position."),
            P("VerticalOffset", typeof(double), C.Layout, 0d, "Vertical offset from the placement position."),
            P("IsLightDismissEnabled", typeof(bool), C.Behavior, false, "Whether clicking outside closes the popup."),
            P("Topmost", typeof(bool), C.Behavior, false, "Whether the popup stays above other windows."),
        ], Control());
        Events = EventSets.Combine(
        [
            EventSets.E("Opened", "EventArgs", "System", "Occurs when the popup opens."),
            EventSets.E("Closed", "EventArgs", "System", "Occurs when the popup closes."),
        ], EventSets.Control());
    }
}
