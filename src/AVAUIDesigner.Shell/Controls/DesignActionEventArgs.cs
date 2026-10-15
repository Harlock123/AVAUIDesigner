using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.UndoRedo;

namespace AVAUIDesigner.Shell.Controls;

public enum DesignActionKind
{
    /// <summary>Execute <see cref="DesignActionEventArgs.Command"/> on the undo stack.</summary>
    ExecuteCommand,
    /// <summary>Double-click: wire the element's default event and show the handler.</summary>
    CreateDefaultEventHandler,
}

public class DesignActionEventArgs : EventArgs
{
    public DesignActionEventArgs(IDesignCommand command)
    {
        Kind = DesignActionKind.ExecuteCommand;
        Command = command;
    }

    public DesignActionEventArgs(DesignActionKind kind, DesignElement element)
    {
        Kind = kind;
        Element = element;
    }

    public DesignActionKind Kind { get; }
    public IDesignCommand? Command { get; }
    public DesignElement? Element { get; }

    /// <summary>Elements to select after the command runs (e.g. a newly dropped control).</summary>
    public IReadOnlyList<DesignElement>? SelectAfter { get; init; }
}
