using Avalonia;

namespace AVAUIDesigner.Shell.Controls;

/// <summary>The 8 resize handles around a selected element.</summary>
public enum SelectionHandle
{
    None,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
}

public static class SelectionHandles
{
    /// <summary>Visual size in screen pixels.</summary>
    public const double VisualSize = 6;

    /// <summary>Hit area in screen pixels (larger than the visual for easier grabbing).</summary>
    public const double HitSize = 12;

    public static readonly SelectionHandle[] All =
    [
        SelectionHandle.TopLeft, SelectionHandle.Top, SelectionHandle.TopRight, SelectionHandle.Right,
        SelectionHandle.BottomRight, SelectionHandle.Bottom, SelectionHandle.BottomLeft, SelectionHandle.Left,
    ];

    /// <summary>Only the right/bottom handles: used for the root element.</summary>
    public static readonly SelectionHandle[] RootOnly = [SelectionHandle.Right, SelectionHandle.BottomRight, SelectionHandle.Bottom];

    public static Point Position(Rect r, SelectionHandle h) => h switch
    {
        SelectionHandle.TopLeft => r.TopLeft,
        SelectionHandle.Top => new Point(r.Center.X, r.Top),
        SelectionHandle.TopRight => r.TopRight,
        SelectionHandle.Right => new Point(r.Right, r.Center.Y),
        SelectionHandle.BottomRight => r.BottomRight,
        SelectionHandle.Bottom => new Point(r.Center.X, r.Bottom),
        SelectionHandle.BottomLeft => r.BottomLeft,
        SelectionHandle.Left => new Point(r.Left, r.Center.Y),
        _ => r.Center,
    };

    /// <summary>Hit-tests the handles of <paramref name="r"/>; sizes are in surface units already scaled by zoom.</summary>
    public static SelectionHandle HitTest(Rect r, Point p, double hitSize, IEnumerable<SelectionHandle> handles)
    {
        foreach (var h in handles)
        {
            var c = Position(r, h);
            if (Math.Abs(p.X - c.X) <= hitSize / 2 && Math.Abs(p.Y - c.Y) <= hitSize / 2) return h;
        }
        return SelectionHandle.None;
    }

    public static SnapEdges MovingEdges(SelectionHandle h) => h switch
    {
        SelectionHandle.TopLeft => SnapEdges.Top | SnapEdges.Left,
        SelectionHandle.Top => SnapEdges.Top,
        SelectionHandle.TopRight => SnapEdges.Top | SnapEdges.Right,
        SelectionHandle.Right => SnapEdges.Right,
        SelectionHandle.BottomRight => SnapEdges.Bottom | SnapEdges.Right,
        SelectionHandle.Bottom => SnapEdges.Bottom,
        SelectionHandle.BottomLeft => SnapEdges.Bottom | SnapEdges.Left,
        SelectionHandle.Left => SnapEdges.Left,
        _ => SnapEdges.None,
    };

    /// <summary>Applies a pointer delta to a rectangle for the given handle, enforcing a minimum size.</summary>
    public static Rect Resize(Rect r, SelectionHandle h, Vector delta, Size min)
    {
        var edges = MovingEdges(h);
        double left = r.Left, top = r.Top, right = r.Right, bottom = r.Bottom;
        if (edges.HasFlag(SnapEdges.Left)) left = Math.Min(left + delta.X, right - min.Width);
        if (edges.HasFlag(SnapEdges.Right)) right = Math.Max(right + delta.X, left + min.Width);
        if (edges.HasFlag(SnapEdges.Top)) top = Math.Min(top + delta.Y, bottom - min.Height);
        if (edges.HasFlag(SnapEdges.Bottom)) bottom = Math.Max(bottom + delta.Y, top + min.Height);
        return new Rect(new Point(left, top), new Point(right, bottom));
    }

    public static Avalonia.Input.StandardCursorType Cursor(SelectionHandle h) => h switch
    {
        SelectionHandle.TopLeft or SelectionHandle.BottomRight => Avalonia.Input.StandardCursorType.TopLeftCorner,
        SelectionHandle.TopRight or SelectionHandle.BottomLeft => Avalonia.Input.StandardCursorType.TopRightCorner,
        SelectionHandle.Top or SelectionHandle.Bottom => Avalonia.Input.StandardCursorType.SizeNorthSouth,
        SelectionHandle.Left or SelectionHandle.Right => Avalonia.Input.StandardCursorType.SizeWestEast,
        _ => Avalonia.Input.StandardCursorType.Arrow,
    };
}
