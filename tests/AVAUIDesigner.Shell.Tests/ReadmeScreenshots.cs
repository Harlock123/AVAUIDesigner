using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using AVAUIDesigner.Engine.Settings;
using AVAUIDesigner.Shell.Controls;
using AVAUIDesigner.Shell.Dialogs;
using AVAUIDesigner.Shell.Services;
using AVAUIDesigner.Shell.ViewModels;
using AVAUIDesigner.Shell.Views;
using Xunit;

namespace AVAUIDesigner.Shell.Tests;

/// <summary>
/// Regenerates the screenshots used by README.md. Only runs when AVAUIDESIGNER_SCREENSHOTS names the
/// output folder (see scripts/update-screenshots.sh); otherwise each test returns immediately.
/// </summary>
public class ReadmeScreenshots
{
    private static string? OutputDir => Environment.GetEnvironmentVariable("AVAUIDESIGNER_SCREENSHOTS") is { Length: > 0 } d ? d : null;

    private const string CustomerForm = """
        <Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                x:Class="DemoApp.Views.CustomerWindow" Title="Customer Details" Width="620" Height="430">
            <DockPanel>
                <Menu DockPanel.Dock="Top">
                    <MenuItem Header="_File" />
                    <MenuItem Header="_Edit" />
                    <MenuItem Header="_Help" />
                </Menu>
                <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right" Spacing="8" Margin="16">
                    <Button x:Name="saveButton" Content="Save" Width="90" Click="SaveButton_Click" />
                    <Button x:Name="cancelButton" Content="Cancel" Width="90" Click="CancelButton_Click" />
                </StackPanel>
                <Grid x:Name="formGrid" Margin="16,12" ColumnDefinitions="110,*" RowDefinitions="44,44,44,44,44,*">
                    <TextBlock Text="First name" VerticalAlignment="Center" />
                    <TextBox x:Name="firstNameBox" Grid.Column="1" Text="Ada" VerticalAlignment="Center" />
                    <TextBlock Grid.Row="1" Text="Last name" VerticalAlignment="Center" />
                    <TextBox x:Name="lastNameBox" Grid.Row="1" Grid.Column="1" Text="Lovelace" VerticalAlignment="Center" />
                    <TextBlock Grid.Row="2" Text="Email" VerticalAlignment="Center" />
                    <TextBox x:Name="emailBox" Grid.Row="2" Grid.Column="1" Watermark="name@example.com" VerticalAlignment="Center" />
                    <TextBlock Grid.Row="3" Text="Plan" VerticalAlignment="Center" />
                    <ComboBox x:Name="planCombo" Grid.Row="3" Grid.Column="1" PlaceholderText="Choose a plan" Width="200" VerticalAlignment="Center" />
                    <CheckBox x:Name="newsletterCheck" Grid.Row="4" Grid.Column="1" Content="Subscribe to the newsletter" IsChecked="True" />
                    <TextBlock Grid.Row="5" Text="Notes" Margin="0,8,0,0" />
                    <TextBox x:Name="notesBox" Grid.Row="5" Grid.Column="1" AcceptsReturn="True" TextWrapping="Wrap" Margin="0,4,0,0" />
                </Grid>
            </DockPanel>
        </Window>
        """;

    private const string LoginCanvas = """
        <Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                x:Class="DemoApp.Views.LoginWindow" Title="Sign In" Width="520" Height="340">
            <Canvas>
                <TextBlock x:Name="titleText" Canvas.Left="40" Canvas.Top="30" Text="Welcome back" FontSize="22" />
                <TextBox x:Name="userBox" Canvas.Left="40" Canvas.Top="90" Width="200" Height="32" Watermark="User name" />
                <TextBox x:Name="passwordBox" Canvas.Left="40" Canvas.Top="140" Width="200" Height="32" PasswordChar="●" Watermark="Password" />
                <CheckBox x:Name="rememberCheck" Canvas.Left="40" Canvas.Top="190" Content="Remember me" />
                <Button x:Name="signInButton" Canvas.Left="40" Canvas.Top="240" Width="100" Height="32" Content="Sign in" />
                <Button x:Name="helpButton" Canvas.Left="300" Canvas.Top="240" Width="100" Height="32" Content="Help" />
                <Border x:Name="logoBorder" Canvas.Left="300" Canvas.Top="30" Width="160" Height="90" Background="#3D7DD8" CornerRadius="8" />
            </Canvas>
        </Window>
        """;

    private const string DashboardGrid = """
        <Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                x:Class="DemoApp.Views.DashboardWindow" Title="Dashboard" Width="600" Height="400">
            <Grid x:Name="layoutGrid" ColumnDefinitions="*,2*" RowDefinitions="Auto,*,*">
                <TextBlock Grid.ColumnSpan="2" Text="Dashboard" FontSize="20" Margin="12" />
                <Button x:Name="refreshButton" Grid.Row="1" Width="110" Height="32" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="12,12,0,0" Content="Refresh" />
                <ProgressBar Grid.Row="2" Grid.Column="1" Value="65" Width="240" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="12,12,0,0" />
            </Grid>
        </Window>
        """;

