using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace AVAUIDesigner.Shell.Controls;

/// <summary>Draws error/warning squiggles and matching-tag highlights in the XAML editor.</summary>
public class EditorDecorations : IBackgroundRenderer
{
    public readonly record struct Marker(int Offset, int Length, bool IsError, string Message);

    public List<Marker> Markers { get; } = [];
    public List<(int Offset, int Length)> TagHighlights { get; } = [];

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        var docLength = textView.Document?.TextLength ?? 0;
        var highlight = new SolidColorBrush(Color.FromArgb(60, 30, 127, 255));
        foreach (var (offset, length) in TagHighlights)
        {
            if (offset < 0 || offset + length > docLength) continue;
            foreach (var r in BackgroundGeometryBuilder.GetRectsForSegment(textView, new TextSegment { StartOffset = offset, Length = length }))
                drawingContext.FillRectangle(highlight, r);
        }

        foreach (var m in Markers)
        {
            if (m.Offset < 0 || m.Offset > docLength) continue;
            var length = Math.Max(1, Math.Min(m.Length, docLength - m.Offset));
            if (docLength == 0) continue;
            var pen = new Pen(new SolidColorBrush(m.IsError ? Color.Parse("#E0453A") : Color.Parse("#D9A400")), 1);
            foreach (var r in BackgroundGeometryBuilder.GetRectsForSegment(textView, new TextSegment { StartOffset = m.Offset, Length = length }))
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    var y = r.Bottom - 1;
                    ctx.BeginFigure(new Point(r.Left, y), false);
                    var up = true;
                    for (var x = r.Left + 2; x <= r.Right + 2; x += 2)
                    {
                        ctx.LineTo(new Point(x, up ? y - 2 : y));
                        up = !up;
                    }
                    ctx.EndFigure(false);
                }
                drawingContext.DrawGeometry(null, pen, geometry);
            }
        }
    }

    public string? MessageAt(int offset) =>
        Markers.FirstOrDefault(m => offset >= m.Offset && offset <= m.Offset + Math.Max(1, m.Length)).Message;
}
