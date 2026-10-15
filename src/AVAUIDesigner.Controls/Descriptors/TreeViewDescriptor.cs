using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class TreeViewDescriptor : StandardDescriptor
{
    public TreeViewDescriptor() : base("TreeView", typeof(TreeView), ToolboxCategories.Data)
    {
        Description = "A hierarchical list of expandable nodes.";
        DefaultSize = new Size(200, 300);
        LayoutMode = ContainerLayoutMode.Items;
        ContentProperty = "Items";
        AllowedChildTypes = ["TreeViewItem"];
        Properties = Combine(
            Items("TreeViewItem", "Header"),
            [
                P("SelectedItem", typeof(string), C.Common, null, "The selected node (usually a {Binding})."),
                P("SelectionMode", typeof(SelectionMode), C.Behavior, SelectionMode.Single, "Single or multiple selection."),
                P("AutoScrollToSelectedItem", typeof(bool), C.Behavior, true, "Whether the selected node is scrolled into view."),
            ],
            Control(), Templated());
        Events = EventSets.Combine(
            [EventSets.E("SelectionChanged", "SelectionChangedEventArgs", EventSets.Controls, "Occurs when the selection changes.")],
            EventSets.Control());
        DefaultEvent = "SelectionChanged";
    }
}
