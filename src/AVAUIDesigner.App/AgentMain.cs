using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using AVAUIDesigner.Controls;
using AVAUIDesigner.Mcp;
using AVAUIDesigner.Shell.Agents;

namespace AVAUIDesigner.App;

/// <summary>Runs work on Avalonia's UI thread, where designs are built and drawn.</summary>
internal sealed class AvaloniaThread : IUiThread
{
    public Task<T> InvokeAsync<T>(Func<Task<T>> work) => Dispatcher.UIThread.InvokeAsync(work);
}

/// <summary>
/// <c>AVAUIDesigner --mcp</c>: the MCP server with no window. Avalonia starts on its headless platform with Skia (so
/// designs draw exactly as on the design surface), its dispatcher runs on this thread, and the server runs beside it
/// until the client goes away or Ctrl+C.
/// </summary>
internal static class AgentMain
{
    public static int Run(string[] args)
    {
        AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        if (Application.Current is { } app) app.RequestedThemeVariant = ThemeVariant.Light;
        var registry = StandardControlDescriptors.CreateRegistry();
        // Designs are drawn inside a window (controls take their look only there); this one is never shown on a screen.
        var stage = new Panel();
        var window = new Window { Width = 10, Height = 10, Content = stage };
        window.Show();
        var library = new DesignLibrary(stage, registry);
        var connections = new ConnectionStore(Path.Combine(AgentSettings.Folder, ConnectionStore.FileName));
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Cancel();
        };
        var input = Console.OpenStandardInput();
        var output = Console.OpenStandardOutput();
        var server = Task.Run(() => McpCommandLine.RunAsync(args, () => library, registry, connections, new AvaloniaThread(), input, output, Console.Error, stop.Token));
        server.ContinueWith(_ => Dispatcher.UIThread.Post(() => stop.Cancel()), TaskScheduler.Default);
        Dispatcher.UIThread.MainLoop(stop.Token);
        try
        {
            return server.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"The MCP server stopped: {e.Message}");
            return 1;
        }
    }
}
