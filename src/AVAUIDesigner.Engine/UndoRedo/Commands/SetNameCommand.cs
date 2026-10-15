using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Engine.UndoRedo.Commands;

/// <summary>Changes x:Name.</summary>
public class SetNameCommand(DesignElement element, string? newName) : IDesignCommand
{
    private string? _oldName;

    public DesignElement Element { get; } = element;
    public string? NewName { get; } = newName;

    public string Description => $"Rename {_oldName ?? Element.ElementType} to {NewName}";

    public void Execute(DesignDocument document)
    {
        _oldName = Element.Name;
        Element.Name = NewName;
    }

    public void Undo(DesignDocument document) => Element.Name = _oldName;
}

/// <summary>Assigns (or clears) the handler method of an event.</summary>
public class SetEventHandlerCommand(DesignElement element, string eventName, string? handlerName) : IDesignCommand
{
    private string? _old;

    public DesignElement Element { get; } = element;
    public string EventName { get; } = eventName;
    public string? HandlerName { get; } = handlerName;

    public string Description => $"Set {Element.DisplayName}.{EventName} handler";

    public void Execute(DesignDocument document)
    {
        _old = Element.GetEventHandler(EventName);
        Element.SetEventHandler(EventName, HandlerName);
    }

    public void Undo(DesignDocument document) => Element.SetEventHandler(EventName, _old);
}
