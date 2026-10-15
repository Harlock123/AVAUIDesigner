using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AVAUIDesigner.Shell.ViewModels;

namespace AVAUIDesigner.Shell.Views;

/// <summary>Document Outline: two-way selection sync with the canvas and drag-to-reparent.</summary>
public partial class DocumentOutlineView : UserControl
{
    private const string NodeFormat = "AVAUIDesigner.OutlineNode";
    private bool _syncing;
    private DesignElementNodeViewModel? _pressedNode;
    private DesignElementNodeViewModel? _draggedNode;
    private Point _pressPoint;
    private DocumentOutlineViewModel? _outline;

    public DocumentOutlineView()
    {
        InitializeComponent();
        Tree.SelectionChanged += OnTreeSelectionChanged;
        Tree.AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        Tree.AddHandler(PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
        Tree.AddHandler(PointerReleasedEvent, (_, _) => _pressedNode = null, RoutingStrategies.Tunnel);
        Tree.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        Tree.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_outline is not null) _outline.SelectionSynced -= OnSelectionSynced;
        _outline = (DataContext as MainWindowViewModel)?.DocumentOutline;
        if (_outline is not null) _outline.SelectionSynced += OnSelectionSynced;
    }

    private void OnSelectionSynced(object? sender, EventArgs e)
    {
        if (_outline?.RootNode is not { } root || _syncing) return;
        _syncing = true;
        try
        {
            var selected = root.SelfAndDescendants().Where(n => n.IsSelected).ToList();
            Tree.SelectedItems.Clear();
            foreach (var n in selected) Tree.SelectedItems.Add(n);
            if (selected.Count > 0 && Tree.ContainerFromItem(selected[^1]) is { } container) container.BringIntoView();
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _outline is null) return;
        _syncing = true;
        try
        {
            _outline.OnTreeSelectionChanged(Tree.SelectedItems.OfType<DesignElementNodeViewModel>());
        }
        finally
        {
            _syncing = false;
        }
    }

    private static DesignElementNodeViewModel? NodeFrom(object? source)
    {
        for (var c = source as Control; c is not null; c = c.GetVisualParent() as Control)
            if (c is TreeViewItem { DataContext: DesignElementNodeViewModel node }) return node;
        return null;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Tree).Properties.IsLeftButtonPressed) return;
        _pressedNode = NodeFrom(e.Source);
        _pressPoint = e.GetPosition(Tree);
    }

    private async void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedNode is not { } node || node.Parent is null) return;
        var p = e.GetPosition(Tree);
        if (Math.Abs(p.X - _pressPoint.X) < 5 && Math.Abs(p.Y - _pressPoint.Y) < 5) return;
        _pressedNode = null;
        _draggedNode = node;
#pragma warning disable CS0618
        var data = new DataObject();
        data.Set(NodeFormat, "node");
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
#pragma warning restore CS0618
        _draggedNode = null;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618
        var isNode = e.Data.Contains(NodeFormat);
#pragma warning restore CS0618
        var target = NodeFrom(e.Source);
        e.DragEffects = isNode && _draggedNode is { } src && target is not null && DocumentOutlineViewModel.CanReparent(src, target)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var target = NodeFrom(e.Source);
        if (_draggedNode is { } src && target is not null && DocumentOutlineViewModel.CanReparent(src, target))
            _outline?.ReparentAt?.Invoke(src, target, -1);
        e.Handled = true;
    }
}
