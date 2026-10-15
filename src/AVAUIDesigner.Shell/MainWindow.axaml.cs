using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
using AVAUIDesigner.Shell.Services;
using AVAUIDesigner.Shell.ViewModels;
using ReactiveUI;

namespace AVAUIDesigner.Shell;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;
    private bool _closeConfirmed;
    private readonly List<IDisposable> _subscriptions = [];
    private GridLength _toolboxWidth = new(200);
    private GridLength _rightWidth = new(280);
    private GridLength _sourceHeight = new(0.42, GridUnitType.Star);
    private GridLength _outlineHeight = new(0.4, GridUnitType.Star);

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Bubble);
        Closing += OnClosing;
    }

    public MainWindow(MainWindowViewModel vm) : this()
    {
        DataContext = vm;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        foreach (var s in _subscriptions) s.Dispose();
        _subscriptions.Clear();
        _vm = DataContext as MainWindowViewModel;
        if (_vm is null) return;

        var dialogs = new DialogService(this);
        _vm.Dialogs = dialogs;
        _vm.Clipboard = dialogs;
        _vm.FocusPropertiesRequested += (_, _) => Properties.FocusFirstEditor();
        _vm.ExitRequested += (_, _) => Close();
        _vm.SettingsChanged += (_, _) => ThemeService.Apply(_vm.Settings.Ui.Theme);

        var layout = _vm.Settings.Ui.PanelLayout;
        _toolboxWidth = new GridLength(Math.Max(120, layout.ToolboxWidth));
        _rightWidth = new GridLength(Math.Max(180, layout.RightPanelWidth));
        _sourceHeight = new GridLength(Math.Clamp(layout.SourceHeightRatio, 0.1, 0.8) / (1 - Math.Clamp(layout.SourceHeightRatio, 0.1, 0.8)), GridUnitType.Star);
        Width = layout.WindowWidth;
        Height = layout.WindowHeight;
        if (layout.WindowMaximized) WindowState = WindowState.Maximized;

        _subscriptions.Add(_vm.WhenAnyValue(x => x.ShowToolbox).Subscribe(_ => UpdatePanels()));
        _subscriptions.Add(_vm.WhenAnyValue(x => x.ShowProperties).Subscribe(_ => UpdatePanels()));
        _subscriptions.Add(_vm.WhenAnyValue(x => x.ShowOutline).Subscribe(_ => UpdatePanels()));
        _subscriptions.Add(_vm.XamlSource.WhenAnyValue(x => x.IsSplitViewVisible).Subscribe(_ => UpdatePanels()));

        _vm.RecentFiles.CollectionChanged += OnRecentChanged;
        RebuildRecentMenu();
    }

    // ------------------------------------------------------------------ panels

    private void UpdatePanels()
    {
        if (_vm is null) return;
        var upper = UpperGrid.ColumnDefinitions;
        if (!_vm.ShowToolbox && upper[0].Width.Value > 0) _toolboxWidth = upper[0].Width;
        upper[0].Width = _vm.ShowToolbox ? _toolboxWidth : new GridLength(0);
        LeftPanel.IsVisible = _vm.ShowToolbox;
        LeftSplitter.IsVisible = _vm.ShowToolbox;

        var work = WorkGrid.RowDefinitions;
        var showSource = _vm.XamlSource.IsSplitViewVisible;
        if (!showSource && work[2].Height.Value > 0) _sourceHeight = work[2].Height;
        work[2].Height = showSource ? _sourceHeight : new GridLength(0);
        Source.IsVisible = showSource;
        SourceSplitter.IsVisible = showSource;

        var showRight = _vm.ShowProperties || _vm.ShowOutline;
        var root = RootGrid.ColumnDefinitions;
        if (!showRight && root[2].Width.Value > 0) _rightWidth = root[2].Width;
        root[2].Width = showRight ? _rightWidth : new GridLength(0);
        RightGrid.IsVisible = showRight;
        RightSplitter.IsVisible = showRight;

        var right = RightGrid.RowDefinitions;
        if (right[2].Height.Value > 0 && _vm.ShowOutline) _outlineHeight = right[2].Height;
        right[0].Height = _vm.ShowProperties ? new GridLength(0.6, GridUnitType.Star) : new GridLength(0);
        right[2].Height = _vm.ShowOutline ? _outlineHeight : new GridLength(0);
        PropertiesPanel.IsVisible = _vm.ShowProperties;
        Outline.IsVisible = _vm.ShowOutline;
        OutlineSplitter.IsVisible = _vm.ShowProperties && _vm.ShowOutline;
    }

    private void SaveLayout()
    {
        if (_vm is null) return;
        var layout = _vm.Settings.Ui.PanelLayout;
        if (_vm.ShowToolbox) layout.ToolboxWidth = UpperGrid.ColumnDefinitions[0].ActualWidth;
        if (_vm.ShowProperties || _vm.ShowOutline) layout.RightPanelWidth = RootGrid.ColumnDefinitions[2].ActualWidth;
        if (_vm.XamlSource.IsSplitViewVisible && WorkGrid.Bounds.Height > 0)
            layout.SourceHeightRatio = WorkGrid.RowDefinitions[2].ActualHeight / WorkGrid.Bounds.Height;
        layout.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            layout.WindowWidth = Width;
            layout.WindowHeight = Height;
        }
    }

    // ------------------------------------------------------------------ recent files

    private void OnRecentChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildRecentMenu();

    private void RebuildRecentMenu()
    {
        if (_vm is null) return;
        var items = _vm.RecentFiles.Select(path => (object)new MenuItem
        {
            Header = path.Replace("_", "__"),
            Command = _vm.OpenRecentCommand,
            CommandParameter = path,
        }).ToList();
        if (items.Count == 0) items.Add(new MenuItem { Header = "(none)", IsEnabled = false });
        RecentMenu.ItemsSource = items;
    }

    // ------------------------------------------------------------------ menu handlers

    private void OnShowCodeBehind(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.XamlSource.IsSplitViewVisible = true;
        _vm.BottomTab = 1;
    }

    private void OnShowViewModel(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.XamlSource.IsSplitViewVisible = true;
        _vm.BottomTab = 2;
    }

    private void OnProjectFileDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm is not null && ProjectList.SelectedItem is ProjectFileItem item)
            _vm.OpenProjectFileCommand.Execute(item).Subscribe(_ => { }, _ => { });
    }

    private async void OnShowShortcuts(object? sender, RoutedEventArgs e)
    {
        if (_vm?.Dialogs is null) return;
        await _vm.Dialogs.ShowMessageAsync("Keyboard Shortcuts", string.Join("\n",
            "Ctrl+N  New window          Ctrl+O  Open          Ctrl+S  Save          Ctrl+Shift+S  Save As",
            "Ctrl+Z  Undo                Ctrl+Y / Ctrl+Shift+Z  Redo",
            "Ctrl+X/C/V  Cut/Copy/Paste  Ctrl+D  Duplicate     Delete  Delete selection",
            "Ctrl+A  Select all          Esc  Deselect / cancel drag     Tab  Next element",
            "Arrows  Nudge 1px           Shift+Arrows  Nudge 8px",
            "F2  Set x:Name              Ctrl+G  Group in Grid",
            "Ctrl+]  Bring to front      Ctrl+[  Send to back",
            "Ctrl+=  Zoom in             Ctrl+-  Zoom out     Ctrl+0  100%     Ctrl+Shift+0  Fit",
            "Ctrl+Space  XAML attribute completion        F12  Go to code-behind definition",
            "Double-click a control  Create its default event handler"));
    }

    // ------------------------------------------------------------------ keyboard

    private bool IsTextInputFocused()
    {
        var focused = FocusManager?.GetFocusedElement() as Visual;
        for (var v = focused; v is not null; v = v.GetVisualParent())
            if (v is TextBox or TextArea or AutoCompleteBox or NumericUpDown or ComboBox) return true;
        return false;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null || e.Handled) return;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var key = e.Key;

        // Global file shortcuts work everywhere, even inside text editors.
        ReactiveCommandBase<System.Reactive.Unit, System.Reactive.Unit>? global = (ctrl, shift, key) switch
        {
            (true, false, Key.S) => _vm.SaveCommand,
            (true, true, Key.S) => _vm.SaveAsCommand,
            (true, false, Key.N) => _vm.NewWindowCommand,
            (true, false, Key.O) => _vm.OpenFileCommand,
            _ => null,
        };
        if (global is not null)
        {
            Run(global);
            e.Handled = true;
            return;
        }

        // Everything else is a designer shortcut: leave text editing keys to text inputs.
        if (IsTextInputFocused()) return;

        var handled = true;
        switch (ctrl, shift, key)
        {
            case (true, false, Key.Z): Run(_vm.UndoCommand); break;
            case (true, false, Key.Y): Run(_vm.RedoCommand); break;
            case (true, true, Key.Z): Run(_vm.RedoCommand); break;
            case (true, false, Key.A): Run(_vm.SelectAllCommand); break;
            case (true, false, Key.C): Run(_vm.CopyCommand); break;
            case (true, false, Key.X): Run(_vm.CutCommand); break;
            case (true, false, Key.V): Run(_vm.PasteCommand); break;
            case (true, false, Key.D): Run(_vm.DuplicateCommand); break;
            case (true, false, Key.G): Run(_vm.GroupInGridCommand); break;
            case (true, false, Key.OemCloseBrackets): Run(_vm.BringToFrontCommand); break;
            case (true, false, Key.OemOpenBrackets): Run(_vm.SendToBackCommand); break;
            case (true, _, Key.OemPlus or Key.Add): Run(_vm.ZoomInCommand); break;
            case (true, _, Key.OemMinus or Key.Subtract): Run(_vm.ZoomOutCommand); break;
            case (true, false, Key.D0 or Key.NumPad0): Run(_vm.Zoom100Command); break;
            case (true, true, Key.D0 or Key.NumPad0): Run(_vm.ZoomFitCommand); break;
            case (false, _, Key.Delete or Key.Back): Run(_vm.DeleteCommand); break;
            case (false, false, Key.F2): Run(_vm.RenameCommand); break;
            case (false, false, Key.Escape): Run(_vm.DeselectCommand); break;
            case (false, _, Key.Tab): _vm.CycleSelection(!shift); break;
            case (false, _, Key.Left): _vm.Nudge(shift ? -8 : -1, 0); break;
            case (false, _, Key.Right): _vm.Nudge(shift ? 8 : 1, 0); break;
            case (false, _, Key.Up): _vm.Nudge(0, shift ? -8 : -1); break;
            case (false, _, Key.Down): _vm.Nudge(0, shift ? 8 : 1); break;
            default: handled = false; break;
        }
        e.Handled = handled;
    }

    private static void Run(ReactiveCommandBase<System.Reactive.Unit, System.Reactive.Unit> command) =>
        command.Execute().Subscribe(_ => { }, _ => { });

    // ------------------------------------------------------------------ closing

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || _vm is null) return;
        e.Cancel = true;
        SaveLayout();
        if (!await _vm.ConfirmExitAsync()) return;
        _closeConfirmed = true;
        Close();
    }
}
