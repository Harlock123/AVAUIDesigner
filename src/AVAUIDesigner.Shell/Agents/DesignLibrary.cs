using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Mcp;
using AVAUIDesigner.Shell.Design;

namespace AVAUIDesigner.Shell.Agents;

/// <summary>
/// Draws a design as the design surface does (the same <see cref="PreviewBuilder"/>), for render_view and the chat's
/// timeline. Controls only take their look (templates, theme) inside a window, so the picture is built in
/// <paramref name="host"/>: a panel in a window that has no size and clips, so nothing shows on screen.
/// </summary>
public sealed class DesignLibrary(Panel host, ControlDescriptorRegistry registry) : IDesignLibrary
{
    public byte[] Picture(DesignDocument document, double scale, string? baseDirectory)
    {
        var builder = new PreviewBuilder(registry) { BaseDirectory = baseDirectory };
        var frame = builder.BuildRoot(document);
        builder.ApplyImages();
        // Scaled by a layout transform and drawn at 96 DPI (a bitmap's own DPI would scale the picture a second time).
        var scaled = new LayoutTransformControl { Child = frame, LayoutTransform = new ScaleTransform(scale, scale) };
        var stage = new Canvas();
        stage.Children.Add(scaled);
        host.Children.Add(stage);
        try
        {
            scaled.Measure(Size.Infinity);
            var size = scaled.DesiredSize;
            scaled.Arrange(new Rect(size));
            scaled.UpdateLayout();
            var pixels = new PixelSize(Math.Max(1, (int)Math.Ceiling(size.Width)), Math.Max(1, (int)Math.Ceiling(size.Height)));
            using var bitmap = new RenderTargetBitmap(pixels, new Vector(96, 96));
            bitmap.Render(scaled);
            using var png = new MemoryStream();
            bitmap.Save(png);
            return png.ToArray();
        }
        finally
        {
            stage.Children.Clear();
            host.Children.Remove(stage);
        }
    }
}
