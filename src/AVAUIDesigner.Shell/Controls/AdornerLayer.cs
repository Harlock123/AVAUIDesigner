using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AVAUIDesigner.Shell.Controls;

/// <summary>One selected element as drawn by the adorner layer.</summary>
public sealed record AdornedSelection(Rect Bounds, bool IsPrimary, string Label, IReadOnlyList<SelectionHandle> Handles, bool IsLocked);

/// <summary>
/// Transparent overlay drawing selection rectangles, resize handles, the container label,
/// snap lines, the rubber band, drop indicators and drag ghosts. It never takes pointer input.
/// All geometry is in design-surface units; strokes are divided by <see cref="Zoom"/> so they stay 1px on screen.
/// </summary>
public class AdornerLayer : Control
{
    private static readonly Color SelectionColor = Color.Parse("#1E7FFF");
    private static readonly Color SpacingColor = Color.Parse("#FF3EA5");
    private static readonly IBrush HandleFill = Brushes.White;
    private static readonly Typeface LabelFace = new(FontFamily.Default);

    public AdornerLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    public double Zoom { get; set; } = 1;
    public IReadOnlyList<AdornedSelection> Selections { get; set; } = [];
    public IReadOnlyList<Rect> Outlines { get; set; } = [];
    public IReadOnlyList<SnapLine> SnapLines { get; set; } = [];
    public IReadOnlyList<Rect> Ghosts { get; set; } = [];
    public Rect? RubberBand { get; set; }
    public (Point Start, Point End)? InsertionLine { get; set; }
    public Rect? DropTarget { get; set; }
    public Rect? DropCell { get; set; }
    public string? GhostLabel { get; set; }

    public override void Render(DrawingContext context)
    {
        var px = 1 / Math.Max(Zoom, 0.05);

        if (Outlines.Count > 0)
        {
            var outlinePen = new Pen(new SolidColorBrush(Color.FromArgb(70, 128, 128, 128)), px, new DashStyle([3, 3], 0));
            foreach (var r in Outlines) context.DrawRectangle(null, outlinePen, r.Deflate(px / 2));
        }

        if (DropTarget is { } target)
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(18, SelectionColor.R, SelectionColor.G, SelectionColor.B)),
                new Pen(new SolidColorBrush(SelectionColor), 2 * px), target);
        }
        if (DropCell is { } cell)
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(40, SelectionColor.R, SelectionColor.G, SelectionColor.B)),
                new Pen(new SolidColorBrush(SelectionColor), px, new DashStyle([4, 2], 0)), cell);
        }

        var selPen = new Pen(new SolidColorBrush(SelectionColor), px, new DashStyle([4, 2], 0));
        var primaryPen = new Pen(new SolidColorBrush(SelectionColor), px);
        foreach (var s in Selections)
        {
            context.DrawRectangle(null, s.IsPrimary ? primaryPen : selPen, s.Bounds);
            if (s.IsPrimary) DrawLabel(context, s.Bounds, s.Label + (s.IsLocked ? " 🔒" : string.Empty), px);
            if (s.IsLocked) continue;
            var size = SelectionHandles.VisualSize * px;
            var handlePen = new Pen(new SolidColorBrush(SelectionColor), px);
            var fill = s.IsPrimary ? HandleFill : new SolidColorBrush(Color.FromArgb(200, 230, 240, 255));
            foreach (var h in s.Handles)
            {
                var c = SelectionHandles.Position(s.Bounds, h);
                context.DrawRectangle(fill, handlePen, new Rect(c.X - size / 2, c.Y - size / 2, size, size));
            }
        }

        if (Ghosts.Count > 0)
        {
            var ghostPen = new Pen(new SolidColorBrush(SelectionColor), px, new DashStyle([2, 2], 0));
            var ghostFill = new SolidColorBrush(Color.FromArgb(28, SelectionColor.R, SelectionColor.G, SelectionColor.B));
            foreach (var g in Ghosts) context.DrawRectangle(ghostFill, ghostPen, g);
            if (GhostLabel is { } gl && Ghosts.Count > 0) DrawLabel(context, Ghosts[0], gl, px);
        }

        if (InsertionLine is { } line)
        {
            var pen = new Pen(new SolidColorBrush(SelectionColor), 2 * px);
            context.DrawLine(pen, line.Start, line.End);
            // Small end caps make the line easier to see.
            var horizontal = Math.Abs(line.End.Y - line.Start.Y) < Math.Abs(line.End.X - line.Start.X);
            var cap = 4 * px;
            foreach (var p in new[] { line.Start, line.End })
            {
                if (horizontal) context.DrawLine(pen, new Point(p.X, p.Y - cap), new Point(p.X, p.Y + cap));
                else context.DrawLine(pen, new Point(p.X - cap, p.Y), new Point(p.X + cap, p.Y));
            }
        }

        foreach (var l in SnapLines)
        {
            var color = l.Kind == SnapLineKind.Alignment ? SelectionColor : SpacingColor;
            context.DrawLine(new Pen(new SolidColorBrush(color), px), l.Start, l.End);
        }

        if (RubberBand is { } band)
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(40, SelectionColor.R, SelectionColor.G, SelectionColor.B)),
                new Pen(new SolidColorBrush(SelectionColor), px), band);
        }
    }

    private static void DrawLabel(DrawingContext context, Rect bounds, string label, double px)
    {
        var text = new FormattedText(label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, LabelFace, 10 * px, Brushes.White);
        var pad = 3 * px;
        var h = text.Height + pad;
        var tab = new Rect(bounds.Left, bounds.Top - h - px, text.Width + pad * 2, h);
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 96, 96, 96)), null, tab, 2 * px, 2 * px);
        context.DrawText(text, new Point(tab.Left + pad, tab.Top + pad / 2));
    }
}
