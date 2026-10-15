using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class MenuItemDescriptor : StandardDescriptor
{
    public MenuItemDescriptor() : base("MenuItem", typeof(MenuItem), ToolboxCategories.Menus)
    {
        Description = "A menu command; may contain a sub-menu.";
        LayoutMode = ContainerLayoutMode.Items;
        ContentProperty = "Items";
        AllowedChildTypes = ["MenuItem", "Separator"];
        DefaultValues = new Dictionary<string, string> { ["Header"] = "MenuItem" };
        Properties = Combine(
            Header("The menu text (use _ for an access key)."),
            [
                P("Icon", typeof(string), C.Appearance, null, "The icon shown next to the header."),
                P("InputGesture", typeof(string), C.Behavior, null, "Shortcut text shown next to the header, e.g. Ctrl+S (display only)."),
                P("HotKey", typeof(string), C.Behavior, null, "Keyboard shortcut that activates the item, e.g. Ctrl+S."),
            ],
            Command(),
            [
                P("ToggleType", typeof(MenuItemToggleType), C.Behavior, MenuItemToggleType.None, "Whether the item toggles as a check box or radio button."),
                P("IsChecked", typeof(bool), C.Common, false, "Whether a toggle item is checked."),
                P("GroupName", typeof(string), C.Behavior, null, "Radio items with the same group name are mutually exclusive."),
                P("StaysOpenOnClick", typeof(bool), C.Behavior, false, "Whether the menu stays open after the item is clicked."),
            ],
            Items("MenuItem", "Header"),
            Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("Click", description: "Occurs when the item is clicked."),
            EventSets.E("SubmenuOpened", description: "Occurs when the sub-menu opens."),
        ], EventSets.Control());
        DefaultEvent = "Click";
    }
}
