using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Shell.Services;
using AVAUIDesigner.Shell.ViewModels;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AVAUIDesigner.Shell.Tests;

public class MainWindowFlowTests
{
    private static (MainWindow Window, MainWindowViewModel Vm) Open(string? xaml = null, string path = "")
    {
        var vm = TestHelpers.CreateViewModel();
        vm.Clipboard = new InMemoryClipboard();
        var window = new MainWindow(vm) { Width = 1400, Height = 900 };
        window.Show();
        vm.Clipboard = new InMemoryClipboard(); // MainWindow installs the system clipboard; tests use an in-memory one
        vm.Dialogs = null;
        if (xaml is null) vm.NewDocumentAsync(NewDocumentKind.Window, false, prompt: false).GetAwaiter().GetResult();
        else vm.LoadText(xaml, path);
        TestHelpers.Pump();
        return (window, vm);
    }

    private static DesignElement Canvas(MainWindowViewModel vm) => vm.CurrentDocument!.RootElement.Children[0];

    private static void Run(System.Windows.Input.ICommand command, object? parameter = null)
    {
        command.Execute(parameter);
        TestHelpers.Pump();
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(TestHelpers.ScratchDir, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [AvaloniaFact]
    public void AddControl_FromToolbox_AutoNamesAndSelects()
    {
        var (_, vm) = Open();
        var button = vm.Toolbox.Categories.SelectMany(c => c.Items).First(i => i.DisplayName == "Button");
        vm.AddControl(button);
        vm.AddControl(button);
        var names = Canvas(vm).Children.Select(c => c.Name).ToList();
        Assert.Equal(["button1", "button2"], names);
        Assert.Same(Canvas(vm).Children[1], vm.SelectionManager.PrimarySelection);
        Assert.Contains("x:Name=\"button2\"", vm.XamlSource.XamlText);
    }

    [AvaloniaFact]
    public async Task Save_WritesAxamlAndCodeBehind_ThenReopenRoundTrips()
    {
        var (_, vm) = Open(TestHelpers.CanvasDocument);
        vm.CreateEventHandler(vm.CurrentDocument!.NamedElements["btn1"], "Click");
        var dir = NewTempDir();
        var path = Path.Combine(dir, "TestWindow.axaml");
        Assert.True(await vm.SaveToAsync(path));

        Assert.True(File.Exists(path));
        var cs = await File.ReadAllTextAsync(path + ".cs");
        Assert.Contains("public partial class TestWindow : Window", cs);
        Assert.Contains("private void Btn1_Click(object? sender, RoutedEventArgs e)", cs);
        Assert.Empty(CSharpSyntaxTree.ParseText(cs).GetDiagnostics());
        Assert.False(vm.CurrentDocument!.IsDirty);

        // Hand-edit the handler, re-save: the body survives.
        await File.WriteAllTextAsync(path + ".cs", cs.Replace("// TODO: implement", "Console.WriteLine(\"clicked\");"));
        var (_, vm2) = Open();
        await vm2.LoadFileAsync(path);
        vm2.Execute(new Engine.UndoRedo.Commands.SetPropertyCommand(vm2.CurrentDocument!.NamedElements["btn2"], "Content", "Changed"));
        Assert.True(await vm2.SaveAsync());
        var cs2 = await File.ReadAllTextAsync(path + ".cs");
        Assert.Contains("Console.WriteLine(\"clicked\");", cs2);
        Assert.Contains("Content=\"Changed\"", await File.ReadAllTextAsync(path));
    }

    [AvaloniaFact]
    public async Task MvvmDocument_SavesViewModel()
    {
        var (_, vm) = Open();
        await vm.NewDocumentAsync(NewDocumentKind.Window, mvvm: true, prompt: false);
        var textBox = vm.Toolbox.Categories.SelectMany(c => c.Items).First(i => i.DisplayName == "TextBox");
        vm.SelectionManager.ClearSelection();
        vm.AddControl(textBox);
        Assert.Equal("{Binding TextBox1Text, Mode=TwoWay}", Canvas(vm).Children[0].GetValue("Text"));

        var dir = NewTempDir();
        var views = Path.Combine(dir, "Views");
        Assert.True(await vm.SaveToAsync(Path.Combine(views, "NewWindow.axaml")));
        var vmPath = Path.Combine(dir, "ViewModels", "NewWindowViewModel.cs");
        Assert.True(File.Exists(vmPath));
        var source = await File.ReadAllTextAsync(vmPath);
        Assert.Contains("private string _textBox1Text = string.Empty;", source);
        Assert.Empty(CSharpSyntaxTree.ParseText(source).GetDiagnostics());
    }

    [AvaloniaFact]
    public void CopyPaste_AndDuplicate_CreateUniqueNames()
    {
        var (_, vm) = Open(TestHelpers.CanvasDocument);
        var doc = vm.CurrentDocument!;
        vm.SelectionManager.SetSelection(doc.NamedElements["btn1"]);
        Run(vm.CopyCommand);
        Run(vm.PasteCommand);
        Assert.Equal(3, Canvas(vm).Children.Count);
        var pasted = Canvas(vm).Children[2];
        Assert.NotEqual("btn1", pasted.Name);
        Assert.Equal("50", pasted.GetValue("Canvas.Left"));

        vm.SelectionManager.SetSelection(doc.NamedElements["btn2"]);
        Run(vm.DuplicateCommand);
        Assert.Equal(4, Canvas(vm).Children.Count);
        Assert.Equal(4, doc.NamedElements.Count(n => n.Value.ElementType == "Button"));

        Run(vm.UndoCommand);
        Run(vm.UndoCommand);
        Assert.Equal(2, Canvas(vm).Children.Count);
    }

    [AvaloniaFact]
    public void Delete_RemovesSelection_RootCannotBeDeleted()
    {
        var (_, vm) = Open(TestHelpers.CanvasDocument);
        vm.SelectionManager.SetSelection(vm.CurrentDocument!.RootElement);
        Run(vm.DeleteCommand);
        Assert.NotNull(vm.CurrentDocument.RootElement);
        vm.SelectionManager.SelectAll();
        Run(vm.DeleteCommand);
        Assert.Empty(vm.CurrentDocument.RootElement.Children);
    }

    [AvaloniaFact]
    public void WrapInGrid_ThenUnwrap_KeepsPositions()
    {
        var (_, vm) = Open(TestHelpers.CanvasDocument);
        var doc = vm.CurrentDocument!;
        var btn1 = doc.NamedElements["btn1"];
        var btn2 = doc.NamedElements["btn2"];
        vm.SelectionManager.SetSelection([btn1, btn2]);
        vm.WrapIn("Grid");
        TestHelpers.Pump();

        var grid = Canvas(vm).Children.Single();
        Assert.Equal("Grid", grid.ElementType);
        Assert.Equal("40", grid.GetValue("Canvas.Left"));
        Assert.Equal("260", grid.GetValue("Width"));
        Assert.Same(grid, btn1.Parent);
        Assert.Equal("160,60,0,0", btn2.GetValue("Margin"));
        Assert.Null(btn2.GetValue("Canvas.Left"));

        vm.SelectionManager.SetSelection(grid);
        Run(vm.UnwrapCommand);
        Assert.Same(Canvas(vm), btn2.Parent);
        Assert.Equal("200", btn2.GetValue("Canvas.Left"));
        Assert.Equal("100", btn2.GetValue("Canvas.Top"));
    }

    [AvaloniaFact]
    public void Align_And_Distribute_And_Nudge()
    {
        const string xaml = """
            <Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="600" Height="400">
                <Canvas>
                    <Button x:Name="a" Canvas.Left="10" Canvas.Top="10" Width="50" Height="20" />
                    <Button x:Name="b" Canvas.Left="100" Canvas.Top="50" Width="50" Height="20" />
                    <Button x:Name="c" Canvas.Left="400" Canvas.Top="90" Width="50" Height="20" />
                </Canvas>
            </Window>
            """;
        var (_, vm) = Open(xaml);
        var doc = vm.CurrentDocument!;
        var (a, b, c) = (doc.NamedElements["a"], doc.NamedElements["b"], doc.NamedElements["c"]);

        vm.SelectionManager.SetSelection([b, c, a]); // a is primary
        Run(vm.AlignCommand, AlignKind.Left);
        Assert.All([a, b, c], e => Assert.Equal("10", e.GetValue("Canvas.Left")));

        Run(vm.UndoCommand);
        vm.SelectionManager.SetSelection([a, b, c]);
        Run(vm.DistributeCommand, "Horizontal");
        Assert.Equal("205", b.GetValue("Canvas.Left")); // gaps equal: (440 - 150)/2 = 145 → 10+50+145

        vm.SelectionManager.SetSelection(a);
        vm.Nudge(8, -1);
        TestHelpers.Pump();
        Assert.Equal("18", a.GetValue("Canvas.Left"));
        Assert.Equal("9", a.GetValue("Canvas.Top"));
    }

    [AvaloniaFact]
    public void ZOrder_ReordersChildren()
    {
        var (_, vm) = Open(TestHelpers.CanvasDocument);
        var btn1 = vm.CurrentDocument!.NamedElements["btn1"];
        vm.SelectionManager.SetSelection(btn1);
        Run(vm.BringToFrontCommand);
        Assert.Equal(1, btn1.IndexInParent);
        Run(vm.SendToBackCommand);
        Assert.Equal(0, btn1.IndexInParent);
    }

    [AvaloniaFact]
    public void DefaultEvent_GeneratesHandlerAndStub()
    {
        var (_, vm) = Open(TestHelpers.CanvasDocument);
        var btn1 = vm.CurrentDocument!.NamedElements["btn1"];
        vm.HandleDesignAction(new Controls.DesignActionEventArgs(Controls.DesignActionKind.CreateDefaultEventHandler, btn1));
        Assert.Equal("Btn1_Click", btn1.GetEventHandler("Click"));
        Assert.Contains("Click=\"Btn1_Click\"", vm.XamlSource.XamlText);
        Assert.Contains("private void Btn1_Click(object? sender, RoutedEventArgs e)", vm.CodeBehindText);
        Assert.Equal(1, vm.BottomTab);
    }

    [AvaloniaFact]
    public void PropertyGrid_EditsAreUndoable_AndMultiSelectShowsCommonProperties()
    {
        var (_, vm) = Open(TestHelpers.CanvasDocument);
        var doc = vm.CurrentDocument!;
        vm.SelectionManager.SetSelection(doc.NamedElements["btn1"]);
        var content = vm.PropertyGrid.AllProperties.Single(r => r.Name == "Content");
        Assert.Equal("One", content.Value);
        content.Value = "Hello";
        Assert.Equal("Hello", doc.NamedElements["btn1"].GetValue("Content"));
        Assert.Contains("Content=\"Hello\"", vm.XamlSource.XamlText);

        var width = vm.PropertyGrid.AllProperties.Single(r => r.Name == "Width");
        width.Value = "abc"; // invalid → rejected
        Assert.True(width.HasError);
        Assert.Equal("100", doc.NamedElements["btn1"].GetValue("Width"));

        vm.SelectionManager.SetSelection([doc.NamedElements["btn1"], doc.NamedElements["btn2"]]);
        var multiWidth = vm.PropertyGrid.AllProperties.Single(r => r.Name == "Width");
        Assert.Equal("100", multiWidth.Value);
        var multiContent = vm.PropertyGrid.AllProperties.Single(r => r.Name == "Content");
        Assert.True(multiContent.IsMixed);
        multiWidth.Value = "120";
        Assert.All(doc.NamedElements.Values.Where(e => e.ElementType == "Button"), e => Assert.Equal("120", e.GetValue("Width")));
        Run(vm.UndoCommand);
        Assert.Equal("100", doc.NamedElements["btn2"].GetValue("Width"));
        Run(vm.UndoCommand);
        Assert.Equal("One", doc.NamedElements["btn1"].GetValue("Content"));
    }

    [AvaloniaFact]
    public void PropertyGrid_Rename_ValidatesIdentifier()
    {
        var (_, vm) = Open(TestHelpers.CanvasDocument);
        var btn1 = vm.CurrentDocument!.NamedElements["btn1"];
        vm.SelectionManager.SetSelection(btn1);
        vm.PropertyGrid.ElementName = "1bad";
        Assert.NotNull(vm.PropertyGrid.NameError);
        Assert.Equal("btn1", btn1.Name);
        vm.PropertyGrid.ElementName = "btn2";
        Assert.NotNull(vm.PropertyGrid.NameError);
        vm.PropertyGrid.ElementName = "okButton";
        Assert.Equal("okButton", btn1.Name);
    }

    [AvaloniaFact]
    public void GridEditor_AddsRowsAndColumns()
    {
        const string xaml = """
            <Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="600" Height="400">
                <Grid x:Name="g" />
            </Window>
            """;
        var (_, vm) = Open(xaml);
        vm.SelectionManager.SetSelection(vm.CurrentDocument!.NamedElements["g"]);
        var editor = vm.PropertyGrid.GridEditor!;
        editor.AddRowCommand.Execute().Subscribe();
        editor.AddColumnCommand.Execute().Subscribe();
        TestHelpers.Pump();
        Assert.Contains("<Grid.RowDefinitions>", vm.XamlSource.XamlText);
        Assert.Equal(2, vm.PropertyGrid.GridEditor!.Rows.Count);
        vm.PropertyGrid.GridEditor.Rows[0].Value = "Auto";
        Assert.Contains("<RowDefinition Height=\"Auto\" />", vm.XamlSource.XamlText);
    }

    [AvaloniaFact]
    public async Task SourceEdit_IsParsedAfterDelay_AndInvalidXamlDoesNotCrash()
    {
        var (_, vm) = Open(TestHelpers.CanvasDocument);
        var text = vm.XamlSource.XamlText.Replace("Content=\"One\"", "Content=\"Edited\"");
        vm.XamlSource.XamlText = text;
        await WaitFor(() => vm.CurrentDocument!.NamedElements["btn1"].GetValue("Content") == "Edited");
        Assert.False(vm.XamlSource.HasParseError);
        Assert.True(vm.UndoRedo.CanUndo);

        vm.XamlSource.XamlText = "<Window xmlns=\"https://github.com/avaloniaui\"><Canvas>";
        await WaitFor(() => vm.XamlSource.HasParseError);
        Assert.True(vm.XamlSource.ErrorLine > 0);
        // The designer keeps the last good document.
        Assert.Equal("Edited", vm.CurrentDocument!.NamedElements["btn1"].GetValue("Content"));
    }

    [AvaloniaFact]
    public void OpeningUnparseableFile_KeepsTextAndReportsError()
    {
        var (_, vm) = Open("<Window><Oops></Window>");
        Assert.True(vm.XamlSource.HasParseError);
        Assert.Equal("<Window><Oops></Window>", vm.XamlSource.XamlText);
        Assert.NotNull(vm.CurrentDocument);
    }

    [AvaloniaFact]
    public void ConvertToMvvm_AddsDataContextAndViewModelPreview()
    {
        var (_, vm) = Open(TestHelpers.CanvasDocument);
        Run(vm.ApplyMvvmBindingsCommand);
        Assert.True(vm.IsMvvm);
        Assert.Contains("x:DataType=\"vm:TestWindowViewModel\"", vm.XamlSource.XamlText);
        Assert.Contains("Command=\"{Binding Btn1Command}\"", vm.XamlSource.XamlText);
        Assert.Contains("[RelayCommand]", vm.ViewModelText);
        Assert.Contains("private void Btn1()", vm.ViewModelText);
    }

    [AvaloniaFact]
    public async Task ExternalModification_ShowsNotification()
    {
        var dir = NewTempDir();
        var path = Path.Combine(dir, "TestWindow.axaml");
        await File.WriteAllTextAsync(path, TestHelpers.CanvasDocument);
        var (_, vm) = Open();
        await vm.LoadFileAsync(path);
        await File.WriteAllTextAsync(path, TestHelpers.CanvasDocument.Replace("One", "External"));
        await WaitFor(() => vm.Notification is not null, 5000);
        Assert.Contains("modified externally", vm.Notification!.Message);
        vm.Notification.PrimaryCommand.Execute().Subscribe();
        await WaitFor(() => vm.CurrentDocument!.NamedElements["btn1"].GetValue("Content") == "External", 5000);
    }

    [AvaloniaFact]
    public void Recovery_SnapshotOnlyWhenDirty()
    {
        var (_, vm) = Open(TestHelpers.CanvasDocument, "/tmp/x.axaml");
        Assert.False(vm.GetRecoverySnapshot()!.Value.Dirty);
        vm.SelectionManager.SelectAll();
        Run(vm.DeleteCommand);
        var snap = vm.GetRecoverySnapshot()!.Value;
        Assert.True(snap.Dirty);
        Assert.DoesNotContain("btn1", snap.Text);

        var dir = NewTempDir();
        var recovery = new RecoveryService(dir);
        recovery.Start(vm.GetRecoverySnapshot);
        recovery.SaveNow();
        Assert.NotNull(recovery.TryGetRecovery());
        recovery.Clear();
        Assert.Null(recovery.TryGetRecovery());
    }

    [AvaloniaFact]
    public async Task OpenProject_ListsFiles_AndDerivesXClass()
    {
        var dir = NewTempDir();
        await File.WriteAllTextAsync(Path.Combine(dir, "MyApp.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><RootNamespace>Acme.App</RootNamespace></PropertyGroup></Project>");
        Directory.CreateDirectory(Path.Combine(dir, "Views"));
        var axaml = Path.Combine(dir, "Views", "Main.axaml");
        await File.WriteAllTextAsync(axaml, "<UserControl xmlns=\"https://github.com/avaloniaui\"><Canvas /></UserControl>");
        var (_, vm) = Open();
        await vm.OpenProjectAsync(Path.Combine(dir, "MyApp.csproj"));
        Assert.Single(vm.ProjectFiles);
        await vm.LoadFileAsync(axaml);
        Assert.Equal("Acme.App.Views.Main", vm.CurrentDocument!.XClass);
        Assert.Contains("namespace Acme.App.Views;", vm.CodeBehindText);
        Assert.Contains("public partial class Main : UserControl", vm.CodeBehindText);
    }

    private static async Task WaitFor(Func<bool> condition, int timeoutMs = 3000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - start > timeoutMs) throw new TimeoutException("Condition was not met in time.");
            await Task.Delay(25);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
