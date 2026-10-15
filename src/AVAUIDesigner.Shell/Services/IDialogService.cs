using AVAUIDesigner.Engine.Settings;

namespace AVAUIDesigner.Shell.Services;

public record FileFilter(string Name, params string[] Patterns)
{
    public static readonly FileFilter Axaml = new("Avalonia XAML", "*.axaml");
    public static readonly FileFilter CsProj = new("C# project", "*.csproj");
    public static readonly FileFilter All = new("All files", "*");
}

public enum SaveChangesResult
{
    Save,
    Discard,
    Cancel,
}

/// <summary>Kind of document created by the New wizard.</summary>
public enum NewDocumentKind
{
    Window,
    UserControl,
}

public record NewDocumentRequest(string ClassName, string Namespace, NewDocumentKind Kind, bool Mvvm, string? Directory);

/// <summary>UI interactions view models need; implemented by the main window.</summary>
public interface IDialogService
{
    Task<string?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters);
    Task<string?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileFilter> filters);

    /// <summary>Single-line prompt. <paramref name="validate"/> returns an error message or null.</summary>
    Task<string?> PromptAsync(string title, string message, string initialValue, Func<string, string?>? validate = null);

    /// <summary>Multi-line text editor (XAML highlighting when <paramref name="isXaml"/>). Null when cancelled.</summary>
    Task<string?> EditTextAsync(string title, string message, string text, bool isXaml);

    Task<SaveChangesResult> AskSaveChangesAsync(string fileName);
    Task<bool> ConfirmAsync(string title, string message);
    Task ShowMessageAsync(string title, string message);

    /// <summary>Shows the preferences dialog on a copy of the settings; returns the edited copy, or null if cancelled.</summary>
    Task<DesignerSettings?> ShowPreferencesAsync(DesignerSettings settings);

    Task<NewDocumentRequest?> ShowNewDocumentAsync(NewDocumentKind kind, bool mvvm, string defaultNamespace, string? defaultDirectory);
}

public interface IClipboardService
{
    Task SetTextAsync(string text);
    Task<string?> GetTextAsync();
}

/// <summary>In-process clipboard used when no system clipboard is available (tests, headless).</summary>
public class InMemoryClipboard : IClipboardService
{
    private string? _text;

    public Task SetTextAsync(string text)
    {
        _text = text;
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync() => Task.FromResult(_text);
}
