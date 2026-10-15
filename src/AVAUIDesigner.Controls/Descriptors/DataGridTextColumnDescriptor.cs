using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

/// <summary>A DataGrid column; not a <c>Control</c>, so it renders as a placeholder and has no live factory.</summary>
[ControlDescriptor]
public sealed class DataGridTextColumnDescriptor : StandardDescriptor
{
    public DataGridTextColumnDescriptor()
        : base("DataGridTextColumn", typeof(DataGridTextColumn).FullName!, ToolboxCategories.Data)
    {
        ClrType = typeof(DataGridTextColumn);
        Description = "A DataGrid column that displays text.";
        IsToolboxVisible = false;
        RenderAsPlaceholder = true;
        Properties =
        [
            P("Header", typeof(string), C.Common, null, "The column header."),
            P("Binding", typeof(string), C.Data, null, "The cell value binding, e.g. {Binding Name}."),
            WithValues(P("Width", typeof(string), C.Layout, null, "Column width: pixels, Auto, *, SizeToCells or SizeToHeader."),
                "Auto", "*", "SizeToCells", "SizeToHeader"),
            P("IsReadOnly", typeof(bool), C.Behavior, false, "Whether cells of this column can be edited."),
            P("CanUserSort", typeof(bool), C.Behavior, null, "Whether this column can be sorted (defaults to the grid setting)."),
            P("CanUserResize", typeof(bool), C.Behavior, null, "Whether this column can be resized (defaults to the grid setting)."),
            P("CanUserReorder", typeof(bool), C.Behavior, null, "Whether this column can be reordered (defaults to the grid setting)."),
            P("SortMemberPath", typeof(string), C.Data, null, "Property path used when sorting."),
            P("IsVisible", typeof(bool), C.Appearance, true, "Whether the column is shown."),
        ];
    }
}
