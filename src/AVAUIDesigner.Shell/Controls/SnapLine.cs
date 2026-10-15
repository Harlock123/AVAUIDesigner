using Avalonia;

namespace AVAUIDesigner.Shell.Controls;

public enum SnapLineKind
{
    /// <summary>Edges/centers aligned (blue).</summary>
    Alignment,
    /// <summary>Standard margin distance (pink).</summary>
    Spacing,
}

public readonly record struct SnapLine(Point Start, Point End, SnapLineKind Kind);

/// <summary>Which edges of a rectangle are moving (all for a move, some for a resize).</summary>
[Flags]
public enum SnapEdges
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 4,
    Bottom = 8,
    Horizontal = Left | Right,
    Vertical = Top | Bottom,
    All = Horizontal | Vertical,
}

public sealed record SnapOptions(
    double Threshold = 6,
    bool SmartSnap = true,
    bool SnapToGrid = true,
    double GridSize = 8);

public sealed record SnapResult(double DeltaX, double DeltaY, IReadOnlyList<SnapLine> Lines);

/// <summary>Computes snap adjustments against sibling edges, parent edges, standard spacing and the grid.</summary>
public static class SnapEngine
{
    private static readonly double[] StandardSpacings = [8, 16];

    public static SnapResult Snap(Rect proposed, IReadOnlyList<Rect> siblings, Rect? parent, SnapEdges edges,
        SnapOptions options, Point gridOrigin)
    {
        var lines = new List<SnapLine>();
        var dx = SnapAxis(proposed, siblings, parent, edges, options, gridOrigin.X, horizontal: true, out var xTarget);
        var dy = SnapAxis(proposed, siblings, parent, edges, options, gridOrigin.Y, horizontal: false, out var yTarget);
        var snapped = proposed.Translate(new Vector(dx, dy));
        if (xTarget is { } xt) lines.Add(MakeLine(snapped, xt, horizontal: true));
        if (yTarget is { } yt) lines.Add(MakeLine(snapped, yt, horizontal: false));
        return new SnapResult(dx, dy, lines);
    }

    private sealed record Target(double Value, SnapLineKind Kind, Rect Against, double MovingValue, double Anchor = 0);

    private static double SnapAxis(Rect r, IReadOnlyList<Rect> siblings, Rect? parent, SnapEdges edges,
        SnapOptions options, double gridOrigin, bool horizontal, out Target? hit)
    {
        hit = null;
        double Lo(Rect x) => horizontal ? x.Left : x.Top;
        double Hi(Rect x) => horizontal ? x.Right : x.Bottom;
        double Mid(Rect x) => horizontal ? x.Center.X : x.Center.Y;
        bool Overlaps(Rect a, Rect b) => horizontal
            ? a.Top < b.Bottom + 24 && b.Top < a.Bottom + 24
            : a.Left < b.Right + 24 && b.Left < a.Right + 24;

        var movesLo = edges.HasFlag(horizontal ? SnapEdges.Left : SnapEdges.Top);
        var movesHi = edges.HasFlag(horizontal ? SnapEdges.Right : SnapEdges.Bottom);
        if (!movesLo && !movesHi) return 0;

        if (options.SmartSnap)
        {
            var moving = new List<double>();
            if (movesLo) moving.Add(Lo(r));
            if (movesHi) moving.Add(Hi(r));
            if (movesLo && movesHi) moving.Add(Mid(r));

            var candidates = new List<Target>();
            foreach (var s in siblings)
            {
                foreach (var m in moving)
                {
                    candidates.Add(new Target(Lo(s), SnapLineKind.Alignment, s, m));
                    candidates.Add(new Target(Hi(s), SnapLineKind.Alignment, s, m));
                    candidates.Add(new Target(Mid(s), SnapLineKind.Alignment, s, m));
                }
                if (!Overlaps(r, s)) continue;
                foreach (var gap in StandardSpacings)
                {
                    if (movesLo) candidates.Add(new Target(Hi(s) + gap, SnapLineKind.Spacing, s, Lo(r), Hi(s)));
                    if (movesHi) candidates.Add(new Target(Lo(s) - gap, SnapLineKind.Spacing, s, Hi(r), Lo(s)));
                }
            }
            if (parent is { } p)
            {
                foreach (var m in moving)
                {
                    candidates.Add(new Target(Lo(p), SnapLineKind.Alignment, p, m));
                    candidates.Add(new Target(Hi(p), SnapLineKind.Alignment, p, m));
                    candidates.Add(new Target(Mid(p), SnapLineKind.Alignment, p, m));
                }
                foreach (var gap in StandardSpacings)
                {
                    if (movesLo) candidates.Add(new Target(Lo(p) + gap, SnapLineKind.Spacing, p, Lo(r), Lo(p)));
                    if (movesHi) candidates.Add(new Target(Hi(p) - gap, SnapLineKind.Spacing, p, Hi(r), Hi(p)));
                }
            }

            Target? best = null;
            var bestDist = options.Threshold + 0.0001;
            foreach (var c in candidates)
            {
                var d = Math.Abs(c.Value - c.MovingValue);
                // Prefer alignment over spacing at equal distance.
                if (d < bestDist || (best is not null && Math.Abs(d - bestDist) < 0.001 && c.Kind == SnapLineKind.Alignment
                                     && best.Kind == SnapLineKind.Spacing))
                {
                    best = c;
                    bestDist = d;
                }
            }
            if (best is not null)
            {
                hit = best;
                return best.Value - best.MovingValue;
            }
        }

        if (options.SnapToGrid && options.GridSize >= 2)
        {
            var v = movesLo ? Lo(r) : Hi(r);
            var snapped = Math.Round((v - gridOrigin) / options.GridSize) * options.GridSize + gridOrigin;
            return snapped - v;
        }
        return 0;
    }

    private static SnapLine MakeLine(Rect snapped, Target t, bool horizontal)
    {
        var a = t.Against;
        if (t.Kind == SnapLineKind.Alignment)
        {
            if (horizontal)
            {
                var top = Math.Min(snapped.Top, a.Top);
                var bottom = Math.Max(snapped.Bottom, a.Bottom);
                return new SnapLine(new Point(t.Value, top), new Point(t.Value, bottom), SnapLineKind.Alignment);
            }
            var left = Math.Min(snapped.Left, a.Left);
            var right = Math.Max(snapped.Right, a.Right);
            return new SnapLine(new Point(left, t.Value), new Point(right, t.Value), SnapLineKind.Alignment);
        }

        // Spacing: a short line across the gap, in the middle of the overlapping range.
        if (horizontal)
        {
            var top = Math.Max(snapped.Top, a.Top);
            var bottom = Math.Min(snapped.Bottom, a.Bottom);
            var y = bottom > top ? (top + bottom) / 2 : snapped.Center.Y;
            return new SnapLine(new Point(t.Anchor, y), new Point(t.Value, y), SnapLineKind.Spacing);
        }
        var l = Math.Max(snapped.Left, a.Left);
        var rr = Math.Min(snapped.Right, a.Right);
        var x = rr > l ? (l + rr) / 2 : snapped.Center.X;
        return new SnapLine(new Point(x, t.Anchor), new Point(x, t.Value), SnapLineKind.Spacing);
    }
}
