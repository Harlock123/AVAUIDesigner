using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AVAUIDesigner.Engine.Properties;

namespace AVAUIDesigner.Shell.Controls;

/// <summary>Miniature drawing of a Grid's rows and columns for the Grid definition editor.</summary>
public class GridThumbnail : Control
{
    public static readonly StyledProperty<IReadOnlyList<string>?> RowValuesProperty =
        AvaloniaProperty.Register<GridThumbnail, IReadOnlyList<string>?>(nameof(RowValues));

    public static readonly StyledProperty<IReadOnlyList<string>?> ColumnValuesProperty =
        AvaloniaProperty.Register<GridThumbnail, IReadOnlyList<string>?>(nameof(ColumnValues));

    static GridThumbnail()
    {
        AffectsRender<GridThumbnail>(RowValuesProperty, ColumnValuesProperty);
    }

    public IReadOnlyList<string>? RowValues
    {
        get => GetValue(RowValuesProperty);
        set => SetValue(RowValuesProperty, value);
    }

    public IReadOnlyList<string>? ColumnValues
    {
        get => GetValue(ColumnValuesProperty);
        set => SetValue(ColumnValuesProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Math.Min(availableSize.Width, 160), 90);

    public override void Render(DrawingContext context)
    {
        var r = new Rect(Bounds.Size).Deflate(1);
        var accent = new SolidColorBrush(Color.Parse("#1E7FFF"));
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(25, 30, 127, 255)), new Pen(accent, 1), r);
        var pen = new Pen(accent, 1, new DashStyle([3, 2], 0));
        foreach (var x in Offsets(ColumnValues, r.Width)) context.DrawLine(pen, new Point(r.X + x, r.Y), new Point(r.X + x, r.Bottom));
        foreach (var y in Offsets(RowValues, r.Height)) context.DrawLine(pen, new Point(r.X, r.Y + y), new Point(r.Right, r.Y + y));
    }

    /// <summary>Divider positions: pixel and Auto sizes are scaled, stars share the rest.</summary>
    private static IEnumerable<double> Offsets(IReadOnlyList<string>? values, double total)
    {
        if (values is null || values.Count < 2) yield break;
        var sizes = new double[values.Count];
        double fixedSum = 0, starSum = 0;
        for (var i = 0; i < values.Count; i++)
        {
            if (!XamlValueConverter.TryDeserialize(values[i], typeof(GridLength), out var o) || o is not GridLength gl) gl = GridLength.Star;
            if (gl.IsStar) starSum += sizes[i] = -Math.Max(gl.Value, 0.01);
            else fixedSum += sizes[i] = gl.IsAuto ? 12 : Math.Min(gl.Value / 4, total / values.Count);
        }
        starSum = -starSum;
        var remaining = Math.Max(0, total - fixedSum);
        var pos = 0.0;
        for (var i = 0; i < sizes.Length - 1; i++)
        {
            pos += sizes[i] < 0 ? remaining * (-sizes[i] / starSum) : sizes[i];
            yield return pos;
        }
    }
}
