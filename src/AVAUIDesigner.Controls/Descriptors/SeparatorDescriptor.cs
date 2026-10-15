using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class SeparatorDescriptor : StandardDescriptor
{
    public SeparatorDescriptor() : base("Separator", typeof(Separator), ToolboxCategories.Layout)
    {
        Description = "A thin dividing line between items or controls.";
        Properties = Combine(Control(), Templated());
        Events = EventSets.Control().ToList();
    }
}
