using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class GridDescriptor : StandardDescriptor
{
    public GridDescriptor() : base("Grid", typeof(Grid), ToolboxCategories.Containers)
    {
        Description = "Arranges children in rows and columns.";
        LayoutMode = ContainerLayoutMode.Grid;
        ContentProperty = "Children";
        DefaultSize = new Size(200, 150);
        ChildAttachedProperties = AttachedPropertySets.Grid;
        Properties = Combine(
        [
            P("Background", typeof(IBrush), C.Brush, null, "The background brush."),
            P("ShowGridLines", typeof(bool), C.Appearance, false, "Draws the row/column boundaries (debug aid)."),
            P("RowDefinitions", typeof(string), C.Layout, null, "Row heights, e.g. \"Auto,*,Auto\" (use the Grid editor)."),
            P("ColumnDefinitions", typeof(string), C.Layout, null, "Column widths, e.g. \"*,2*\" (use the Grid editor)."),
        ], Control());
        Events = EventSets.Control().ToList();
    }
}
