using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Engine.UndoRedo;

/// <summary>One undoable design action.</summary>
public interface IDesignCommand
{
    /// <summary>Shown in Edit &gt; Undo "{Description}".</summary>
    string Description { get; }

    void Execute(DesignDocument document);
    void Undo(DesignDocument document);
}
