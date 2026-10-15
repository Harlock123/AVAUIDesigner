using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class TabControlDescriptor : StandardDescriptor
{
    public TabControlDescriptor() : base("TabControl", typeof(TabControl), ToolboxCategories.Containers)
    {
        Description = "Hosts TabItem pages.";
        LayoutMode = ContainerLayoutMode.Items;
        ContentProperty = "Items";
        AllowedChildTypes = ["TabItem"];
        DefaultSize = new Size(300, 200);
        Properties = Combine(
        [
            P("TabStripPlacement", typeof(Dock), C.Layout, Dock.Top, "Where the tab headers are placed."),
        ], Selection(includeMode: false), Control(), Templated());
        Events = EventSets.Combine(
            [EventSets.E("SelectionChanged", "SelectionChangedEventArgs", EventSets.Controls, "Occurs when the selected tab changes.")],
            EventSets.Control());
        DefaultEvent = "SelectionChanged";
        DefaultChildrenFactory = registry =>
        [
            NewTab(registry, "Tab 1"),
            NewTab(registry, "Tab 2"),
        ];
    }

    private static DesignElement NewTab(ControlDescriptorRegistry registry, string header)
    {
        var tab = registry.GetByElementName("TabItem")?.DefaultElementFactory() ?? new DesignElement("TabItem");
        tab.SetValue("Header", header);
        return tab;
    }
}
