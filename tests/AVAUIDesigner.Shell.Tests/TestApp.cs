using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using AVAUIDesigner.Shell.Tests;
using ReactiveUI.Avalonia;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace AVAUIDesigner.Shell.Tests;

public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        foreach (var uri in new[]
                 {
                     "avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml",
                     "avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml",
                     "avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml",
                 })
            Styles.Add(new StyleInclude(new Uri("avares://AVAUIDesigner.Shell.Tests/")) { Source = new Uri(uri) });
    }
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseSkia()
        .WithInterFont()
        .UseReactiveUI(_ => { })
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
