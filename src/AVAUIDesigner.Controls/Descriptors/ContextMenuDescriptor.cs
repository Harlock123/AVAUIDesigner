using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class ContextMenuDescriptor : StandardDescriptor
{
    public ContextMenuDescriptor() : base("ContextMenu", typeof(ContextMenu), ToolboxCategories.Menus)
    {
        Description = "A pop-up menu shown on right-click; drop it onto a control to attach it.";
        AttachToPropertyOnDrop = "ContextMenu";
        RenderAsPlaceholder = true;
        LayoutMode = ContainerLayoutMode.Items;
        ContentProperty = "Items";
        AllowedChildTypes = ["MenuItem", "Separator"];
        Properties = Combine(
            Items("MenuItem", "Header"),
            [
                P("Placement", typeof(PlacementMode), C.Layout, PlacementMode.Pointer, "Where the menu opens relative to its target."),
                P("HorizontalOffset", typeof(double), C.Layout, 0d, "Horizontal offset from the placement position."),
                P("VerticalOffset", typeof(double), C.Layout, 0d, "Vertical offset from the placement position."),
            ],
            Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("Opening", "CancelEventArgs", "System.ComponentModel", "Occurs before the menu opens; can be cancelled."),
            EventSets.E("Opened", description: "Occurs after the menu has opened."),
            EventSets.E("Closing", "CancelEventArgs", "System.ComponentModel", "Occurs before the menu closes; can be cancelled."),
            EventSets.E("Closed", description: "Occurs after the menu has closed."),
        ], EventSets.Control());
        DefaultChildrenFactory = registry =>
        [
            registry.GetByElementName("MenuItem")?.DefaultElementFactory() ?? new DesignElement("MenuItem"),
        ];
    }
}
