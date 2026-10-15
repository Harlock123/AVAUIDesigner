using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class ComboBoxItemDescriptor : StandardDescriptor
{
    public ComboBoxItemDescriptor() : base("ComboBoxItem", typeof(ComboBoxItem), ToolboxCategories.Data)
    {
        Description = "An item of a ComboBox.";
        IsToolboxVisible = false;
        ContentProperty = "Content";
        DefaultValues = new Dictionary<string, string> { ["Content"] = "Item" };
        Properties = Combine(
            Content("The item content."),
            [P("IsSelected", typeof(bool), C.Common, false, "Whether the item is selected.")],
            Control(), Templated());
        Events = EventSets.Control().ToList();
    }
}
