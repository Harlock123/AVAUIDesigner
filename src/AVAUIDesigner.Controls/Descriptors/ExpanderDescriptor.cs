using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class ExpanderDescriptor : StandardDescriptor
{
    public ExpanderDescriptor() : base("Expander", typeof(Expander), ToolboxCategories.Containers)
    {
        Description = "A header that expands or collapses a single content element.";
        LayoutMode = ContainerLayoutMode.Single;
        ContentProperty = "Content";
        MaxChildren = 1;
        DefaultSize = new Size(200, 150);
        DefaultValues = new Dictionary<string, string> { ["Header"] = "Expander", ["IsExpanded"] = "True" };
        Properties = Combine(
            Header("The header shown next to the expand toggle."),
            [
                P("IsExpanded", typeof(bool), C.Common, false, "Whether the content is visible."),
                P("ExpandDirection", typeof(ExpandDirection), C.Layout, ExpandDirection.Down, "The direction the content expands in."),
                P("HorizontalContentAlignment", typeof(HorizontalAlignment), C.Layout, null, "Horizontal alignment of the content."),
                P("VerticalContentAlignment", typeof(VerticalAlignment), C.Layout, null, "Vertical alignment of the content."),
            ],
            Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("Expanded", description: "Occurs after the content has expanded."),
            EventSets.E("Collapsed", description: "Occurs after the content has collapsed."),
            EventSets.E("Expanding", "CancelRoutedEventArgs", EventSets.Interactivity, "Occurs before the content expands; can be cancelled."),
            EventSets.E("Collapsing", "CancelRoutedEventArgs", EventSets.Interactivity, "Occurs before the content collapses; can be cancelled."),
        ], EventSets.Control());
        DefaultEvent = "Expanded";
    }
}
