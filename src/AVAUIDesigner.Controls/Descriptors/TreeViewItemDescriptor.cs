using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class TreeViewItemDescriptor : StandardDescriptor
{
    public TreeViewItemDescriptor() : base("TreeViewItem", typeof(TreeViewItem), ToolboxCategories.Data)
    {
        Description = "A node of a TreeView; may contain child nodes.";
        IsToolboxVisible = false;
        LayoutMode = ContainerLayoutMode.Items;
        ContentProperty = "Items";
        AllowedChildTypes = ["TreeViewItem"];
        DefaultValues = new Dictionary<string, string> { ["Header"] = "Item" };
        Properties = Combine(
            Header("The node caption."),
            [
                P("IsExpanded", typeof(bool), C.Common, false, "Whether the child nodes are shown."),
                P("IsSelected", typeof(bool), C.Common, false, "Whether the node is selected."),
                P("ItemsSource", typeof(string), C.Data, null, "The collection used to generate child nodes (usually a {Binding})."),
            ],
            Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("Expanded", description: "Occurs when the node is expanded."),
            EventSets.E("Collapsed", description: "Occurs when the node is collapsed."),
        ], EventSets.Control());
    }
}
