using Avalonia.Headless.XUnit;
using AVAUIDesigner.Shell.Services;
using Xunit;

namespace AVAUIDesigner.Shell.Tests;

/// <summary>Renders the main window (written to the temp folder for visual inspection) and checks it does not throw.</summary>
public class ScreenshotTests
{
    [AvaloniaFact]
    public void MainWindow_Renders_WithSampleDocument()
    {
        var vm = TestHelpers.CreateViewModel();
        var window = new MainWindow(vm) { Width = 1500, Height = 950 };
        window.Show();
        vm.LoadText(TestHelpers.CanvasDocument, string.Empty);
        TestHelpers.Pump();
        vm.SelectionManager.SetSelection(vm.CurrentDocument!.NamedElements["btn1"]);
        TestHelpers.Pump();
        var path = TestHelpers.SaveFrame(window, "mainwindow");
        Assert.True(File.Exists(path));
    }

    [AvaloniaFact]
    public void MainWindow_Renders_DarkTheme_AllControls()
    {
        ThemeService.Apply("FluentDark");
        try
        {
            var vm = TestHelpers.CreateViewModel();
            var window = new MainWindow(vm) { Width = 1500, Height = 950 };
            window.Show();
            vm.NewDocumentAsync(NewDocumentKind.Window, false, prompt: false).GetAwaiter().GetResult();
            foreach (var item in vm.Toolbox.Categories.SelectMany(c => c.Items).Take(16)) vm.AddControl(item);
            TestHelpers.Pump();
            TestHelpers.SaveFrame(window, "mainwindow-dark");
        }
        finally
        {
            ThemeService.Apply("System");
        }
    }
}
