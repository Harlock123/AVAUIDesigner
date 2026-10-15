using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class UserControlDescriptor : StandardDescriptor
{
    public UserControlDescriptor() : base("UserControl", typeof(UserControl), ToolboxCategories.Root)
    {
        Description = "A reusable composite control.";
        IsRootOnly = true;
        IsToolboxVisible = false;
        LayoutMode = ContainerLayoutMode.Single;
        ContentProperty = "Content";
        MaxChildren = 1;
        DefaultSize = new Size(400, 300);
        Properties = Combine(
        [
            P("HorizontalContentAlignment", typeof(HorizontalAlignment), C.Layout, null, "Horizontal alignment of the content."),
            P("VerticalContentAlignment", typeof(VerticalAlignment), C.Layout, null, "Vertical alignment of the content."),
        ], Control(), Templated());
        Events = EventSets.Control().ToList();
    }
}
