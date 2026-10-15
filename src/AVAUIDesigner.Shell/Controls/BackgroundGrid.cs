using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AVAUIDesigner.Shell.Controls;

/// <summary>Dot grid drawn on the root's client area underneath the designed controls.</summary>
public class BackgroundGrid : Control
{
    public static readonly StyledProperty<double> GridSizeProperty =
        AvaloniaProperty.Register<BackgroundGrid, double>(nameof(GridSize), 8);

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<BackgroundGrid, double>(nameof(Zoom), 1);

    static BackgroundGrid()
    {
        AffectsRender<BackgroundGrid>(GridSizeProperty, ZoomProperty);
    }

    public BackgroundGrid()
    {
        IsHitTestVisible = false;
    }

    public double GridSize
    {
        get => GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = GridSize;
        if (size < 2) return;
        // Skip intermediate dots when they would be closer than 6 screen pixels.
        var step = size;
        while (step * Zoom < 6) step *= 2;
        var dot = 1.2 / Math.Max(Zoom, 0.05);
        var brush = new SolidColorBrush(Color.FromArgb(90, 128, 128, 128));
        var b = Bounds;
        for (var x = 0.0; x <= b.Width; x += step)
        for (var y = 0.0; y <= b.Height; y += step)
            context.FillRectangle(brush, new Rect(x - dot / 2, y - dot / 2, dot, dot));
    }
}
