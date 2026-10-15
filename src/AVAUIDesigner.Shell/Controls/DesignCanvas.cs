using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Selection;
using AVAUIDesigner.Engine.UndoRedo;
using AVAUIDesigner.Engine.UndoRedo.Commands;
using AVAUIDesigner.Shell.Design;

namespace AVAUIDesigner.Shell.Controls;

/// <summary>
/// The design surface: renders the document as live controls and handles selection, move,
/// resize, rubber-band selection and toolbox drops. It never changes the document itself —
/// every change is raised through <see cref="ActionRequested"/> as an undoable command.
/// </summary>
public class DesignCanvas : Panel, IDesignSurface
{
    public const string DescriptorDataFormat = "AVAUIDesigner.ControlDescriptor";
    private const double SurfaceMargin = 40;
    private const double DragThreshold = 3;

    public static readonly StyledProperty<DesignDocument?> DocumentProperty =
        AvaloniaProperty.Register<DesignCanvas, DesignDocument?>(nameof(Document));

    public static readonly StyledProperty<SelectionManager?> SelectionManagerProperty =
        AvaloniaProperty.Register<DesignCanvas, SelectionManager?>(nameof(SelectionManager));

    public static readonly StyledProperty<double> ZoomFactorProperty =
        AvaloniaProperty.Register<DesignCanvas, double>(nameof(ZoomFactor), 1.0, coerce: (_, v) => Math.Clamp(v, 0.1, 8));

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<DesignCanvas, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<bool> SnapToGridProperty =
        AvaloniaProperty.Register<DesignCanvas, bool>(nameof(SnapToGrid), true);

    public static readonly StyledProperty<bool> SmartSnapProperty =
        AvaloniaProperty.Register<DesignCanvas, bool>(nameof(SmartSnap), true);

    public static readonly StyledProperty<double> GridSizeProperty =
        AvaloniaProperty.Register<DesignCanvas, double>(nameof(GridSize), 8);

    public static readonly StyledProperty<double> SnapThresholdProperty =
        AvaloniaProperty.Register<DesignCanvas, double>(nameof(SnapThreshold), 6);

    public static readonly StyledProperty<bool> ShowOutlinesProperty =
        AvaloniaProperty.Register<DesignCanvas, bool>(nameof(ShowOutlines), true);

    private readonly ScrollViewer _scroll;
    private readonly LayoutTransformControl _zoomHost;
    private readonly Panel _surface;
    private readonly Canvas _previewHost;
    private readonly AdornerLayer _adorner;
    private readonly TextBlock _emptyText;
    private readonly PlacementService _placement;
    private PreviewBuilder? _builder;
    private Control? _rootFrame;
    private readonly Dictionary<Control, DesignElement> _reverse = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<DesignElement, DesignElement> _selectedTabs = new(ReferenceEqualityComparer.Instance);
    private DragState? _drag;
    private bool _adornerRefreshQueued;

