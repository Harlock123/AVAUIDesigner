using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Properties;

namespace AVAUIDesigner.Shell.Design;

/// <summary>
/// Translates surface geometry into XAML placement values for each container layout mode:
/// Canvas → Canvas.Left/Top; Grid → Grid.Row/Column + Margin (left/top aligned); ordered panels → insertion index.
/// </summary>
public class PlacementService
{
    private static readonly string[] CanvasKeys = ["Canvas.Left", "Canvas.Top", "Canvas.Right", "Canvas.Bottom"];
    private static readonly string[] GridKeys = ["Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan"];
    private static readonly string[] DockKeys = ["DockPanel.Dock"];

    private readonly Func<DesignElement, Rect?> _bounds;
    private readonly Func<DesignElement, Control?> _control;

    public PlacementService(Func<DesignElement, Rect?> bounds, Func<DesignElement, Control?> control)
    {
        _bounds = bounds;
        _control = control;
    }

    public static ContainerLayoutMode ModeOf(DesignElement? container) =>
        container?.Descriptor?.LayoutMode ?? ContainerLayoutMode.None;

    /// <summary>Canvas and Grid position children by coordinates.</summary>
    public static bool IsAbsolute(DesignElement? container) =>
        ModeOf(container) is ContainerLayoutMode.Canvas or ContainerLayoutMode.Grid;

    /// <summary>Stack, Wrap, Dock and Items containers position children by order.</summary>
    public static bool IsOrdered(DesignElement? container) =>
        ModeOf(container) is ContainerLayoutMode.Stack or ContainerLayoutMode.Wrap or ContainerLayoutMode.Dock or ContainerLayoutMode.Items;

    /// <summary>Values placing <paramref name="element"/> at <paramref name="rect"/> (surface coordinates) inside <paramref name="container"/>.</summary>
    public Dictionary<string, string?> PlaceAt(DesignElement container, DesignElement element, Rect rect)
    {
        var values = new Dictionary<string, string?>();
        var cb = _bounds(container);
        if (cb is null) return values;
        var origin = cb.Value.TopLeft;

        switch (ModeOf(container))
        {
            case ContainerLayoutMode.Canvas:
                values["Canvas.Left"] = Fmt(rect.X - origin.X);
                values["Canvas.Top"] = Fmt(rect.Y - origin.Y);
                break;

            case ContainerLayoutMode.Grid:
            {
                var (row, col, cell) = GridCellAt(container, new Point(rect.X + Math.Min(4, rect.Width / 2), rect.Y + Math.Min(4, rect.Height / 2)));
                values["Grid.Row"] = row == 0 ? null : row.ToString();
                values["Grid.Column"] = col == 0 ? null : col.ToString();
                values["Margin"] = XamlValueConverter.Serialize(
                    new Thickness(Math.Round(rect.X - cell.X), Math.Round(rect.Y - cell.Y), 0, 0), typeof(Thickness));
                if (values["Margin"] == "0") values["Margin"] = null;
                // Absolute-style placement inside a cell needs left/top alignment and an explicit size.
                var hAlign = element.GetValue("HorizontalAlignment");
                var vAlign = element.GetValue("VerticalAlignment");
                if (element.GetValue("Width") is null && hAlign is null or "Stretch") values["Width"] = Fmt(rect.Width);
                if (element.GetValue("Height") is null && vAlign is null or "Stretch") values["Height"] = Fmt(rect.Height);
                values["HorizontalAlignment"] = "Left";
                values["VerticalAlignment"] = "Top";
                break;
            }
        }
        return values;
    }

    /// <summary>Removes attached properties that only made sense in the previous parent.</summary>
    public static Dictionary<string, string?> CleanupForNewParent(DesignElement element, DesignElement newParent)
    {
        var values = new Dictionary<string, string?>();
        var newMode = ModeOf(newParent);
        if (newMode != ContainerLayoutMode.Canvas)
            foreach (var k in CanvasKeys) if (element.GetValue(k) is not null) values[k] = null;
        if (newMode != ContainerLayoutMode.Grid)
            foreach (var k in GridKeys) if (element.GetValue(k) is not null) values[k] = null;
        if (newMode != ContainerLayoutMode.Dock)
            foreach (var k in DockKeys) if (element.GetValue(k) is not null) values[k] = null;

        // Leaving a Grid: drop the designer's absolute-style placement (left/top aligned + margin).
        if (ModeOf(element.Parent) == ContainerLayoutMode.Grid && newMode != ContainerLayoutMode.Grid
            && element.GetValue("HorizontalAlignment") == "Left" && element.GetValue("VerticalAlignment") == "Top")
        {
            values["Margin"] = null;
            values["HorizontalAlignment"] = null;
            values["VerticalAlignment"] = null;
        }
        return values;
    }

