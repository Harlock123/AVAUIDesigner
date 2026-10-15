using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using AVAUIDesigner.Controls;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Serialization;
using AVAUIDesigner.Engine.Settings;
using AVAUIDesigner.Shell.Controls;
using AVAUIDesigner.Shell.ViewModels;

namespace AVAUIDesigner.Shell.Tests;

internal static class TestHelpers
{
    public static readonly ControlDescriptorRegistry Registry = StandardControlDescriptors.CreateRegistry();

    public static string ScratchDir
    {
        get
        {
            var dir = Path.Combine(Path.GetTempPath(), "AVAUIDesigner.Shell.Tests");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static DesignerSettings Settings() => new()
    {
        Design = { SnapToGrid = false, SmartSnapLines = false, AutoNameNewControls = true },
    };

    public static MainWindowViewModel CreateViewModel(DesignerSettings? settings = null) =>
        new(Registry, new XamlParser(), new XamlSerializer(), new CodeBehindGenerator(), new ViewModelGenerator(),
            settings ?? Settings());

    public const string CanvasDocument = """
        <Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                x:Class="MyApp.Views.TestWindow" Width="600" Height="400">
            <Canvas>
                <Button x:Name="btn1" Canvas.Left="40" Canvas.Top="40" Width="100" Height="30" Content="One" />
                <Button x:Name="btn2" Canvas.Left="200" Canvas.Top="100" Width="100" Height="30" Content="Two" />
            </Canvas>
        </Window>
        """;

    /// <summary>A canvas in a window, loaded with <paramref name="xaml"/>, wired to a selection manager and undo stack.</summary>
    public static (Window Window, DesignCanvas Canvas, DesignDocument Doc, Engine.Selection.SelectionManager Selection, Engine.UndoRedo.UndoRedoStack Undo)
        CreateCanvas(string xaml = CanvasDocument)
    {
        var doc = new XamlParser().Parse(xaml, Registry);
        var selection = new Engine.Selection.SelectionManager { Document = doc };
        var undo = new Engine.UndoRedo.UndoRedoStack { Document = doc };
        var canvas = new DesignCanvas { Registry = Registry, SnapToGrid = false, SmartSnap = false, ShowGrid = false };
        canvas.SelectionManager = selection;
        canvas.Document = doc;
        canvas.ActionRequested += (_, a) =>
        {
            if (a.Command is not null) undo.Execute(a.Command);
            if (a.SelectAfter is { } sel) selection.SetSelection(sel);
        };
        undo.CommandApplied += (_, _) => canvas.InvalidateDesign();
        var window = new Window { Width = 1000, Height = 700, Content = canvas };
        window.Show();
        Pump();
        return (window, canvas, doc, selection, undo);
    }

    /// <summary>Runs pending layout/render/dispatcher work.</summary>
    public static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Converts a surface point to window coordinates.</summary>
    public static Point ToWindow(DesignCanvas canvas, Window window, Point surfacePoint) =>
        canvas.Surface.TranslatePoint(surfacePoint, window) ?? throw new InvalidOperationException("Surface is not in the window.");

    public static Point CenterOf(DesignCanvas canvas, Window window, DesignElement element)
    {
        var b = canvas.GetElementBounds(element) ?? throw new InvalidOperationException($"{element} is not visible.");
        return ToWindow(canvas, window, b.Center);
    }

    public static void Drag(Window window, Point from, Point to, RawInputModifiers modifiers = RawInputModifiers.None, int steps = 5)
    {
        window.MouseMove(from, modifiers);
        window.MouseDown(from, MouseButton.Left, modifiers);
        for (var i = 1; i <= steps; i++)
        {
            var p = new Point(from.X + (to.X - from.X) * i / steps, from.Y + (to.Y - from.Y) * i / steps);
            window.MouseMove(p, modifiers | RawInputModifiers.LeftMouseButton);
            Pump();
        }
        window.MouseUp(to, MouseButton.Left, modifiers);
        Pump();
    }

    public static void Click(Window window, Point p, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.MouseMove(p, modifiers);
        window.MouseDown(p, MouseButton.Left, modifiers);
        window.MouseUp(p, MouseButton.Left, modifiers);
        Pump();
    }

    public static string SaveFrame(Window window, string name)
    {
        Pump();
        var frame = window.CaptureRenderedFrame();
        var path = Path.Combine(ScratchDir, name + ".png");
        frame?.Save(path);
        return path;
    }
}
