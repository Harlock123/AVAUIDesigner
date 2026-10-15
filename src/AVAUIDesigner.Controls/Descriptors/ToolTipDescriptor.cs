using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class ToolTipDescriptor : StandardDescriptor
{
    public ToolTipDescriptor() : base("ToolTip", typeof(ToolTip), ToolboxCategories.Menus)
    {
        Description = "Rich tooltip content; drop it onto a control to set its ToolTip.Tip.";
        AttachToPropertyOnDrop = "ToolTip.Tip";
        RenderAsPlaceholder = true;
        LayoutMode = ContainerLayoutMode.Single;
        ContentProperty = "Content";
        MaxChildren = 1;
        // Placement and ShowDelay are attached properties read from the *host* control
        // (ToolTip.Placement / ToolTip.ShowDelay), available on every control as global attached properties.
        Properties = Combine(Content("The tooltip text or content."), Control(), Templated());
        Events = EventSets.Control().ToList();
    }
}