    /// <summary>The Grid cell containing <paramref name="p"/> and its bounds.</summary>
    public (int Row, int Column, Rect Cell) GridCellAt(DesignElement grid, Point p)
    {
        var gb = _bounds(grid) ?? default;
        if (_control(grid) is not Grid g) return (0, 0, gb);

        var (col, x, w) = Locate(g.ColumnDefinitions.Select(c => c.ActualWidth).ToList(), p.X - gb.X, gb.Width);
        var (row, y, h) = Locate(g.RowDefinitions.Select(r => r.ActualHeight).ToList(), p.Y - gb.Y, gb.Height);
        return (row, col, new Rect(gb.X + x, gb.Y + y, w, h));
    }

    /// <summary>All cell rectangles of a Grid (for drawing).</summary>
    public IEnumerable<Rect> GridCells(DesignElement grid)
    {
        var gb = _bounds(grid) ?? default;
        if (_control(grid) is not Grid g) yield break;
        var cols = g.ColumnDefinitions.Count == 0 ? [gb.Width] : g.ColumnDefinitions.Select(c => c.ActualWidth).ToList();
        var rows = g.RowDefinitions.Count == 0 ? [gb.Height] : g.RowDefinitions.Select(r => r.ActualHeight).ToList();
        var y = gb.Y;
        foreach (var h in rows)
        {
            var x = gb.X;
            foreach (var w in cols)
            {
                yield return new Rect(x, y, w, h);
                x += w;
            }
            y += h;
        }
    }

    private static (int Index, double Offset, double Size) Locate(List<double> sizes, double pos, double total)
    {
        if (sizes.Count == 0) return (0, 0, total);
        var offset = 0.0;
        for (var i = 0; i < sizes.Count; i++)
        {
            if (pos < offset + sizes[i] || i == sizes.Count - 1) return (i, offset, sizes[i]);
            offset += sizes[i];
        }
        return (0, 0, total);
    }

    /// <summary>
    /// Insertion index within an ordered container for a drop at <paramref name="p"/>, plus the
    /// insertion line to draw. The index refers to the current child list (including <paramref name="moving"/>).
    /// </summary>
    public int InsertionIndex(DesignElement container, Point p, DesignElement? moving, out (Point Start, Point End)? line)
    {
        line = null;
        var cb = _bounds(container);
        var children = container.Children.Where(c => !ReferenceEquals(c, moving))
            .Select(c => (Element: c, Bounds: _bounds(c)))
            .Where(c => c.Bounds is { Width: > 0 } or { Height: > 0 })
            .Select(c => (c.Element, Bounds: c.Bounds!.Value)).ToList();
        if (children.Count == 0 || cb is null)
        {
            return container.Children.Count;
        }

        var vertical = IsVerticalStack(container);
        if (vertical is not null)
        {
            foreach (var (element, b) in children)
            {
                var before = vertical.Value ? p.Y < b.Center.Y : p.X < b.Center.X;
                if (!before) continue;
                line = vertical.Value
                    ? (new Point(cb.Value.Left, b.Top - 1), new Point(cb.Value.Right, b.Top - 1))
                    : (new Point(b.Left - 1, cb.Value.Top), new Point(b.Left - 1, cb.Value.Bottom));
                return container.Children.IndexOf(element);
            }
            var last = children[^1].Bounds;
            line = vertical.Value
                ? (new Point(cb.Value.Left, last.Bottom + 1), new Point(cb.Value.Right, last.Bottom + 1))
                : (new Point(last.Right + 1, cb.Value.Top), new Point(last.Right + 1, cb.Value.Bottom));
            return container.Children.Count;
        }

        // Wrap/Dock/Items: nearest child, before or after it horizontally.
        var nearest = children.OrderBy(c => Distance(c.Bounds, p)).First();
        var after = p.X > nearest.Bounds.Center.X;
        var x = after ? nearest.Bounds.Right + 1 : nearest.Bounds.Left - 1;
        line = (new Point(x, nearest.Bounds.Top), new Point(x, nearest.Bounds.Bottom));
        var idx = container.Children.IndexOf(nearest.Element);
        return after ? idx + 1 : idx;
    }

    /// <summary>True = vertical stack, false = horizontal stack, null = not a simple stack.</summary>
    private bool? IsVerticalStack(DesignElement container)
    {
        switch (_control(container))
        {
            case StackPanel sp:
                return sp.Orientation == Orientation.Vertical;
            case ItemsControl ic when ModeOf(container) == ContainerLayoutMode.Items:
                // Menus are horizontal, TabControl strips are horizontal for Top/Bottom placement.
                if (ic is Menu) return false;
                if (ic is TabControl tc) return tc.TabStripPlacement is Dock.Left or Dock.Right;
                return true;
        }
        if (ModeOf(container) == ContainerLayoutMode.Stack)
            return container.GetValue("Orientation") != "Horizontal";
        return null;
    }

    private static double Distance(Rect r, Point p)
    {
        var dx = Math.Max(Math.Max(r.Left - p.X, 0), p.X - r.Right);
        var dy = Math.Max(Math.Max(r.Top - p.Y, 0), p.Y - r.Bottom);
        return dx * dx + dy * dy;
    }

    public static string Fmt(double v) => XamlValueConverter.FormatDouble(Math.Round(v));
}
