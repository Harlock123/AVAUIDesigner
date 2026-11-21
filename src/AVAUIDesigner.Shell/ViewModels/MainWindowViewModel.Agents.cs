using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Shell.Services;
using ReactiveUI;

namespace AVAUIDesigner.Shell.ViewModels;

/// <summary>What AI agents (the AI Chat and MCP clients) need from the designer beyond its commands.</summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// Code files an agent made for the design (a model, a repository, a view model, converters), by full path, written
    /// the next time the design is saved, by an agent or with Ctrl+S. Opening or starting another design drops them.
    /// </summary>
    public Dictionary<string, string> PendingFiles { get; } = new(StringComparer.Ordinal);

    /// <summary>The open design, making a blank one if there is none (agents always work on one).</summary>
    internal DesignDocument EnsureDocument()
    {
        if (CurrentDocument is null) NewDocument(NewDocumentKind.UserControl, true, prompt: false);
        return CurrentDocument!;
    }

    /// <summary>Takes up a design an agent started or opened: a fresh history, its code read from beside its file.</summary>
    internal void AdoptDocument(DesignDocument document, bool changed)
    {
        var path = document.FilePath;
        _existingCodeBehind = string.IsNullOrEmpty(path) ? null : SafeRead(path + ".cs");
        _existingViewModel = document.CodeStyle == CodeBehindStyle.MVVM && !string.IsNullOrEmpty(path) ? SafeRead(ViewModelPathFor(document, path)) : null;
        document.AssemblyName ??= ProjectContext?.AssemblyName;
        SetDocument(document);
        document.IsDirty = changed;
        this.RaisePropertyChanged(nameof(Title));
    }

    /// <summary>After an agent's call: the code views show a view model an agent made, and the surface redraws.</summary>
    internal void AgentRefresh()
    {
        if (CurrentDocument is { CodeStyle: CodeBehindStyle.MVVM, FilePath.Length: > 0 } doc && PendingFiles.TryGetValue(ViewModelPathFor(doc, doc.FilePath), out var vm))
            _existingViewModel = vm;
        RegenerateCode();
        this.RaisePropertyChanged(nameof(Title));
        this.RaisePropertyChanged(nameof(IsMvvm));
        DesignInvalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Where the open design's view model goes, for a design saved at <paramref name="axamlPath"/>.</summary>
    internal string ViewModelPathOf(string axamlPath) => ViewModelPathFor(EnsureDocument(), axamlPath);

    /// <summary>Writes the pending files, first asking before replacing any that exist with different contents.</summary>
    private async Task<bool> WritePendingFilesAsync()
    {
        if (PendingFiles.Count == 0) return true;
        var clashes = PendingFiles.Where(p => File.Exists(p.Key) && SafeRead(p.Key) != p.Value).Select(p => Path.GetFileName(p.Key)).ToList();
        if (clashes.Count > 0 && Dialogs is not null &&
            !await Dialogs.ConfirmAsync("Replace files?", $"Saving writes code an AI agent made, replacing {string.Join(", ", clashes)}. Replace them?"))
            return false;
        foreach (var (file, code) in PendingFiles.ToList())
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, code);
            PendingFiles.Remove(file);
        }
        return true;
    }
}
