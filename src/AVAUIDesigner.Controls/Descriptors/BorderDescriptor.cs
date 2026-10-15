using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class BorderDescriptor : StandardDescriptor
{
    public BorderDescriptor() : base("Border", typeof(Border), ToolboxCategories.Containers)
    {
        Description = "Draws a border, background and/or shadow around a single child.";
        LayoutMode = ContainerLayoutMode.Single;
        ContentProperty = "Child";
        MaxChildren = 1;
        DefaultSize = new Size(200, 150);
        Properties = Combine(
        [
            P("Background", typeof(IBrush), C.Brush, null, "The background brush."),
            P("BorderBrush", typeof(IBrush), C.Brush, null, "The border brush."),
            P("BorderThickness", typeof(Thickness), C.Appearance, default(Thickness), "The border thickness."),
            P("CornerRadius", typeof(CornerRadius), C.Appearance, default(CornerRadius), "The corner radius."),
            P("Padding", typeof(Thickness), C.Layout, default(Thickness), "Space between the border and the child."),
            P("BoxShadow", typeof(string), C.Appearance, null, "Drop shadow(s), e.g. \"5 5 10 0 Gray\"."),
            P("BackgroundSizing", typeof(BackgroundSizing), C.Appearance, BackgroundSizing.CenterBorder, "How far the background extends under the border."),
        ], Control());
        Events = EventSets.Control().ToList();
    }
}
