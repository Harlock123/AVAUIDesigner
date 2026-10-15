using Avalonia;
using Avalonia.Controls;
using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;
using static AVAUIDesigner.Controls.Common.PropertySets;
using C = AVAUIDesigner.Engine.Descriptors.PropertyCategories;

namespace AVAUIDesigner.Controls.Descriptors;

[ControlDescriptor]
public sealed class GridSplitterDescriptor : StandardDescriptor
{
    // Note: GridSplitter's static constructor needs a running platform (cursor factory),
    // so only typeof(GridSplitter) may be used here — never its static members.
    public GridSplitterDescriptor() : base("GridSplitter", typeof(GridSplitter), ToolboxCategories.Layout)
    {
        Description = "Lets the user resize Grid rows or columns by dragging.";
        DefaultSize = new Size(5, 100);
        MinimumSize = new Size(1, 1);
        Properties = Combine(
        [
            P("ResizeDirection", typeof(GridResizeDirection), C.Behavior, GridResizeDirection.Auto, "Whether rows, columns or (Auto) the inferred direction is resized."),
            P("ResizeBehavior", typeof(GridResizeBehavior), C.Behavior, GridResizeBehavior.BasedOnAlignment, "Which rows or columns are resized."),
            P("ShowsPreview", typeof(bool), C.Behavior, false, "Whether a preview is shown while dragging instead of live resizing."),
            P("KeyboardIncrement", typeof(double), C.Behavior, 10d, "Distance moved per arrow key press."),
            P("DragIncrement", typeof(double), C.Behavior, 1d, "Minimum distance moved per drag step."),
        ], Control(), Templated());
        Events = EventSets.Combine(
        [
            EventSets.E("DragStarted", "VectorEventArgs", EventSets.Input, "Occurs when dragging starts."),
            EventSets.E("DragDelta", "VectorEventArgs", EventSets.Input, "Occurs while dragging."),
            EventSets.E("DragCompleted", "VectorEventArgs", EventSets.Input, "Occurs when dragging ends."),
        ], EventSets.Control());
    }
}
