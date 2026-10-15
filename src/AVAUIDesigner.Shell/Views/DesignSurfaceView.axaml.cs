using Avalonia.Controls;
using AVAUIDesigner.Shell.Controls;
using AVAUIDesigner.Shell.ViewModels;
using ReactiveUI;

namespace AVAUIDesigner.Shell.Views;

/// <summary>Hosts the DesignCanvas and connects it to the main view model.</summary>
public partial class DesignSurfaceView : UserControl
{
    private MainWindowViewModel? _vm;
    private IDisposable? _baseDirSubscription;

    public DesignSurfaceView()
    {
        InitializeComponent();
        Canvas.ActionRequested += (_, args) => _vm?.HandleDesignAction(args);
        Canvas.PointerPositionChanged += (_, p) => _vm?.OnCanvasPointerMoved(p);
    }

    public DesignCanvas DesignCanvas => Canvas;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.DesignInvalidated -= OnInvalidated;
            _vm.Surface = null;
        }
        _baseDirSubscription?.Dispose();
        _vm = DataContext as MainWindowViewModel;
        if (_vm is null) return;
        Canvas.Registry = _vm.Registry;
        Canvas.ElementFactory = _vm.CreateElement;
        _vm.Surface = Canvas;
        _vm.DesignInvalidated += OnInvalidated;
        _baseDirSubscription = _vm.WhenAnyValue(x => x.BaseDirectory).Subscribe(d => Canvas.BaseDirectory = d);
        Canvas.InvalidateDesign();
    }

    private void OnInvalidated(object? sender, EventArgs e)
    {
        Canvas.BaseDirectory = _vm?.BaseDirectory;
        Canvas.InvalidateDesign();
    }
}
