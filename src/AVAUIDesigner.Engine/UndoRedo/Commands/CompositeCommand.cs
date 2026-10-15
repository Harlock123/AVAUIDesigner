using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Engine.UndoRedo.Commands;

/// <summary>Wraps several commands as one undo step; undo runs them in reverse.</summary>
public class CompositeCommand : IDesignCommand
{
    private readonly List<IDesignCommand> _commands;

    public CompositeCommand(string description, IEnumerable<IDesignCommand> commands)
    {
        Description = description;
        _commands = commands.ToList();
    }

    public string Description { get; }
    public IReadOnlyList<IDesignCommand> Commands => _commands;

    public void Execute(DesignDocument document)
    {
        foreach (var c in _commands) c.Execute(document);
    }

    public void Undo(DesignDocument document)
    {
        for (var i = _commands.Count - 1; i >= 0; i--) _commands[i].Undo(document);
    }
}

/// <summary>
/// Runs arbitrary actions as a command. Used by multi-step operations whose sub-steps depend on
/// the result of earlier ones (the sub-commands are created lazily on first execute).
/// </summary>
public class DelegateCommand(string description, Action<DesignDocument> execute, Action<DesignDocument> undo) : IDesignCommand
{
    public string Description { get; } = description;
    public void Execute(DesignDocument document) => execute(document);
    public void Undo(DesignDocument document) => undo(document);
}
