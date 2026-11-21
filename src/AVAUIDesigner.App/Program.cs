using Avalonia;
using AVAUIDesigner.Controls;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Plugins;
using AVAUIDesigner.Engine.Serialization;
using AVAUIDesigner.Engine.Settings;
using AVAUIDesigner.Shell.Services;
using AVAUIDesigner.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia;

namespace AVAUIDesigner.App;

internal static class Program
{
    public static IServiceProvider? Services { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        // AVAUIDesigner --mcp: the MCP server for AI agents, with no window (see AgentMain).
        if (AVAUIDesigner.Mcp.McpCommandLine.Wanted(args)) return AgentMain.Run(args);
        Services = BuildServices();
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // Engine
        services.AddSingleton(_ => StandardControlDescriptors.CreateRegistry());
        services.AddSingleton<XamlParser>();
        services.AddSingleton<XamlSerializer>();
        services.AddSingleton<CodeBehindGenerator>();
        services.AddSingleton<ViewModelGenerator>();
        services.AddSingleton(_ => DesignerSettings.Load());

        // Shell
        services.AddTransient<MainWindowViewModel>();
        services.AddSingleton<RecoveryService>();

        // Plugin loading
        services.AddSingleton<IPluginLoader, FileSystemPluginLoader>();

        return services.BuildServiceProvider();
    }

    // Used by the visual designer / previewer as well.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI(_ => { });
}
