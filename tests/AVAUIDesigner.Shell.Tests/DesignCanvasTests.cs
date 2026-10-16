using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using AVAUIDesigner.Engine.Serialization;
using AVAUIDesigner.Shell.Controls;
using Xunit;

namespace AVAUIDesigner.Shell.Tests;

public class DesignCanvasTests
{
    [AvaloniaFact]
    public void Renders_Elements_AtTheirCanvasPositions()
    {
        var (_, canvas, doc, _, _) = TestHelpers.CreateCanvas();
        var root = canvas.GetElementBounds(doc.RootElement)!.Value;
        var btn1 = canvas.GetElementBounds(doc.NamedElements["btn1"])!.Value;
        Assert.Equal(new Size(600, 400), root.Size);
        Assert.Equal(40, btn1.X - root.X, 1);
        Assert.Equal(40, btn1.Y - root.Y, 1);
        Assert.Equal(new Size(100, 30), btn1.Size);
    }

    [AvaloniaFact]
    public void Click_SelectsElement_CtrlClick_AddsToSelection()
    {
        var (window, canvas, doc, sel, _) = TestHelpers.CreateCanvas();
        TestHelpers.Click(window, TestHelpers.CenterOf(canvas, window, doc.NamedElements["btn1"]));
        Assert.Same(doc.NamedElements["btn1"], sel.PrimarySelection);
        Assert.Single(sel.SelectedElements);

        TestHelpers.Click(window, TestHelpers.CenterOf(canvas, window, doc.NamedElements["btn2"]), RawInputModifiers.Control);
        Assert.Equal(2, sel.SelectedElements.Count);
        Assert.Same(doc.NamedElements["btn2"], sel.PrimarySelection);
    }

    [AvaloniaFact]
    public void Drag_MovesElement_AndUndoRestores()
    {
        var (window, canvas, doc, _, undo) = TestHelpers.CreateCanvas();
        var btn = doc.NamedElements["btn1"];
        var from = TestHelpers.CenterOf(canvas, window, btn);
        TestHelpers.Drag(window, from, from + new Point(50, 30));

        Assert.Equal("90", btn.GetValue("Canvas.Left"));
        Assert.Equal("70", btn.GetValue("Canvas.Top"));
        Assert.True(undo.CanUndo);
        Assert.StartsWith("Move", undo.UndoDescription);

        undo.Undo();
        Assert.Equal("40", btn.GetValue("Canvas.Left"));
        Assert.Equal("40", btn.GetValue("Canvas.Top"));
    }

    [AvaloniaFact]
    public void Drag_WithSnapToGrid_SnapsPosition()
    {
        var (window, canvas, doc, _, _) = TestHelpers.CreateCanvas();
        canvas.SnapToGrid = true;
        canvas.GridSize = 8;
        var btn = doc.NamedElements["btn1"];
        var from = TestHelpers.CenterOf(canvas, window, btn);
        TestHelpers.Drag(window, from, from + new Point(13, 21));
        Assert.Equal(0, int.Parse(btn.GetValue("Canvas.Left")!) % 8);
        Assert.Equal(0, int.Parse(btn.GetValue("Canvas.Top")!) % 8);
    }

    [AvaloniaFact]
    public void SmartSnap_AlignsToSiblingEdge()
    {
        var (window, canvas, doc, _, _) = TestHelpers.CreateCanvas();
        canvas.SmartSnap = true;
        var btn2 = doc.NamedElements["btn2"];
        var from = TestHelpers.CenterOf(canvas, window, btn2);
        // btn1 left edge is 40; dragging btn2 (left 200) by -157 puts it at 43 → snaps to 40.
        TestHelpers.Drag(window, from, from + new Point(-157, 0));
        Assert.Equal("40", btn2.GetValue("Canvas.Left"));
    }

    [AvaloniaFact]
    public void ResizeHandle_ChangesSize()
    {
        var (window, canvas, doc, sel, undo) = TestHelpers.CreateCanvas();
        var btn = doc.NamedElements["btn1"];
        sel.SetSelection(btn);
        TestHelpers.Pump();
        var b = canvas.GetElementBounds(btn)!.Value;
        var handle = TestHelpers.ToWindow(canvas, window, b.BottomRight);
        TestHelpers.Drag(window, handle, handle + new Point(40, 20));
        Assert.Equal("140", btn.GetValue("Width"));
        Assert.Equal("50", btn.GetValue("Height"));
        Assert.StartsWith("Resize", undo.UndoDescription);
    }

    [AvaloniaFact]
    public void ResizeFromLeft_MovesAndResizes_RespectingMinimum()
    {
        var (window, canvas, doc, sel, _) = TestHelpers.CreateCanvas();
        var btn = doc.NamedElements["btn1"];
        sel.SetSelection(btn);
        TestHelpers.Pump();
        var b = canvas.GetElementBounds(btn)!.Value;
        var handle = TestHelpers.ToWindow(canvas, window, new Point(b.Left, b.Center.Y));
        TestHelpers.Drag(window, handle, handle + new Point(500, 0)); // far past the right edge
        var width = double.Parse(btn.GetValue("Width")!);
        Assert.True(width >= btn.Descriptor!.MinimumSize.Width);
        Assert.Equal(140 - width, double.Parse(btn.GetValue("Canvas.Left")!), 1);
    }

