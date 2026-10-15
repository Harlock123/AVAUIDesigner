using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using AVAUIDesigner.Engine.Settings;
using AVAUIDesigner.Shell.Dialogs;

namespace AVAUIDesigner.Shell.Services;

/// <summary><see cref="IDialogService"/> and <see cref="IClipboardService"/> backed by a window.</summary>
public class DialogService(Window owner) : IDialogService, IClipboardService
{
    public async Task<string?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters.Select(ToPickerType).ToList(),
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileFilter> filters)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = "axaml",
            FileTypeChoices = filters.Select(ToPickerType).ToList(),
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath();
    }

    public Task<string?> PromptAsync(string title, string message, string initialValue, Func<string, string?>? validate = null) =>
        new PromptDialog(title, message, initialValue, validate).ShowDialog<string?>(owner);

    public Task<string?> EditTextAsync(string title, string message, string text, bool isXaml) =>
        new TextEditDialog(title, message, text, isXaml).ShowDialog<string?>(owner);

    public async Task<SaveChangesResult> AskSaveChangesAsync(string fileName)
    {
        var r = await new MessageDialog("Unsaved changes", $"Save changes to {fileName}?", "Save", "Don't Save", "Cancel")
            .ShowDialog<string?>(owner);
        return r switch
        {
            "Save" => SaveChangesResult.Save,
            "Don't Save" => SaveChangesResult.Discard,
            _ => SaveChangesResult.Cancel,
        };
    }

    public async Task<bool> ConfirmAsync(string title, string message) =>
        await new MessageDialog(title, message, "Yes", "No").ShowDialog<string?>(owner) == "Yes";

    public Task ShowMessageAsync(string title, string message) =>
        new MessageDialog(title, message, "OK").ShowDialog<string?>(owner);

    public Task<DesignerSettings?> ShowPreferencesAsync(DesignerSettings settings) =>
        new PreferencesWindow(settings).ShowDialog<DesignerSettings?>(owner);

    public Task<NewDocumentRequest?> ShowNewDocumentAsync(NewDocumentKind kind, bool mvvm, string defaultNamespace, string? defaultDirectory) =>
        new NewDocumentDialog(kind, mvvm, defaultNamespace, defaultDirectory).ShowDialog<NewDocumentRequest?>(owner);

    public async Task SetTextAsync(string text)
    {
        if (owner.Clipboard is { } clipboard) await clipboard.SetTextAsync(text);
    }

    public async Task<string?> GetTextAsync() =>
        owner.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null;

    private static FilePickerFileType ToPickerType(FileFilter f) => new(f.Name) { Patterns = f.Patterns };
}
