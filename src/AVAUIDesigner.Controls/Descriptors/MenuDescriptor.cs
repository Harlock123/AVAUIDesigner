using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class MenuDescriptor : StandardDescriptor
{
    public MenuDescriptor() : base("Menu", typeof(Menu), ToolboxCategories.Menus)
    {
        Description = "A horizontal menu bar of MenuItems.";
        LayoutMode = ContainerLayoutMode.Items;
        ContentProperty = "Items";
        AllowedChildTypes = ["MenuItem"];
        Properties = Combine(Items("MenuItem", "Header"), Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("Opened", description: "Occurs when a top-level menu opens."),
            EventSets.E("Closed", description: "Occurs when the menu closes."),
        ], EventSets.Control());
        DefaultChildrenFactory = registry =>
        [
            NewItem(registry, "_File"),
        ];
    }

    private static DesignElement NewItem(ControlDescriptorRegistry registry, string header)
    {
        var item = registry.GetByElementName("MenuItem")?.DefaultElementFactory() ?? new DesignElement("MenuItem");
        item.SetValue("Header", header);
        return item;
    }
}