    [AvaloniaFact]
    public void RubberBand_SelectsIntersectingElements()
    {
        var (window, canvas, doc, sel, _) = TestHelpers.CreateCanvas();
        var root = canvas.GetElementBounds(doc.RootElement)!.Value;
        var from = TestHelpers.ToWindow(canvas, window, root.TopLeft + new Point(10, 10));
        var to = TestHelpers.ToWindow(canvas, window, root.TopLeft + new Point(320, 140));
        TestHelpers.Drag(window, from, to);
        Assert.Equal(2, sel.SelectedElements.Count);
    }

    [AvaloniaFact]
    public void ToolboxDrop_AddsElementAtDropPoint()
    {
        var (window, canvas, doc, sel, undo) = TestHelpers.CreateCanvas();
        var root = canvas.GetElementBounds(doc.RootElement)!.Value;
        var target = TestHelpers.ToWindow(canvas, window, root.TopLeft + new Point(300, 250));
#pragma warning disable CS0618
        var data = new DataObject();
        data.Set(DesignCanvas.DescriptorDataFormat, "TextBox");
        window.DragDrop(target, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        window.DragDrop(target, RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
        window.DragDrop(target, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);
#pragma warning restore CS0618
        TestHelpers.Pump();

        var canvasElement = doc.RootElement.Children[0];
        var added = canvasElement.Children.Last();
        Assert.Equal("TextBox", added.ElementType);
        Assert.Equal("300", added.GetValue("Canvas.Left"));
        Assert.Equal("250", added.GetValue("Canvas.Top"));
        Assert.Equal("120", added.GetValue("Width"));
        Assert.Same(added, sel.PrimarySelection);
        undo.Undo();
        Assert.DoesNotContain(added, canvasElement.Children);
    }

    [AvaloniaFact]
    public void Drag_IntoStackPanel_ReparentsAndClearsCanvasPosition()
    {
        const string xaml = """
            <Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="600" Height="400">
                <Canvas>
                    <Button x:Name="btn" Canvas.Left="20" Canvas.Top="20" Width="80" Height="30" Content="B" />
                    <StackPanel x:Name="stack" Canvas.Left="200" Canvas.Top="100" Width="200" Height="200" Background="LightGray">
                        <TextBlock Text="first" />
                    </StackPanel>
                </Canvas>
            </Window>
            """;
        var (window, canvas, doc, _, _) = TestHelpers.CreateCanvas(xaml);
        var btn = doc.NamedElements["btn"];
        var stack = doc.NamedElements["stack"];
        var from = TestHelpers.CenterOf(canvas, window, btn);
        var stackBounds = canvas.GetElementBounds(stack)!.Value;
        TestHelpers.Drag(window, from, TestHelpers.ToWindow(canvas, window, new Point(stackBounds.Center.X, stackBounds.Bottom - 10)));

        Assert.Same(stack, btn.Parent);
        Assert.Equal(1, btn.IndexInParent);
        Assert.Null(btn.GetValue("Canvas.Left"));
        Assert.Null(btn.GetValue("Canvas.Top"));
    }

    [AvaloniaFact]
    public void Drag_InGrid_SetsCellAndMargin()
    {
        const string xaml = """
            <Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="600" Height="400">
                <Grid ColumnDefinitions="*,*" RowDefinitions="*,*">
                    <Button x:Name="btn" Width="80" Height="30" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="10,10,0,0" Content="B" />
                </Grid>
            </Window>
            """;
        var (window, canvas, doc, _, _) = TestHelpers.CreateCanvas(xaml);
        var btn = doc.NamedElements["btn"];
        var from = TestHelpers.CenterOf(canvas, window, btn);
        TestHelpers.Drag(window, from, from + new Point(320, 220)); // into column 1, row 1
        Assert.Equal("1", btn.GetValue("Grid.Column"));
        Assert.Equal("1", btn.GetValue("Grid.Row"));
        Assert.Equal("30,30,0,0", btn.GetValue("Margin"));
    }

    [AvaloniaFact]
    public void Escape_CancelsDrag()
    {
        var (window, canvas, doc, _, undo) = TestHelpers.CreateCanvas();
        var btn = doc.NamedElements["btn1"];
        var from = TestHelpers.CenterOf(canvas, window, btn);
        window.MouseMove(from);
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(from + new Point(60, 60), RawInputModifiers.LeftMouseButton);
        TestHelpers.Pump();
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        window.MouseUp(from + new Point(60, 60), MouseButton.Left);
        TestHelpers.Pump();
        Assert.Equal("40", btn.GetValue("Canvas.Left"));
        Assert.False(undo.CanUndo);
    }

    [AvaloniaFact]
    public void UnknownElements_RenderAsPlaceholders_AndCanBeSelected()
    {
        const string xaml = """
            <Window xmlns="https://github.com/avaloniaui" xmlns:local="clr-namespace:My.Controls" Width="400" Height="300">
                <Canvas>
                    <local:Gauge Canvas.Left="50" Canvas.Top="50" Width="120" Height="60" Value="3" />
                </Canvas>
            </Window>
            """;
        var (window, canvas, doc, sel, _) = TestHelpers.CreateCanvas(xaml);
        var gauge = doc.RootElement.Children[0].Children[0];
        Assert.True(gauge.IsUnknown);
        TestHelpers.Click(window, TestHelpers.CenterOf(canvas, window, gauge));
        Assert.Same(gauge, sel.PrimarySelection);
        Assert.Contains("local:Gauge", new XamlSerializer().Serialize(doc));
    }
}
