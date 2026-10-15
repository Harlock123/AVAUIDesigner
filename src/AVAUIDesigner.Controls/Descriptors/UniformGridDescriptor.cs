using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class UniformGridDescriptor : StandardDescriptor
{
    public UniformGridDescriptor() : base("UniformGrid", typeof(UniformGrid), ToolboxCategories.Layout)
    {
        Description = "Arranges children in a grid of equally sized cells.";
        LayoutMode = ContainerLayoutMode.Wrap;
        ContentProperty = "Children";
        DefaultSize = new Size(200, 150);
        Properties = Combine(
        [
            P("Rows", typeof(int), C.Layout, 0, "Number of rows (0 = automatic)."),
            P("Columns", typeof(int), C.Layout, 0, "Number of columns (0 = automatic)."),
            P("FirstColumn", typeof(int), C.Layout, 0, "Number of empty cells before the first child."),
            P("RowSpacing", typeof(double), C.Layout, 0d, "Space between rows."),
            P("ColumnSpacing", typeof(double), C.Layout, 0d, "Space between columns."),
            P("Background", typeof(IBrush), C.Brush, null, "The background brush."),
        ], Control());
        Events = EventSets.Control().ToList();
    }
}
