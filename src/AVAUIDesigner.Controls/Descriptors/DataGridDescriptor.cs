using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class DataGridDescriptor : StandardDescriptor
{
    public DataGridDescriptor() : base("DataGrid", typeof(DataGrid), ToolboxCategories.Data)
    {
        Description = "A grid of rows and columns bound to a collection.";
        DefaultSize = new Size(300, 200);
        Properties = Combine(
        [
            P("ItemsSource", typeof(string), C.Data, null, "The rows (usually a {Binding})."),
            new PropertyDescriptor("Columns", typeof(string), C.Data, null, "The column definitions.")
            {
                CollectionItemElement = "DataGridTextColumn",
                CollectionItemProperties = ["Header", "Binding"],
                CollectionIsPropertyElement = true,
            },
            P("AutoGenerateColumns", typeof(bool), C.Data, false, "Whether columns are generated from the item properties."),
            P("SelectedItem", typeof(string), C.Data, null, "The selected row (usually a {Binding})."),
            P("IsReadOnly", typeof(bool), C.Behavior, false, "Whether cells can be edited."),
            P("CanUserSortColumns", typeof(bool), C.Behavior, true, "Whether clicking a header sorts the column."),
            P("CanUserResizeColumns", typeof(bool), C.Behavior, false, "Whether columns can be resized by dragging."),
            P("CanUserReorderColumns", typeof(bool), C.Behavior, false, "Whether columns can be reordered by dragging."),
            P("SelectionMode", typeof(DataGridSelectionMode), C.Behavior, DataGridSelectionMode.Extended, "Single or extended row selection."),
            P("GridLinesVisibility", typeof(DataGridGridLinesVisibility), C.Appearance, null, "Which grid lines are drawn."),
            P("HeadersVisibility", typeof(DataGridHeadersVisibility), C.Appearance, null, "Whether column and/or row headers are shown."),
            P("FrozenColumnCount", typeof(int), C.Layout, 0, "Number of leading columns that do not scroll horizontally."),
            P("RowBackground", typeof(IBrush), C.Brush, null, "The row background brush."),
            P("HorizontalGridLinesBrush", typeof(IBrush), C.Brush, null, "The brush of horizontal grid lines."),
            P("VerticalGridLinesBrush", typeof(IBrush), C.Brush, null, "The brush of vertical grid lines."),
        ], Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("SelectionChanged", "SelectionChangedEventArgs", EventSets.Controls, "Occurs when the selected rows change."),
            EventSets.E("CellEditEnding", "DataGridCellEditEndingEventArgs", EventSets.Controls, "Occurs before a cell edit is committed or cancelled."),
            EventSets.E("CellEditEnded", "DataGridCellEditEndedEventArgs", EventSets.Controls, "Occurs after a cell edit has been committed or cancelled."),
            EventSets.E("BeginningEdit", "DataGridBeginningEditEventArgs", EventSets.Controls, "Occurs before a cell or row enters edit mode."),
            EventSets.E("LoadingRow", "DataGridRowEventArgs", EventSets.Controls, "Occurs when a row is prepared for display."),
            EventSets.E("Sorting", "DataGridColumnEventArgs", EventSets.Controls, "Occurs when a column is being sorted."),
        ], EventSets.Control());
        DefaultEvent = "SelectionChanged";
    }
}