    public DesignCanvas()
    {
        Focusable = true;
        ClipToBounds = true;

        _previewHost = new Canvas();
        _adorner = new AdornerLayer();
        _surface = new Panel { Background = Brushes.Transparent };
        _surface.Children.Add(_previewHost);
        _surface.Children.Add(_adorner);
        _zoomHost = new LayoutTransformControl { Child = _surface };
        _scroll = new ScrollViewer
        {
            Content = _zoomHost,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _emptyText = new TextBlock
        {
            Text = "No document is open.\nUse File › New or File › Open to start designing.",
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Opacity = 0.6,
            IsHitTestVisible = false,
        };
        Children.Add(_scroll);
        Children.Add(_emptyText);

        _placement = new PlacementService(GetElementBounds, e => _builder?.Map.GetValueOrDefault(e));

        _surface.PointerPressed += OnSurfacePointerPressed;
        _surface.PointerMoved += OnSurfacePointerMoved;
        _surface.PointerReleased += OnSurfacePointerReleased;
        _surface.PointerCaptureLost += (_, _) => CancelDrag(revert: false);
        _surface.PointerWheelChanged += OnSurfaceWheel;
        _surface.PointerExited += (_, _) => PointerPositionChanged?.Invoke(this, null);
        _surface.LayoutUpdated += (_, _) => QueueAdornerRefresh();

        DragDrop.SetAllowDrop(_surface, true);
        _surface.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        _surface.AddHandler(DragDrop.DragLeaveEvent, (_, _) => ClearDropFeedback());
        _surface.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    // ------------------------------------------------------------------ public API

    public DesignDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public SelectionManager? SelectionManager
    {
        get => GetValue(SelectionManagerProperty);
        set => SetValue(SelectionManagerProperty, value);
    }

    public double ZoomFactor
    {
        get => GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public bool SnapToGrid
    {
        get => GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    public bool SmartSnap
    {
        get => GetValue(SmartSnapProperty);
        set => SetValue(SmartSnapProperty, value);
    }

    public double GridSize
    {
        get => GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    public double SnapThreshold
    {
        get => GetValue(SnapThresholdProperty);
        set => SetValue(SnapThresholdProperty, value);
    }

    public bool ShowOutlines
    {
        get => GetValue(ShowOutlinesProperty);
        set => SetValue(ShowOutlinesProperty, value);
    }

    /// <summary>Descriptor lookup for toolbox drops.</summary>
    public ControlDescriptorRegistry? Registry { get; set; }

    /// <summary>Creates the element for a toolbox drop (auto-naming, default children...). Defaults to the descriptor factory.</summary>
    public Func<ControlDescriptor, DesignElement>? ElementFactory { get; set; }

    /// <summary>Directory used to resolve relative image sources.</summary>
    public string? BaseDirectory { get; set; }

    /// <summary>Raised when a user action would change the document.</summary>
    public event EventHandler<DesignActionEventArgs>? ActionRequested;

    /// <summary>Pointer position relative to the root's client area (null when outside the surface).</summary>
    public event EventHandler<Point?>? PointerPositionChanged;

    /// <summary>The surface panel (design coordinates); exposed for tests.</summary>
    internal Panel Surface => _surface;

    /// <summary>The builder of the current preview; exposed for tests.</summary>
    internal PreviewBuilder? Builder => _builder;

    /// <summary>Force re-render from the current document state.</summary>
    public void InvalidateDesign()
    {
        _drag = null;
        _previewHost.Children.Clear();
        _reverse.Clear();
        var doc = Document;
        _emptyText.IsVisible = doc is null;
        _scroll.IsVisible = doc is not null;
        if (doc is null || Registry is null)
        {
            _builder = null;
            _rootFrame = null;
            UpdateAdorners();
            return;
        }

        _builder = new PreviewBuilder(Registry) { BaseDirectory = BaseDirectory };
        _rootFrame = _builder.BuildRoot(doc);
        _builder.ApplyImages();
        foreach (var (e, c) in _builder.Map) _reverse.TryAdd(c, e);

        if (ShowGrid && _builder.GridLayer is { } layer)
        {
            layer.Children.Add(new BackgroundGrid { GridSize = GridSize, Zoom = ZoomFactor });
        }

        Canvas.SetLeft(_rootFrame, SurfaceMargin);
        Canvas.SetTop(_rootFrame, SurfaceMargin);
        _previewHost.Children.Add(_rootFrame);
        RestoreSelectedTabs();
        UpdateSurfaceSize();
        QueueAdornerRefresh();
    }

    /// <summary>Hit-tests a point in design-surface coordinates.</summary>
    public DesignElement? HitTest(Point position) => HitTest(position, null);

    /// <summary>Hit-tests a point in this control's coordinates.</summary>
    public DesignElement? HitTestFromCanvas(Point canvasPoint)
    {
        var p = this.TranslatePoint(canvasPoint, _surface);
        return p is null ? null : HitTest(p.Value);
    }

    public void ZoomToFit()
    {
        if (_rootFrame is null) return;
        var viewport = _scroll.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0) viewport = Bounds.Size;
        var size = _rootFrame.DesiredSize + new Size(SurfaceMargin * 2, SurfaceMargin * 2);
        if (size.Width <= 0 || size.Height <= 0) return;
        ZoomFactor = Math.Clamp(Math.Min(viewport.Width / size.Width, viewport.Height / size.Height), 0.1, 4);
    }

    public void ZoomTo(double factor) => ZoomFactor = factor;

    public Rect? GetElementBounds(DesignElement element)
    {
        if (_builder is null || !_builder.Map.TryGetValue(element, out var control)) return null;
        if (!control.IsEffectivelyVisible) return null;
        var p = control.TranslatePoint(default, _surface);
        if (p is null) return null;
        return new Rect(p.Value, control.Bounds.Size);
    }

    public IReadOnlyDictionary<string, string?>? GetPlacementValues(DesignElement element, Rect newBounds)
    {
        var parent = element.Parent;
        if (parent is null || element.OwnerProperty is not null || !PlacementService.IsAbsolute(parent)) return null;
        return _placement.PlaceAt(parent, element, newBounds);
    }

    public IReadOnlyDictionary<string, string?>? GetPlacementValuesIn(DesignElement container, DesignElement element, Rect bounds) =>
        PlacementService.IsAbsolute(container) ? _placement.PlaceAt(container, element, bounds) : null;

    /// <summary>The root client area's origin in surface coordinates.</summary>
    public Point ClientOrigin => Document is { } d && GetElementBounds(d.RootElement) is { } r ? r.TopLeft : default;

    // ------------------------------------------------------------------ property changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DocumentProperty)
        {
            _selectedTabs.Clear();
            InvalidateDesign();
        }
        else if (change.Property == SelectionManagerProperty)
        {
            if (change.OldValue is SelectionManager old) old.SelectionChanged -= OnSelectionChanged;
            if (change.NewValue is SelectionManager sm) sm.SelectionChanged += OnSelectionChanged;
            UpdateAdorners();
        }
        else if (change.Property == ZoomFactorProperty)
        {
            var z = ZoomFactor;
            _zoomHost.LayoutTransform = new ScaleTransform(z, z);
            _adorner.Zoom = z;
            if (_builder?.GridLayer is { } layer)
                foreach (var g in layer.Children.OfType<BackgroundGrid>()) g.Zoom = z;
            QueueAdornerRefresh();
        }
        else if (change.Property == ShowGridProperty || change.Property == GridSizeProperty)
        {
            InvalidateDesign();
        }
        else if (change.Property == ShowOutlinesProperty)
        {
            UpdateAdorners();
        }
    }

    private void OnSelectionChanged(object? sender, AVAUIDesigner.Engine.Selection.SelectionChangedEventArgs e)
    {
        foreach (var element in e.Added) RevealTab(element);
        UpdateAdorners();
    }

    private void UpdateSurfaceSize()
    {
        if (_rootFrame is null) return;
        _rootFrame.Measure(Size.Infinity);
        var s = _rootFrame.DesiredSize;
        _surface.Width = s.Width + SurfaceMargin * 2;
        _surface.Height = s.Height + SurfaceMargin * 2;
    }

    // ------------------------------------------------------------------ tabs

    /// <summary>Shows the TabItem containing <paramref name="element"/> in the preview.</summary>
    private void RevealTab(DesignElement element)
    {
        for (var e = element; e is not null; e = e.Parent)
        {
            if (e.ElementType != "TabItem" || e.Parent is not { ElementType: "TabControl" } tabControl) continue;
            _selectedTabs[tabControl] = e;
            ApplySelectedTab(tabControl, e);
        }
    }

    private void RestoreSelectedTabs()
    {
        foreach (var (tc, tab) in _selectedTabs.ToList())
        {
            if (!ReferenceEquals(tab.Parent, tc)) _selectedTabs.Remove(tc);
            else ApplySelectedTab(tc, tab);
        }
    }

    private void ApplySelectedTab(DesignElement tabControl, DesignElement tab)
    {
        if (_builder is null) return;
        if (_builder.Map.GetValueOrDefault(tabControl) is TabControl tcc && _builder.Map.GetValueOrDefault(tab) is TabItem ti)
        {
            tcc.SelectedItem = ti;
            QueueAdornerRefresh();
        }
    }

    private DesignElement? SelectedTabOf(DesignElement tabControl)
    {
        if (_builder?.Map.GetValueOrDefault(tabControl) is TabControl { SelectedItem: Control c } && _reverse.TryGetValue(c, out var e))
            return e;
        return tabControl.Children.FirstOrDefault();
    }

    // ------------------------------------------------------------------ hit testing

    private DesignElement? HitTest(Point p, ISet<DesignElement>? exclude)
    {
        var doc = Document;
        if (doc is null || _builder is null) return null;
        DesignElement? best = null;
        var bestDepth = -1;
        foreach (var e in doc.AllElements)
        {
            if (ReferenceEquals(e, doc.RootElement)) continue;
            if (exclude is not null && IsExcluded(e, exclude)) continue;
            if (GetElementBounds(e) is not { } b || !b.Contains(p)) continue;
            var depth = e.Depth;
            if (depth >= bestDepth)
            {
                best = e;
                bestDepth = depth;
            }
        }
        if (best is not null) return best;
        return GetElementBounds(doc.RootElement) is { } rb && rb.Contains(p) ? doc.RootElement : null;
    }

    private static bool IsExcluded(DesignElement e, ISet<DesignElement> exclude)
    {
        for (var x = e; x is not null; x = x.Parent)
            if (exclude.Contains(x)) return true;
        return false;
    }

    /// <summary>The container that would receive a child of <paramref name="elementType"/> dropped at <paramref name="p"/>.</summary>
    private DesignElement? FindDropTarget(Point p, string elementType, ISet<DesignElement>? exclude, DesignElement? moving)
    {
        for (var e = HitTest(p, exclude); e is not null; e = e.Parent)
        {
            if (e.OwnerProperty is not null) continue;
            var d = e.Descriptor;
            if (d is null) continue;
            if (d.ElementName == "TabControl" && elementType != "TabItem" && SelectedTabOf(e) is { } tab
                && tab.Descriptor?.AcceptsChild(elementType) == true && HasCapacity(tab, moving))
                return tab;
            if (d.AcceptsChild(elementType) && HasCapacity(e, moving)) return e;
        }
        return null;
    }

    private static bool HasCapacity(DesignElement container, DesignElement? moving)
    {
        var max = container.Descriptor?.MaxChildren;
        if (max is null) return true;
        var count = container.Children.Count(c => !ReferenceEquals(c, moving));
        return count < max;
    }

    // ------------------------------------------------------------------ adorners

    private void QueueAdornerRefresh()
    {
        if (_adornerRefreshQueued) return;
        _adornerRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _adornerRefreshQueued = false;
            if (_drag is null || _drag.Mode is DragMode.Pending or DragMode.PendingRubber) UpdateAdorners();
        }, DispatcherPriority.Render);
    }

    private void UpdateAdorners()
    {
        var sm = SelectionManager;
        var doc = Document;
        var selections = new List<AdornedSelection>();
        var outlines = new List<Rect>();
        if (doc is not null && sm is not null)
        {
            foreach (var e in sm.SelectedElements)
            {
                if (GetElementBounds(e) is not { } b) continue;
                var isRoot = ReferenceEquals(e, doc.RootElement);
                var primary = ReferenceEquals(e, sm.PrimarySelection);
                var handles = !primary ? SelectionHandles.All
                    : isRoot ? SelectionHandles.RootOnly
                    : SelectionHandles.All;
                selections.Add(new AdornedSelection(b, primary, e.DisplayName, handles, e.IsLocked));
            }
        }
        if (doc is not null && ShowOutlines)
        {
            foreach (var e in doc.RootElement.SelfAndDescendants().Skip(1))
            {
                var d = e.Descriptor;
                if (d is null || !(d.IsContainer || d.ElementName == "Image")) continue;
                if (GetElementBounds(e) is { Width: > 0, Height: > 0 } b) outlines.Add(b);
            }
        }
        _adorner.Selections = selections;
        _adorner.Outlines = outlines;
        if (_drag is null)
        {
            _adorner.SnapLines = [];
            _adorner.Ghosts = [];
            _adorner.RubberBand = null;
            _adorner.InsertionLine = null;
            _adorner.DropTarget = null;
            _adorner.DropCell = null;
            _adorner.GhostLabel = null;
        }
        _adorner.InvalidateVisual();
    }

    // ------------------------------------------------------------------ pointer handling

    private enum DragMode
    {
        /// <summary>Pressed on an element; becomes Move after the drag threshold.</summary>
        Pending,
        /// <summary>Pressed on an unselected container; becomes RubberBand after the threshold.</summary>
        PendingRubber,
        Move,
        Resize,
        RubberBand,
    }

    private sealed class DragState
    {
        public DragMode Mode;
        public Point Start;
        public DesignElement? Element;
        public DesignElement? Container;
        public SelectionHandle Handle;
        public bool Additive;
        public List<DesignElement> Elements = [];
        public Dictionary<DesignElement, Rect> StartBounds = new(ReferenceEqualityComparer.Instance);
        public Rect Current;
        public Vector Delta;
        public DesignElement? Target;
        public int InsertIndex = -1;
        public IPointer? Pointer;
    }

    private void OnSurfacePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var doc = Document;
        var sm = SelectionManager;
        if (doc is null || sm is null) return;
        Focus();
        var p = e.GetPosition(_surface);
        var point = e.GetCurrentPoint(_surface);
        var hit = HitTest(p);

        if (point.Properties.IsRightButtonPressed)
        {
            if (hit is not null && !sm.IsSelected(hit)) sm.SetSelection(hit);
            return;
        }
        if (!point.Properties.IsLeftButtonPressed) return;

        if (e.ClickCount == 2 && hit is not null)
        {
            ActionRequested?.Invoke(this, new DesignActionEventArgs(DesignActionKind.CreateDefaultEventHandler, hit));
            e.Handled = true;
            return;
        }

        var additive = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        // Resize handles of the primary selection take precedence.
        if (!additive && sm.PrimarySelection is { IsLocked: false } primary && GetElementBounds(primary) is { } pb)
        {
            var isRoot = ReferenceEquals(primary, doc.RootElement);
            var handle = SelectionHandles.HitTest(pb, p, SelectionHandles.HitSize / ZoomFactor,
                isRoot ? SelectionHandles.RootOnly : SelectionHandles.All);
            if (handle != SelectionHandle.None)
            {
                _drag = new DragState
                {
                    Mode = DragMode.Resize, Start = p, Element = primary, Handle = handle, Current = pb, Pointer = e.Pointer,
                };
                e.Pointer.Capture(_surface);
                e.Handled = true;
                return;
            }
        }

        if (hit is null)
        {
            if (!additive) sm.ClearSelection();
            _drag = new DragState { Mode = DragMode.RubberBand, Start = p, Container = RubberContainer(doc.RootElement), Additive = additive, Pointer = e.Pointer };
            e.Pointer.Capture(_surface);
            e.Handled = true;
            return;
        }

        if (additive)
        {
            sm.ToggleSelection(hit);
            e.Handled = true;
            return;
        }

        var isContainer = hit.Descriptor?.IsContainer == true || ReferenceEquals(hit, doc.RootElement);
        if (isContainer && !sm.IsSelected(hit))
        {
            sm.SetSelection(hit);
            _drag = new DragState { Mode = DragMode.PendingRubber, Start = p, Element = hit, Container = RubberContainer(hit), Pointer = e.Pointer };
        }
        else
        {
            if (!sm.IsSelected(hit)) sm.SetSelection(hit);
            else sm.AddToSelection(hit); // make primary
            _drag = new DragState { Mode = DragMode.Pending, Start = p, Element = hit, Pointer = e.Pointer };
        }
        e.Pointer.Capture(_surface);
        e.Handled = true;
    }

