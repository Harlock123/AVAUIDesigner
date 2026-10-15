using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class ItemsControlDescriptor : StandardDescriptor
{
    public ItemsControlDescriptor() : base("ItemsControl", typeof(ItemsControl), ToolboxCategories.Data)
    {
        Description = "Displays a collection of items using a template.";
        DefaultSize = new Size(200, 200);
        LayoutMode = ContainerLayoutMode.Items;
        ContentProperty = "Items";
        Properties = Combine(
        [
            P("ItemsSource", typeof(string), C.Data, null, "The collection used to generate items (usually a {Binding})."),
            new PropertyDescriptor("ItemTemplate", typeof(object), C.Data, null, "The template used to display each item.") { IsXamlFragment = true },
            new PropertyDescriptor("ItemsPanel", typeof(object), C.Layout, null, "The panel that arranges the items.") { IsXamlFragment = true },
        ], Control(), Templated());
        Events = EventSets.Control().ToList();
    }
}
