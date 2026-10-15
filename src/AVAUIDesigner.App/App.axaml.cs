using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AVAUIDesigner.Engine.Settings;
using AVAUIDesigner.Shell;
using AVAUIDesigner.Shell.Services;
using AVAUIDesigner.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AVAUIDesigner.App;

public partial class App : Application
{
    private RecoveryService? _recovery;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = Program.Services ?? Program.BuildServices();
            var settings = services.GetRequiredService<DesignerSettings>();
            ThemeService.Apply(settings.Ui.Theme);

            var vm = services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow(vm);
            desktop.MainWindow = window;

            _recovery = services.GetRequiredService<RecoveryService>();
            var file = desktop.Args?.FirstOrDefault(a => !a.StartsWith('-'));
            vm.Initialize(file);
            vm.OfferRecovery(_recovery);
            _recovery.Start(vm.GetRecoverySnapshot);

            // Crash recovery: write the current document before an unhandled exception takes the app down.
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                _recovery.SaveNow();
                vm.StatusMessage = $"Unexpected error: {e.Exception.Message}";
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (_, _) => _recovery.SaveNow();

            desktop.ShutdownRequested += (_, _) =>
            {
                _recovery.Dispose();
                _recovery.Clear();
                vm.Dispose();
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
