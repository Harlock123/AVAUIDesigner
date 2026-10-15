using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia.Media;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Selection;
using ReactiveUI;

namespace AVAUIDesigner.Shell.ViewModels;

/// <summary>Tree node wrapping one design element.</summary>
public class DesignElementNodeViewModel : ReactiveObject
{
    private bool _isSelected;
    private bool _isExpanded = true;

    public DesignElementNodeViewModel(DesignElement element, DesignElementNodeViewModel? parent, string? propertyName = null)
    {
        Element = element;
        Parent = parent;
        PropertyName = propertyName;
    }

    public DesignElement Element { get; }
    public DesignElementNodeViewModel? Parent { get; }

    /// <summary>Set when the element lives in a property element ("ContextMenu").</summary>
    public string? PropertyName { get; }

    /// <summary>x:Name or type name.</summary>
    public string DisplayName => Element.Name is { } n ? $"{n}" : Element.ElementType;

    public string TypeName => Element.Name is null ? string.Empty : Element.ElementType;

    public string? PropertyLabel => PropertyName is null ? null : $"[{PropertyName}]";

    public IImage? Icon => Element.Descriptor?.Icon;

    public bool IsLocked => Element.IsLocked;
    public bool IsHidden => !Element.IsVisible;

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public ObservableCollection<DesignElementNodeViewModel> Children { get; } = [];

    public IEnumerable<DesignElementNodeViewModel> SelfAndDescendants()
    {
        yield return this;
        foreach (var c in Children)
        foreach (var d in c.SelfAndDescendants())
            yield return d;
    }

    public override string ToString() => DisplayName;
}

/// <summary>The Document Outline: the element tree, kept in two-way selection sync with the canvas.</summary>
public class DocumentOutlineViewModel : ReactiveObject
{
    private DesignElementNodeViewModel? _rootNode;

    public DocumentOutlineViewModel(SelectionManager selectionManager,
        Action<DesignElementNodeViewModel, DesignElementNodeViewModel, int>? reparent = null)
    {
        SelectionManager = selectionManager;
        SelectNodeCommand = ReactiveCommand.Create<DesignElementNodeViewModel>(n => SelectionManager.SetSelection(n.Element));
        ReparentCommand = ReactiveCommand.Create<(DesignElementNodeViewModel source, DesignElementNodeViewModel target)>(
            t => reparent?.Invoke(t.source, t.target, -1));
        ReparentAt = reparent;
        selectionManager.SelectionChanged += (_, _) => SyncSelection();
    }

    public SelectionManager SelectionManager { get; }

    public DesignElementNodeViewModel? RootNode
    {
        get => _rootNode;
        private set
        {
            this.RaiseAndSetIfChanged(ref _rootNode, value);
            this.RaisePropertyChanged(nameof(RootNodes));
        }
    }

    /// <summary>The root as a one-item list (TreeView ItemsSource).</summary>
    public IReadOnlyList<DesignElementNodeViewModel> RootNodes => RootNode is null ? [] : [RootNode];

    public ReactiveCommand<DesignElementNodeViewModel, Unit> SelectNodeCommand { get; }

    public ReactiveCommand<(DesignElementNodeViewModel source, DesignElementNodeViewModel target), Unit> ReparentCommand { get; }

    /// <summary>Reparent with an explicit insertion index (used by tree drag and drop).</summary>
    public Action<DesignElementNodeViewModel, DesignElementNodeViewModel, int>? ReparentAt { get; }

    /// <summary>Raised after <see cref="SyncSelection"/> so the view can update the TreeView's selected items.</summary>
    public event EventHandler? SelectionSynced;

    /// <summary>Rebuilds the tree, keeping expansion state.</summary>
    public void Rebuild(DesignDocument? document)
    {
        var collapsed = RootNode?.SelfAndDescendants().Where(n => !n.IsExpanded).Select(n => n.Element)
            .ToHashSet(ReferenceEqualityComparer.Instance) ?? [];
        RootNode = document is null ? null : Build(document.RootElement, null, null, collapsed);
        SyncSelection();
    }

    private static DesignElementNodeViewModel Build(DesignElement e, DesignElementNodeViewModel? parent, string? propertyName,
        HashSet<object?> collapsed)
    {
        var node = new DesignElementNodeViewModel(e, parent, propertyName) { IsExpanded = !collapsed.Contains(e) };
        foreach (var prop in e.PropertyElements)
        foreach (var v in prop.ElementValues)
            if (v.Descriptor is not null) node.Children.Add(Build(v, node, prop.Name, collapsed));
        foreach (var c in e.Children) node.Children.Add(Build(c, node, null, collapsed));
        return node;
    }

    public DesignElementNodeViewModel? FindNode(DesignElement element) =>
        RootNode?.SelfAndDescendants().FirstOrDefault(n => ReferenceEquals(n.Element, element));

    /// <summary>Pushes the SelectionManager state into the nodes (expanding ancestors of selected nodes).</summary>
    public void SyncSelection()
    {
        if (RootNode is null) return;
        foreach (var n in RootNode.SelfAndDescendants())
        {
            var selected = SelectionManager.IsSelected(n.Element);
            n.IsSelected = selected;
            if (!selected) continue;
            for (var p = n.Parent; p is not null; p = p.Parent) p.IsExpanded = true;
        }
        SelectionSynced?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called by the view when the user changes the TreeView selection.</summary>
    public void OnTreeSelectionChanged(IEnumerable<DesignElementNodeViewModel> selected)
    {
        var elements = selected.Select(n => n.Element).ToList();
        if (elements.Count == 0) return;
        SelectionManager.SetSelection(elements);
    }

    /// <summary>True when <paramref name="source"/> may be dropped into <paramref name="target"/>.</summary>
    public static bool CanReparent(DesignElementNodeViewModel source, DesignElementNodeViewModel target)
    {
        if (ReferenceEquals(source.Element, target.Element) || source.Element.IsAncestorOf(target.Element)) return false;
        if (source.Element.Parent is null || source.PropertyName is not null) return false;
        var d = target.Element.Descriptor;
        if (d is null || !d.AcceptsChild(source.Element.ElementType)) return false;
        var max = d.MaxChildren;
        return max is null || target.Element.Children.Count(c => !ReferenceEquals(c, source.Element)) < max;
    }
}