    // ------------------------------------------------------------------ main window

    [AvaloniaFact]
    public void Overview_Light()
    {
        if (OutputDir is null) return;
        var (window, vm) = Open(CustomerForm);
        Select(vm, "saveButton");
        Save(window, "overview-light");
    }

    [AvaloniaFact]
    public void Overview_Dark()
    {
        if (OutputDir is null) return;
        ThemeService.Apply("FluentDark");
        try
        {
            var (window, vm) = Open(CustomerForm);
            Select(vm, "emailBox");
            Save(window, "overview-dark");
        }
        finally
        {
            ThemeService.Apply("System");
        }
    }

    // ------------------------------------------------------------------ panels

    [AvaloniaFact]
    public void Toolbox()
    {
        if (OutputDir is null) return;
        var (window, _) = Open(CustomerForm, height: 1100);
        SaveCrop(window, window.FindControl<TabControl>("LeftPanel")!, "toolbox");
    }

    [AvaloniaFact]
    public void PropertyGrid_Properties()
    {
        if (OutputDir is null) return;
        var (window, vm) = Open(CustomerForm, height: 1100);
        Select(vm, "firstNameBox");
        SaveCrop(window, window.FindControl<Control>("PropertiesPanel")!, "property-grid");
    }

    [AvaloniaFact]
    public void PropertyGrid_Events()
    {
        if (OutputDir is null) return;
        var (window, vm) = Open(CustomerForm, height: 1100);
        Select(vm, "saveButton");
        vm.PropertyGrid.SelectedTab = 1;
        TestHelpers.Pump();
        SaveCrop(window, window.FindControl<Control>("PropertiesPanel")!, "property-grid-events");
    }

    [AvaloniaFact]
    public void PropertyGrid_GridEditor()
    {
        if (OutputDir is null) return;
        var (window, vm) = Open(CustomerForm, height: 1100);
        Select(vm, "formGrid");
        SaveCrop(window, window.FindControl<Control>("PropertiesPanel")!, "grid-definition-editor");
    }

    [AvaloniaFact]
    public void DocumentOutline()
    {
        if (OutputDir is null) return;
        var (window, vm) = Open(CustomerForm, height: 1300);
        Select(vm, "emailBox");
        SaveCrop(window, window.FindControl<Control>("Outline")!, "document-outline");
    }

    [AvaloniaFact]
    public void XamlSource()
    {
        if (OutputDir is null) return;
        var (window, vm) = Open(CustomerForm);
        Select(vm, "emailBox");
        SaveCrop(window, [Surface(window), window.FindControl<Control>("Source")!], "xaml-source");
    }

    [AvaloniaFact]
    public void CodeBehind()
    {
        if (OutputDir is null) return;
        var (window, _) = Open(CustomerForm, height: 920);
        TallSource(window);
        SourceTabs(window).SelectedIndex = 1;
        TestHelpers.Pump();
        SaveCrop(window, window.FindControl<Control>("Source")!, "code-behind");
    }

    [AvaloniaFact]
    public void ViewModel()
    {
        if (OutputDir is null) return;
        var (window, vm) = Open(CustomerForm, height: 1200);
        TallSource(window);
        ((System.Windows.Input.ICommand)vm.ConvertToMvvmCommand).Execute(null);
        TestHelpers.Pump();
        ((System.Windows.Input.ICommand)vm.ApplyMvvmBindingsCommand).Execute(null);
        TestHelpers.Pump();
        SourceTabs(window).SelectedIndex = 2;
        TestHelpers.Pump();
        SaveCrop(window, window.FindControl<Control>("Source")!, "viewmodel");
    }

    // ------------------------------------------------------------------ design surface interactions

    [AvaloniaFact]
    public void SnapLines()
    {
        if (OutputDir is null) return;
        var (window, vm) = Open(LoginCanvas, smartSnap: true);
        var canvas = Canvas(window);
        var help = vm.CurrentDocument!.NamedElements["helpButton"];
        var from = TestHelpers.CenterOf(canvas, window, help);
        // Drag the Help button up until it lines up with the password box: smart guides appear.
        DragAndCapture(window, from, from + new Point(-3, -98), () => SaveCrop(window, Surface(window), "snap-lines"));
    }

    [AvaloniaFact]
    public void RubberBandSelection()
    {
        if (OutputDir is null) return;
        var (window, vm) = Open(LoginCanvas);
        var canvas = Canvas(window);
        var root = canvas.GetElementBounds(vm.CurrentDocument!.RootElement)!.Value;
        var from = TestHelpers.ToWindow(canvas, window, root.TopLeft + new Point(20, 80));
        var to = TestHelpers.ToWindow(canvas, window, root.TopLeft + new Point(270, 300));
        DragAndCapture(window, from, to, () => SaveCrop(window, Surface(window), "rubber-band"));
    }

