using AVAUIDesigner.Engine.Document;
using ReactiveUI;

namespace AVAUIDesigner.Engine.UndoRedo;

/// <summary>Unlimited undo/redo of <see cref="IDesignCommand"/>s against the current document.</summary>
public class UndoRedoStack : ReactiveObject
{
    private readonly Stack<IDesignCommand> _undo = new();
    private readonly Stack<IDesignCommand> _redo = new();
    private DesignDocument? _document;

    /// <summary>Raised after any execute/undo/redo with the command involved.</summary>
    public event EventHandler<IDesignCommand>? CommandApplied;

    public DesignDocument? Document
    {
        get => _document;
        set
        {
            this.RaiseAndSetIfChanged(ref _document, value);
            Clear();
        }
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string UndoDescription => _undo.TryPeek(out var c) ? c.Description : string.Empty;
    public string RedoDescription => _redo.TryPeek(out var c) ? c.Description : string.Empty;
    public int UndoCount => _undo.Count;
    public IEnumerable<IDesignCommand> UndoHistory => _undo;

    /// <summary>Executes <paramref name="command"/> and pushes it onto the undo stack.</summary>
    public void Execute(IDesignCommand command)
    {
        var doc = RequireDocument();
        command.Execute(doc);
        _undo.Push(command);
        _redo.Clear();
        doc.IsDirty = true;
        RaiseState();
        CommandApplied?.Invoke(this, command);
    }

    public void Undo()
    {
        if (!_undo.TryPop(out var command)) return;
        var doc = RequireDocument();
        command.Undo(doc);
        _redo.Push(command);
        doc.IsDirty = true;
        RaiseState();
        CommandApplied?.Invoke(this, command);
    }

    public void Redo()
    {
        if (!_redo.TryPop(out var command)) return;
        var doc = RequireDocument();
        command.Execute(doc);
        _undo.Push(command);
        doc.IsDirty = true;
        RaiseState();
        CommandApplied?.Invoke(this, command);
    }

    /// <summary>
    /// Folds every step done since the stack was <paramref name="depth"/> steps deep into one step called
    /// <paramref name="description"/> (an AI agent's batch is one undo step). Nothing is run again.
    /// </summary>
    public void Squash(int depth, string description)
    {
        if (depth < 0 || _undo.Count - depth < 2) return;
        var steps = new List<IDesignCommand>();
        while (_undo.Count > depth) steps.Add(_undo.Pop());
        steps.Reverse();
        _undo.Push(new Commands.CompositeCommand(description, steps));
        RaiseState();
    }

    /// <summary>Drops the steps that could be redone (after steps were taken back for good).</summary>
    public void ForgetRedo()
    {
        if (_redo.Count == 0) return;
        _redo.Clear();
        RaiseState();
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        RaiseState();
    }

    private DesignDocument RequireDocument() =>
        _document ?? throw new InvalidOperationException("No document is attached to the undo stack.");

    private void RaiseState()
    {
        this.RaisePropertyChanged(nameof(CanUndo));
        this.RaisePropertyChanged(nameof(CanRedo));
        this.RaisePropertyChanged(nameof(UndoDescription));
        this.RaisePropertyChanged(nameof(RedoDescription));
    }
}
