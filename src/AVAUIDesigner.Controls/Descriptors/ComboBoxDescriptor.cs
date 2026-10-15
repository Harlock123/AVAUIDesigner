using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class ComboBoxDescriptor : StandardDescriptor
{
    public ComboBoxDescriptor() : base("ComboBox", typeof(ComboBox), ToolboxCategories.Common)
    {
        Description = "A drop-down list of items.";
        DefaultSize = new Size(120, 23);
        LayoutMode = ContainerLayoutMode.Items;
        ContentProperty = "Items";
        AllowedChildTypes = ["ComboBoxItem"];
        Properties = Combine(
            Items("ComboBoxItem"),
            Selection(includeMode: false),
            [
                P("IsEditable", typeof(bool), C.Behavior, false, "Whether the user can type a value."),
                P("PlaceholderText", typeof(string), C.Common, null, "Text shown when nothing is selected."),
                P("MaxDropDownHeight", typeof(double), C.Layout, 200d, "Maximum height of the drop-down."),
                P("IsDropDownOpen", typeof(bool), C.Behavior, false, "Whether the drop-down is open."),
            ],
            Control(), Templated());
        Events = EventSets.Combine(
            [EventSets.E("SelectionChanged", "SelectionChangedEventArgs", EventSets.Controls, "Occurs when the selection changes.")],
            EventSets.Control());
        DefaultEvent = "SelectionChanged";
    }
}
