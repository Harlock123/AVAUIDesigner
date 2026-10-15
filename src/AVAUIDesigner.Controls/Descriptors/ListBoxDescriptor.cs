using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class ListBoxDescriptor : StandardDescriptor
{
    public ListBoxDescriptor() : base("ListBox", typeof(ListBox), ToolboxCategories.Common)
    {
        Description = "A scrollable list of selectable items.";
        DefaultSize = new Size(120, 96);
        LayoutMode = ContainerLayoutMode.Items;
        ContentProperty = "Items";
        AllowedChildTypes = ["ListBoxItem"];
        Properties = Combine(Items("ListBoxItem"), Selection(), Control(), Templated());
        Events = EventSets.Combine(
            [EventSets.E("SelectionChanged", "SelectionChangedEventArgs", EventSets.Controls, "Occurs when the selection changes.")],
            EventSets.Control());
        DefaultEvent = "SelectionChanged";
    }
}
