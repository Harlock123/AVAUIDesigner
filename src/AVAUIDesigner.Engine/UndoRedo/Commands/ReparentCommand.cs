using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Engine.UndoRedo.Commands;

/// <summary>
/// Moves an element to a different parent (or a different index within the same parent),
/// optionally changing placement properties (e.g. removing Canvas.Left, adding Grid.Row).
/// </summary>
public class ReparentCommand : IDesignCommand
{
    private DesignElement? _oldParent;
    private DesignProperty? _oldOwnerProperty;
    private int _oldIndex;
    private readonly Dictionary<string, string?> _oldValues = new();

    public ReparentCommand(DesignElement element, DesignElement newParent, int newIndex = -1,
        IReadOnlyDictionary<string, string?>? placementValues = null)
    {
        if (ReferenceEquals(element, newParent) || element.IsAncestorOf(newParent))
            throw new InvalidOperationException("An element cannot be moved into itself.");
        Element = element;
        NewParent = newParent;
        NewIndex = newIndex;
        PlacementValues = placementValues ?? new Dictionary<string, string?>();
    }

    public DesignElement Element { get; }
    public DesignElement NewParent { get; }
    public int NewIndex { get; }
    public IReadOnlyDictionary<string, string?> PlacementValues { get; }

    public string Description => ReferenceEquals(_oldParent ?? Element.Parent, NewParent)
        ? $"Reorder {Element.DisplayName}"
        : $"Move {Element.DisplayName} into {NewParent.DisplayName}";

    public void Execute(DesignDocument document)
    {
        _oldParent = Element.Parent ?? throw new InvalidOperationException("The root element cannot be moved.");
        _oldOwnerProperty = Element.OwnerProperty;
        _oldIndex = Element.IndexInParent;
        _oldValues.Clear();
        foreach (var key in PlacementValues.Keys) _oldValues[key] = Element.GetValue(key);

        var index = NewIndex;
        // Moving down within the same list: removing first shifts the target index.
        if (ReferenceEquals(_oldParent, NewParent) && _oldOwnerProperty is null && index > _oldIndex) index--;
        Element.Detach();
        NewParent.AddChild(Element, index);
        foreach (var (k, v) in PlacementValues) Element.SetValue(k, v);
    }

    public void Undo(DesignDocument document)
    {
        if (_oldParent is null) return;
        Element.Detach();
        if (_oldOwnerProperty is not null)
        {
            if (!_oldParent.Properties.Contains(_oldOwnerProperty)) _oldParent.Properties.Add(_oldOwnerProperty);
            _oldParent.AddToPropertyElement(_oldOwnerProperty, Element, _oldIndex);
        }
        else _oldParent.AddChild(Element, _oldIndex);
        foreach (var (k, v) in _oldValues) Element.SetValue(k, v);
    }
}