    /// <summary>The root's single layout panel is the natural rubber-band container.</summary>
    private static DesignElement RubberContainer(DesignElement container) =>
        container.Parent is null && container.Children.Count == 1 && container.Children[0].Descriptor?.IsContainer == true
            ? container.Children[0]
            : container;

    private void OnSurfacePointerMoved(object? sender, PointerEventArgs e)
    {
        var p = e.GetPosition(_surface);
        var origin = ClientOrigin;
        PointerPositionChanged?.Invoke(this, new Point(Math.Round(p.X - origin.X), Math.Round(p.Y - origin.Y)));

        var drag = _drag;
        if (drag is null)
        {
            UpdateHoverCursor(p);
            return;
        }

        var delta = p - drag.Start;
        switch (drag.Mode)
        {
            case DragMode.Pending:
                if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) return;
                if (!BeginMove(drag)) return;
                break;
            case DragMode.PendingRubber:
                if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) return;
                drag.Mode = DragMode.RubberBand;
                break;
        }

        switch (drag.Mode)
        {
            case DragMode.Move:
                UpdateMove(drag, p);
                break;
            case DragMode.Resize:
                UpdateResize(drag, delta);
                break;
            case DragMode.RubberBand:
                _adorner.RubberBand = new Rect(drag.Start, p).Normalize();
                _adorner.InvalidateVisual();
                break;
        }
    }

    private void UpdateHoverCursor(Point p)
    {
        var cursor = StandardCursorType.Arrow;
        if (SelectionManager?.PrimarySelection is { IsLocked: false } primary && Document is { } doc && GetElementBounds(primary) is { } b)
        {
            var handles = ReferenceEquals(primary, doc.RootElement) ? SelectionHandles.RootOnly : SelectionHandles.All;
            var h = SelectionHandles.HitTest(b, p, SelectionHandles.HitSize / ZoomFactor, handles);
            if (h != SelectionHandle.None) cursor = SelectionHandles.Cursor(h);
            else if (b.Contains(p) && !ReferenceEquals(primary, doc.RootElement)) cursor = StandardCursorType.SizeAll;
        }
        _surface.Cursor = new Cursor(cursor);
    }

    private void OnSurfacePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var drag = _drag;
        if (drag is null) return;
        _drag = null;
        e.Pointer.Capture(null);
        var p = e.GetPosition(_surface);

        switch (drag.Mode)
        {
            case DragMode.Move:
                CommitMove(drag);
                break;
            case DragMode.Resize:
                CommitResize(drag);
                break;
            case DragMode.RubberBand:
                CommitRubberBand(drag, new Rect(drag.Start, p).Normalize());
                break;
        }
        UpdateAdorners();
    }

    private void OnSurfaceWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        ZoomFactor *= e.Delta.Y > 0 ? 1.1 : 1 / 1.1;
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _drag is not null)
        {
            CancelDrag(revert: true);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private void CancelDrag(bool revert)
    {
        if (_drag is null) return;
        var wasLive = _drag.Mode is DragMode.Move or DragMode.Resize;
        _drag.Pointer?.Capture(null);
        _drag = null;
        if (revert && wasLive) InvalidateDesign();
        else UpdateAdorners();
    }

    // ------------------------------------------------------------------ move

    private bool BeginMove(DragState drag)
    {
        var doc = Document!;
        var sm = SelectionManager!;
        var selected = new HashSet<DesignElement>(sm.SelectedElements, ReferenceEqualityComparer.Instance);
        // Move only top-most selected elements; never the root or locked ones.
        drag.Elements = sm.SelectedElements
            .Where(x => !ReferenceEquals(x, doc.RootElement) && !x.IsLocked && x.OwnerProperty is null)
            .Where(x => !Ancestors(x).Any(selected.Contains))
            .ToList();
        if (drag.Elements.Count == 0)
        {
            _drag = null;
            return false;
        }
        foreach (var x in drag.Elements)
            if (GetElementBounds(x) is { } b) drag.StartBounds[x] = b;
        if (drag.Element is null || !drag.StartBounds.ContainsKey(drag.Element)) drag.Element = drag.Elements[0];
        drag.Mode = DragMode.Move;
        return drag.StartBounds.ContainsKey(drag.Element);
    }

    private static IEnumerable<DesignElement> Ancestors(DesignElement e)
    {
        for (var p = e.Parent; p is not null; p = p.Parent) yield return p;
    }

    private void UpdateMove(DragState drag, Point p)
    {
        var primary = drag.Element!;
        var start = drag.StartBounds[primary];
        var delta = p - drag.Start;
        var exclude = new HashSet<DesignElement>(drag.Elements, ReferenceEqualityComparer.Instance);

        // Multi-selection moves stay within their parents; a single element can be re-parented.
        var target = drag.Elements.Count == 1
            ? FindDropTarget(p, primary.ElementType, exclude, primary) ?? primary.Parent
            : primary.Parent;
        drag.Target = target;
        var lines = new List<SnapLine>();
        _adorner.InsertionLine = null;
        _adorner.DropCell = null;
        _adorner.DropTarget = target is not null && !ReferenceEquals(target, primary.Parent) ? GetElementBounds(target) : null;

        if (PlacementService.IsAbsolute(target))
        {
            var proposed = start.Translate(delta);
            var siblings = target!.Children.Where(c => !exclude.Contains(c)).Select(GetElementBounds)
                .Where(b => b is not null).Select(b => b!.Value).ToList();
            var parentRect = GetElementBounds(target);
            var snap = SnapEngine.Snap(proposed, siblings, parentRect, SnapEdges.All, SnapOptions(), parentRect?.TopLeft ?? default);
            delta += new Vector(snap.DeltaX, snap.DeltaY);
            lines.AddRange(snap.Lines);
            if (PlacementService.ModeOf(target) == ContainerLayoutMode.Grid)
            {
                var (_, _, cell) = _placement.GridCellAt(target, start.Translate(delta).TopLeft + new Vector(2, 2));
                _adorner.DropCell = cell;
            }
            drag.InsertIndex = -1;
        }
        else if (PlacementService.IsOrdered(target))
        {
            drag.InsertIndex = _placement.InsertionIndex(target!, p, primary, out var line);
            _adorner.InsertionLine = line;
        }
        else
        {
            drag.InsertIndex = -1;
        }

        drag.Delta = delta;
        _adorner.Ghosts = drag.Elements.Where(drag.StartBounds.ContainsKey).Select(x => drag.StartBounds[x].Translate(delta)).ToList();
        _adorner.SnapLines = lines;
        _adorner.Selections = [];
        _adorner.GhostLabel = primary.DisplayName;
        _adorner.InvalidateVisual();
    }

    private void CommitMove(DragState drag)
    {
        var primary = drag.Element!;
        var target = drag.Target ?? primary.Parent;
        if (target is null) return;
        var commands = new List<IDesignCommand>();

        if (drag.Delta.Length < 0.5 && ReferenceEquals(target, primary.Parent)) return;

        if (!ReferenceEquals(target, primary.Parent))
        {
            var values = PlacementService.CleanupForNewParent(primary, target);
            if (PlacementService.IsAbsolute(target))
                foreach (var (k, v) in _placement.PlaceAt(target, primary, drag.StartBounds[primary].Translate(drag.Delta))) values[k] = v;
            commands.Add(new ReparentCommand(primary, target, PlacementService.IsOrdered(target) ? drag.InsertIndex : -1, values));
        }
        else if (PlacementService.IsAbsolute(target))
        {
            foreach (var x in drag.Elements)
            {
                if (!drag.StartBounds.TryGetValue(x, out var b) || x.Parent is null || !PlacementService.IsAbsolute(x.Parent)) continue;
                var cmd = new MoveElementCommand(x, _placement.PlaceAt(x.Parent, x, b.Translate(drag.Delta)));
                if (!cmd.IsNoOp) commands.Add(cmd);
            }
        }
        else if (PlacementService.IsOrdered(target) && drag.Elements.Count == 1 && drag.InsertIndex >= 0)
        {
            var current = primary.IndexInParent;
            if (drag.InsertIndex != current && drag.InsertIndex != current + 1)
                commands.Add(new ReparentCommand(primary, target, drag.InsertIndex));
        }

        Raise(commands, commands.Count == 1 ? commands[0].Description : $"Move {commands.Count} elements", drag.Elements);
    }

    // ------------------------------------------------------------------ resize

    private void UpdateResize(DragState drag, Vector delta)
    {
        var element = drag.Element!;
        var start = drag.StartBounds.TryGetValue(element, out var sb) ? sb : drag.Current;
        if (!drag.StartBounds.ContainsKey(element)) drag.StartBounds[element] = start;
        var min = element.Descriptor?.MinimumSize ?? new Size(4, 4);
        var rect = SelectionHandles.Resize(start, drag.Handle, delta, min);

        var lines = new List<SnapLine>();
        var parent = element.Parent;
        if (parent is not null)
        {
            var siblings = parent.Children.Where(c => !ReferenceEquals(c, element)).Select(GetElementBounds)
                .Where(b => b is not null).Select(b => b!.Value).ToList();
            var parentRect = GetElementBounds(parent);
            var edges = SelectionHandles.MovingEdges(drag.Handle);
            var snap = SnapEngine.Snap(rect, siblings, parentRect, edges, SnapOptions(), parentRect?.TopLeft ?? default);
            rect = SelectionHandles.Resize(start, drag.Handle, delta + new Vector(snap.DeltaX, snap.DeltaY), min);
            lines.AddRange(snap.Lines);
        }
        else if (SnapToGrid)
        {
            // Root: snap the size to the grid.
            var g = GridSize;
            rect = new Rect(rect.X, rect.Y, Math.Max(g, Math.Round(rect.Width / g) * g), Math.Max(g, Math.Round(rect.Height / g) * g));
        }

        drag.Current = rect;
        _adorner.Ghosts = [rect];
        _adorner.GhostLabel = $"{Math.Round(rect.Width)} × {Math.Round(rect.Height)}";
        _adorner.SnapLines = lines;
        _adorner.Selections = [];
        _adorner.InvalidateVisual();
    }

    private void CommitResize(DragState drag)
    {
        var element = drag.Element!;
        if (!drag.StartBounds.TryGetValue(element, out var start)) return;
        var rect = drag.Current;
        if (Math.Abs(rect.Width - start.Width) < 0.5 && Math.Abs(rect.Height - start.Height) < 0.5
                                                      && Math.Abs(rect.X - start.X) < 0.5 && Math.Abs(rect.Y - start.Y) < 0.5)
            return;

        var values = new Dictionary<string, string?>();
        var isRoot = element.Parent is null;
        var useDesignSize = isRoot && element.ElementType != "Window" && element.GetValue("Width") is null && element.GetValue("Height") is null;
        var widthKey = useDesignSize ? "d:DesignWidth" : "Width";
        var heightKey = useDesignSize ? "d:DesignHeight" : "Height";
        var edges = SelectionHandles.MovingEdges(drag.Handle);
        if (edges.HasFlag(SnapEdges.Left) || edges.HasFlag(SnapEdges.Right)) values[widthKey] = PlacementService.Fmt(rect.Width);
        if (edges.HasFlag(SnapEdges.Top) || edges.HasFlag(SnapEdges.Bottom)) values[heightKey] = PlacementService.Fmt(rect.Height);

        if (!isRoot && (edges.HasFlag(SnapEdges.Left) || edges.HasFlag(SnapEdges.Top) || PlacementService.ModeOf(element.Parent) == ContainerLayoutMode.Grid)
            && GetPlacementValues(element, rect) is { } placement)
        {
            foreach (var (k, v) in placement)
                if (k is not ("Width" or "Height")) values[k] = v;
        }

        var cmd = new ResizeElementCommand(element, values);
        if (!cmd.IsNoOp) Raise([cmd], cmd.Description, [element]);
    }

    // ------------------------------------------------------------------ rubber band

    private void CommitRubberBand(DragState drag, Rect band)
    {
        var sm = SelectionManager!;
        var container = drag.Container;
        if (container is null) return;
        var hits = container.Children.Where(c => GetElementBounds(c) is { } b && b.Intersects(band)).ToList();
        if (drag.Additive)
        {
            foreach (var h in hits) sm.AddToSelection(h);
        }
        else if (hits.Count > 0)
        {
            sm.SetSelection(hits);
        }
    }

    // ------------------------------------------------------------------ toolbox drag & drop

    private ControlDescriptor? DraggedDescriptor(DragEventArgs e)
    {
#pragma warning disable CS0618 // DataObject API is the stable in-process drag payload in Avalonia 11
        var name = e.Data.Get(DescriptorDataFormat) as string ?? (e.Data.Contains(DataFormats.Text) ? e.Data.GetText() : null);
#pragma warning restore CS0618
        return name is null ? null : Registry?.GetByElementName(name);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var descriptor = DraggedDescriptor(e);
        if (descriptor is null || Document is null)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        var p = e.GetPosition(_surface);
        var plan = PlanDrop(descriptor, p);
        e.DragEffects = plan is null ? DragDropEffects.None : DragDropEffects.Copy;
        _adorner.Ghosts = plan?.Ghost is { } g ? [g] : [];
        _adorner.GhostLabel = descriptor.ElementName;
        _adorner.DropTarget = plan?.TargetBounds;
        _adorner.InsertionLine = plan?.Line;
        _adorner.DropCell = plan?.Cell;
        _adorner.SnapLines = plan?.Lines ?? [];
        _adorner.InvalidateVisual();
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var descriptor = DraggedDescriptor(e);
        ClearDropFeedback();
        if (descriptor is null || Document is null) return;
        var p = e.GetPosition(_surface);
        var plan = PlanDrop(descriptor, p);
        if (plan is null) return;
        Focus();
        DropDescriptor(descriptor, plan);
        e.Handled = true;
    }

    private sealed record DropPlan(DesignElement Target, Rect? Ghost, Rect? TargetBounds, (Point, Point)? Line, Rect? Cell,
        int Index, string? PropertyElement, IReadOnlyList<SnapLine> Lines);

    private DropPlan? PlanDrop(ControlDescriptor descriptor, Point p)
    {
        if (descriptor.IsRootOnly) return null;
        if (descriptor.AttachToPropertyOnDrop is { } propName)
        {
            // Walk up from the control under the pointer to the first one that has the property (e.g. a Button for Flyout).
            var host = HitTest(p);
            while (host is not null && !descriptor.CanAttachTo(host)) host = host.Parent;
            if (host is null) return null;
            return new DropPlan(host, null, GetElementBounds(host), null, null, -1, propName, []);
        }

        var target = FindDropTarget(p, descriptor.ElementName, null, null);
        if (target is null) return null;
        var size = descriptor.DefaultSize;
        var ghostSize = new Size(double.IsNaN(size.Width) ? 80 : size.Width, double.IsNaN(size.Height) ? 24 : size.Height);
        var ghost = new Rect(p, ghostSize);
        var lines = new List<SnapLine>();
        (Point, Point)? line = null;
        Rect? cell = null;
        var index = -1;

        if (PlacementService.IsAbsolute(target))
        {
            var parentRect = GetElementBounds(target);
            var siblings = target.Children.Select(GetElementBounds).Where(b => b is not null).Select(b => b!.Value).ToList();
            var snap = SnapEngine.Snap(ghost, siblings, parentRect, SnapEdges.All, SnapOptions(), parentRect?.TopLeft ?? default);
            ghost = ghost.Translate(new Vector(snap.DeltaX, snap.DeltaY));
            lines.AddRange(snap.Lines);
            if (PlacementService.ModeOf(target) == ContainerLayoutMode.Grid)
                cell = _placement.GridCellAt(target, ghost.TopLeft + new Vector(2, 2)).Cell;
        }
        else if (PlacementService.IsOrdered(target))
        {
            index = _placement.InsertionIndex(target, p, null, out line);
        }

        return new DropPlan(target, ghost, GetElementBounds(target), line, cell, index, null, lines);
    }

    private void DropDescriptor(ControlDescriptor descriptor, DropPlan plan)
    {
        var element = ElementFactory?.Invoke(descriptor) ?? descriptor.DefaultElementFactory();
        IDesignCommand command;
        if (plan.PropertyElement is { } propName)
        {
            var prop = new DesignProperty(propName, null) { IsAttributeForm = false };
            prop.ElementValues.Add(element);
            command = new ReplacePropertyCommand(plan.Target, propName, prop, $"Add {descriptor.ElementName} to {plan.Target.DisplayName}");
        }
        else
        {
            if (PlacementService.IsAbsolute(plan.Target) && plan.Ghost is { } ghost)
            {
                // A new element keeps its default size; only its position comes from the drop point.
                foreach (var (k, v) in _placement.PlaceAt(plan.Target, element, ghost))
                    if (k is not ("Width" or "Height")) element.SetValue(k, v);
            }
            command = new AddElementCommand(plan.Target, element, plan.Index);
        }
        ActionRequested?.Invoke(this, new DesignActionEventArgs(command) { SelectAfter = [element] });
    }

    private void ClearDropFeedback()
    {
        _adorner.Ghosts = [];
        _adorner.DropTarget = null;
        _adorner.InsertionLine = null;
        _adorner.DropCell = null;
        _adorner.SnapLines = [];
        _adorner.GhostLabel = null;
        _adorner.InvalidateVisual();
    }

    // ------------------------------------------------------------------ helpers

    private SnapOptions SnapOptions() => new(SnapThreshold / Math.Max(ZoomFactor, 0.1), SmartSnap, SnapToGrid, GridSize);

    private void Raise(List<IDesignCommand> commands, string description, IReadOnlyList<DesignElement> select)
    {
        if (commands.Count == 0)
        {
            InvalidateDesign();
            return;
        }
        var command = commands.Count == 1 ? commands[0] : new CompositeCommand(description, commands);
        ActionRequested?.Invoke(this, new DesignActionEventArgs(command) { SelectAfter = select });
    }
}
