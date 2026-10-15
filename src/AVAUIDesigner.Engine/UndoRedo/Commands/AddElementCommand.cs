using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Engine.UndoRedo.Commands;

/// <summary>Adds an element to a parent's children, or to one of its property elements.</summary>
public class AddElementCommand : IDesignCommand
{
    public AddElementCommand(DesignElement parent, DesignElement element, int index = -1, string? propertyElement = null)
    {
        Parent = parent;
        Element = element;
        Index = index;
        PropertyElement = propertyElement;
    }

    public DesignElement Parent { get; }
    public DesignElement Element { get; }
    public int Index { get; }
    public string? PropertyElement { get; }
    private bool _createdPropertyElement;

    public string Description => $"Add {Element.DisplayName}";

    public void Execute(DesignDocument document)
    {
        if (PropertyElement is null)
        {
            Parent.AddChild(Element, Index);
            return;
        }
        _createdPropertyElement = Parent.GetPropertyElement(PropertyElement) is null;
        var prop = Parent.GetOrCreatePropertyElement(PropertyElement);
        Parent.AddToPropertyElement(prop, Element, Index);
    }

    public void Undo(DesignDocument document)
    {
        var prop = Element.OwnerProperty;
        Element.Detach();
        if (_createdPropertyElement && prop is not null && prop.ElementValues.Count == 0)
            Parent.Properties.Remove(prop);
    }
}
