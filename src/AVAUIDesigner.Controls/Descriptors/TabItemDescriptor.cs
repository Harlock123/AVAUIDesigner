using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class TabItemDescriptor : StandardDescriptor
{
    public TabItemDescriptor() : base("TabItem", typeof(TabItem), ToolboxCategories.Containers)
    {
        Description = "A page of a TabControl with a header and a single content element.";
        LayoutMode = ContainerLayoutMode.Single;
        ContentProperty = "Content";
        MaxChildren = 1;
        DefaultValues = new Dictionary<string, string> { ["Header"] = "TabItem" };
        Properties = Combine(
            Header("The tab header."),
            [
                P("IsSelected", typeof(bool), C.Common, false, "Whether this tab is the selected one."),
                P("HorizontalContentAlignment", typeof(HorizontalAlignment), C.Layout, null, "Horizontal alignment of the content."),
                P("VerticalContentAlignment", typeof(VerticalAlignment), C.Layout, null, "Vertical alignment of the content."),
            ],
            Control(), Templated());
        Events = EventSets.Control().ToList();
    }
}
