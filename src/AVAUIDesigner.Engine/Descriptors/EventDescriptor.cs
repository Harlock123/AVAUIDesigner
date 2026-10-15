namespace AVAUIDesigner.Engine.Descriptors;

/// <summary>
/// Describes one event shown on the Events tab of the property grid.
/// </summary>
public class EventDescriptor
{
    public EventDescriptor(string name, string eventArgsTypeName = "RoutedEventArgs",
        string eventArgsNamespace = "Avalonia.Interactivity", string? description = null)
    {
        Name = name;
        EventArgsTypeName = eventArgsTypeName;
        EventArgsNamespace = eventArgsNamespace;
        Description = description ?? $"Occurs when the {name} event is raised.";
    }

    /// <summary>Event name, e.g. "Click".</summary>
    public string Name { get; }

    /// <summary>Delegate type name, e.g. "EventHandler&lt;RoutedEventArgs&gt;".</summary>
    public string HandlerSignatureTypeName => $"EventHandler<{EventArgsTypeName}>";

    /// <summary>Second parameter type of the generated handler stub, e.g. "RoutedEventArgs".</summary>
    public string EventArgsTypeName { get; }

    /// <summary>Namespace that must be imported for <see cref="EventArgsTypeName"/>.</summary>
    public string EventArgsNamespace { get; }

    public string Description { get; init; }

    /// <summary>Suffix used when suggesting a handler stub name ("{Element}_{DefaultHandlerName}").</summary>
    public string DefaultHandlerName => Name;

    public override string ToString() => Name;
}