    [AvaloniaFact]
    public void MultiSelection()
    {
        if (OutputDir is null) return;
        var (window, vm) = Open(LoginCanvas);
        var d = vm.CurrentDocument!.NamedElements;
        vm.SelectionManager.SetSelection([d["userBox"], d["passwordBox"], d["signInButton"]]);
        TestHelpers.Pump();
        SaveCrop(window, Surface(window), "multi-selection");
    }

    [AvaloniaFact]
    public void GridCellDrop()
    {
        if (OutputDir is null) return;
        var (window, vm) = Open(DashboardGrid);
        var canvas = Canvas(window);
        var btn = vm.CurrentDocument!.NamedElements["refreshButton"];
        var from = TestHelpers.CenterOf(canvas, window, btn);
        DragAndCapture(window, from, from + new Point(260, 10), () => SaveCrop(window, Surface(window), "grid-cell-drop"));
    }

    // ------------------------------------------------------------------ dialogs

    [AvaloniaFact]
    public void Preferences()
    {
        if (OutputDir is null) return;
        var dialog = new PreferencesWindow(new DesignerSettings());
        dialog.Show();
        // Keep the machine's home folder out of published screenshots.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var t in dialog.GetVisualDescendants().OfType<TextBlock>())
            if (t.Text?.Contains(home) == true) t.Text = t.Text.Replace(home, "~");
        Save(dialog, "preferences");
    }

    [AvaloniaFact]
    public void NewDocument()
    {
        if (OutputDir is null) return;
        var dialog = new NewDocumentDialog(NewDocumentKind.Window, true, "DemoApp.Views", "/home/user/Projects/DemoApp/Views");
        dialog.Show();
        Save(dialog, "new-document");
    }

    // ------------------------------------------------------------------ helpers

    private static (MainWindow Window, MainWindowViewModel Vm) Open(string xaml, double width = 1400, double height = 860, bool smartSnap = false)
    {
        var settings = TestHelpers.Settings();
        settings.Design.SmartSnapLines = smartSnap;
        var vm = TestHelpers.CreateViewModel(settings);
        var window = new MainWindow(vm) { Width = width, Height = height };
        window.Show();
        vm.Dialogs = null;
        vm.LoadText(xaml, string.Empty);
        TestHelpers.Pump();
        return (window, vm);
    }

    private static void Select(MainWindowViewModel vm, string name)
    {
        vm.SelectionManager.SetSelection(vm.CurrentDocument!.NamedElements[name]);
        TestHelpers.Pump();
    }

    private static DesignCanvas Canvas(Window window) => window.FindControl<DesignSurfaceView>("Surface")!.DesignCanvas;

    private static Control Surface(Window window) => window.FindControl<Control>("Surface")!;

    /// <summary>Gives the source pane most of the window height so whole files fit.</summary>
    private static void TallSource(Window window)
    {
        var rows = window.FindControl<Grid>("WorkGrid")!.RowDefinitions;
        rows[0].Height = new GridLength(200);
        rows[2].Height = new GridLength(1, GridUnitType.Star);
        TestHelpers.Pump();
    }

    private static TabControl SourceTabs(Window window) =>
        window.FindControl<Control>("Source")!.GetVisualDescendants().OfType<TabControl>().First();

    /// <summary>Presses at <paramref name="from"/>, moves to <paramref name="to"/>, captures mid-drag, then releases.</summary>
    private static void DragAndCapture(Window window, Point from, Point to, Action capture, int steps = 8)
    {
        window.MouseMove(from);
        window.MouseDown(from, MouseButton.Left);
        for (var i = 1; i <= steps; i++)
        {
            window.MouseMove(new Point(from.X + (to.X - from.X) * i / steps, from.Y + (to.Y - from.Y) * i / steps), RawInputModifiers.LeftMouseButton);
            TestHelpers.Pump();
        }
        capture();
        window.MouseUp(to, MouseButton.Left);
        TestHelpers.Pump();
    }

    private static string PathFor(string name)
    {
        Directory.CreateDirectory(OutputDir!);
        return Path.Combine(OutputDir!, name + ".png");
    }

    private static void Save(Window window, string name)
    {
        TestHelpers.Pump();
        window.CaptureRenderedFrame()!.Save(PathFor(name));
    }

    private static void SaveCrop(Window window, Control control, string name) => SaveCrop(window, [control], name);

    /// <summary>Saves the part of the window frame covered by <paramref name="controls"/>.</summary>
    private static void SaveCrop(Window window, Control[] controls, string name)
    {
        TestHelpers.Pump();
        using var frame = window.CaptureRenderedFrame()!;
        var area = controls.Select(c => new Rect(c.TranslatePoint(default, window)!.Value, c.Bounds.Size)).Aggregate((a, b) => a.Union(b));
        var src = area.Intersect(new Rect(frame.Size));
        var size = new PixelSize((int)src.Width, (int)src.Height);
        using var crop = new RenderTargetBitmap(size);
        using (var ctx = crop.CreateDrawingContext())
            ctx.DrawImage(frame, src, new Rect(0, 0, size.Width, size.Height));
        crop.Save(PathFor(name));
    }
}
