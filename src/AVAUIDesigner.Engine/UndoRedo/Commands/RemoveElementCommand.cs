using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Engine.UndoRedo.Commands;

/// <summary>Removes an element; undo re-adds it at the same position.</summary>
public class RemoveElementCommand : IDesignCommand
{
    private DesignElement? _parent;
    private DesignProperty? _ownerProperty;
    private int _index;

    public RemoveElementCommand(DesignElement element)
    {
        Element = element;
    }

    public DesignElement Element { get; }

    public string Description => $"Delete {Element.DisplayName}";

    public void Execute(DesignDocument document)
    {
        _parent = Element.Parent ?? throw new InvalidOperationException("The root element cannot be removed.");
        _ownerProperty = Element.OwnerProperty;
        _index = Element.IndexInParent;
        Element.Detach();
    }

    public void Undo(DesignDocument document)
    {
        if (_parent is null) return;
        if (_ownerProperty is not null)
        {
            if (!_parent.Properties.Contains(_ownerProperty)) _parent.Properties.Add(_ownerProperty);
            _parent.AddToPropertyElement(_ownerProperty, Element, _index);
        }
        else
        {
            _parent.AddChild(Element, _index);
        }
    }
}
