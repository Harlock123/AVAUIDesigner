using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Engine.UndoRedo.Commands;

/// <summary>
/// Base for commands that change a set of property values (by full name, including attached
/// properties like "Canvas.Left") on one element. Null values mean "remove".
/// </summary>
public abstract class PropertyChangeCommand : IDesignCommand
{
    private readonly Dictionary<string, string?> _oldValues = new();

    protected PropertyChangeCommand(DesignElement element, IReadOnlyDictionary<string, string?> newValues)
    {
        Element = element;
        NewValues = newValues;
        foreach (var key in newValues.Keys) _oldValues[key] = element.GetValue(key);
    }

    public DesignElement Element { get; }
    public IReadOnlyDictionary<string, string?> NewValues { get; }
    public IReadOnlyDictionary<string, string?> OldValues => _oldValues;

    public abstract string Description { get; }

    public void Execute(DesignDocument document)
    {
        foreach (var (k, v) in NewValues) Element.SetValue(k, v);
    }

    public void Undo(DesignDocument document)
    {
        foreach (var (k, v) in _oldValues) Element.SetValue(k, v);
    }

    /// <summary>True when the command would not change anything.</summary>
    public bool IsNoOp => NewValues.All(kv => _oldValues.TryGetValue(kv.Key, out var old) && old == kv.Value);
}

/// <summary>Changes Left/Top, Grid.Row/Column or Margin.</summary>
public class MoveElementCommand(DesignElement element, IReadOnlyDictionary<string, string?> newValues)
    : PropertyChangeCommand(element, newValues)
{
    public override string Description => $"Move {Element.DisplayName}";
}

/// <summary>Changes Width/Height (and position when resizing from the left/top).</summary>
public class ResizeElementCommand(DesignElement element, IReadOnlyDictionary<string, string?> newValues)
    : PropertyChangeCommand(element, newValues)
{
    public override string Description => $"Resize {Element.DisplayName}";
}

/// <summary>Changes one property value.</summary>
public class SetPropertyCommand : PropertyChangeCommand
{
    public SetPropertyCommand(DesignElement element, string propertyName, string? newValue)
        : base(element, new Dictionary<string, string?> { [propertyName] = newValue })
    {
        PropertyName = propertyName;
    }

    public string PropertyName { get; }

    public override string Description => $"Set {Element.DisplayName}.{PropertyName}";
}
