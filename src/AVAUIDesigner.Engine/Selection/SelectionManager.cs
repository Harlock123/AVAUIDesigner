using AVAUIDesigner.Engine.Document;
using ReactiveUI;

namespace AVAUIDesigner.Engine.Selection;

public class SelectionChangedEventArgs(IReadOnlyList<DesignElement> added, IReadOnlyList<DesignElement> removed) : EventArgs
{
    public IReadOnlyList<DesignElement> Added { get; } = added;
    public IReadOnlyList<DesignElement> Removed { get; } = removed;
}

/// <summary>Tracks the selected elements. The primary selection is the last one clicked.</summary>
public class SelectionManager : ReactiveObject
{
    private readonly List<DesignElement> _selected = [];
    private DesignDocument? _document;

    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    public DesignDocument? Document
    {
        get => _document;
        set
        {
            ClearSelection();
            this.RaiseAndSetIfChanged(ref _document, value);
        }
    }

    public IReadOnlyList<DesignElement> SelectedElements => _selected;

    public DesignElement? PrimarySelection => _selected.Count > 0 ? _selected[^1] : null;

    public bool HasSelection => _selected.Count > 0;

    public bool IsMultiSelection => _selected.Count > 1;

    public bool IsSelected(DesignElement element) => _selected.Contains(element);

    public void SetSelection(DesignElement element) => SetSelection([element]);

    public void SetSelection(IEnumerable<DesignElement> elements)
    {
        var newList = elements.Distinct().ToList();
        if (newList.SequenceEqual(_selected)) return;
        var removed = _selected.Except(newList).ToList();
        var added = newList.Except(_selected).ToList();
        _selected.Clear();
        _selected.AddRange(newList);
        Raise(added, removed);
    }

    public void AddToSelection(DesignElement element)
    {
        if (_selected.Contains(element))
        {
            // Re-clicking makes it primary.
            _selected.Remove(element);
            _selected.Add(element);
            Raise([], []);
            return;
        }
        _selected.Add(element);
        Raise([element], []);
    }

    public void RemoveFromSelection(DesignElement element)
    {
        if (_selected.Remove(element)) Raise([], [element]);
    }

    public void ToggleSelection(DesignElement element)
    {
        if (_selected.Contains(element)) RemoveFromSelection(element);
        else AddToSelection(element);
    }

    /// <summary>Selects every element except the root.</summary>
    public void SelectAll()
    {
        if (_document is null) return;
        SetSelection(_document.RootElement.SelfAndDescendants().Skip(1));
    }

    public void ClearSelection()
    {
        if (_selected.Count == 0) return;
        var removed = _selected.ToList();
        _selected.Clear();
        Raise([], removed);
    }

    /// <summary>Drops selected elements that are no longer part of the document (after undo, delete...).</summary>
    public void Prune()
    {
        if (_document is null)
        {
            ClearSelection();
            return;
        }
        var alive = new HashSet<DesignElement>(_document.AllElements, ReferenceEqualityComparer.Instance);
        var removed = _selected.Where(e => !alive.Contains(e)).ToList();
        if (removed.Count == 0) return;
        foreach (var r in removed) _selected.Remove(r);
        Raise([], removed);
    }

    private void Raise(IReadOnlyList<DesignElement> added, IReadOnlyList<DesignElement> removed)
    {
        foreach (var r in removed) r.IsSelected = false;
        foreach (var e in _selected) e.IsSelected = true;
        this.RaisePropertyChanged(nameof(SelectedElements));
        this.RaisePropertyChanged(nameof(PrimarySelection));
        this.RaisePropertyChanged(nameof(HasSelection));
        this.RaisePropertyChanged(nameof(IsMultiSelection));
        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(added, removed));
    }
}
