using System.Diagnostics;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Project;
using AVAUIDesigner.Engine.Serialization;
using AVAUIDesigner.Engine.UndoRedo;
using AVAUIDesigner.Engine.UndoRedo.Commands;

namespace AVAUIDesigner.Mcp;

/// <summary>
/// What the server needs from the program that it can't do itself: drawing a design as a picture, which needs
/// Avalonia's renderer. The program supplies it; it runs on the host's thread.
/// </summary>
public interface IDesignLibrary
{
    /// <summary>The design drawn as the designer's surface shows it, as a PNG, at <paramref name="scale"/> (1: one pixel per unit).</summary>
    byte[] Picture(DesignDocument document, double scale, string? baseDirectory);
}

/// <summary>
/// The design an agent works on and what goes with it: its document, its undo history, the parser, serializer and code
/// generators, its file, and the companion code files waiting to be written when it is saved. Used on the host's thread
/// only (<see cref="IDesignHost.RunAsync{T}"/>).
/// </summary>
public interface IDesignSession
{
    /// <summary>The open design. Changes to it are undo steps in <see cref="History"/>, the person's own history.</summary>
    DesignDocument Document { get; }

    UndoRedoStack History { get; }

    ControlDescriptorRegistry Registry { get; }

    XamlParser Parser { get; }

    XamlSerializer Serializer { get; }

    /// <summary>The .axaml file the design was opened from or will be saved to (empty: none yet).</summary>
    string FilePath => Document.FilePath;

    /// <summary>The code generation settings for the design: its namespace, class, base class and style.</summary>
    CodeGenOptions Options { get; }

    /// <summary>The code-behind the design would save now.</summary>
    string CodeBehind { get; }

    /// <summary>The view model the design would save now (MVVM designs), or empty.</summary>
    string ViewModel { get; }

    /// <summary>Where the design's view model goes (beside a Views folder, in ViewModels).</summary>
    string ViewModelPath(string axamlPath);

    /// <summary>Code files a tool made (a model, a repository, a view model, converters), by full path, written on save.</summary>
    Dictionary<string, string> Pending { get; }

    /// <summary>Adopts another design (new, open): a fresh history, and saved unless <paramref name="changed"/>.</summary>
    void Replace(DesignDocument document, bool changed);

    /// <summary>Writes the design (its .axaml, code-behind and view model) and the pending companions to <paramref name="path"/>.</summary>
    Task SaveAsync(string path);

    /// <summary>The design or its companions changed: the program refreshes its panels and code views.</summary>
    void Refresh();

    /// <summary>Selects elements in the designer (the live window shows them selected); nothing headless.</summary>
    void Select(IReadOnlyList<DesignElement> elements);
}

/// <summary>
/// Where the MCP tools' work runs. The handlers (<see cref="DesignTools"/>) only touch the design through
/// <see cref="RunAsync{T}"/>, so the same handlers serve the headless server and the open window, both on the thread
/// Avalonia runs on.
/// </summary>
public interface IDesignHost
{
    /// <summary>The folder relative paths start from; open, save, reading files and generated code stay inside it.</summary>
    string Root { get; }

    IDesignLibrary Library { get; }

    /// <summary>The database connections the person has set up (by name; agents never see their passwords).</summary>
    ConnectionStore Connections { get; }

    /// <summary>
    /// Runs <paramref name="work"/> on the thread that owns the design, one call at a time, and returns what it returns
    /// (or throws what it throws). <paramref name="action"/> names it for the status bar.
    /// </summary>
    Task<T> RunAsync<T>(string action, Func<IDesignSession, Task<T>> work, CancellationToken cancel = default);
}

public static class DesignHostExtensions
{
    public static Task<T> RunAsync<T>(this IDesignHost host, string action, Func<IDesignSession, T> work, CancellationToken cancel = default) =>
        host.RunAsync(action, s => Task.FromResult(work(s)), cancel);
}

/// <summary>Runs work on a UI thread (the program's Avalonia dispatcher, or a test's).</summary>
public interface IUiThread
{
    Task<T> InvokeAsync<T>(Func<Task<T>> work);
}

/// <summary>The program as the host sees it: the design, whether it can take a call now, and the status bar.</summary>
public interface IDesignDesk
{
    IDesignSession Session { get; }

    /// <summary>Why the program can't take a call just now ("a dialog box is open"), or null. The call waits.</summary>
    string? Busy { get; }

    /// <summary>Tells the person what an agent just did.</summary>
    void Report(string text);
}

/// <summary>
/// The host: calls run against the desk's design on its UI thread, one at a time, so the surface redraws and Ctrl+Z
/// undoes them like the person's own edits. While the person is busy a call waits up to <see cref="BusyWait"/>.
/// </summary>
public sealed class DesignHost(IUiThread ui, IDesignDesk desk, IDesignLibrary library, string root, ConnectionStore? connections = null) : IDesignHost
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string Root { get; } = Path.GetFullPath(root);

    public IDesignLibrary Library { get; } = library;

    public ConnectionStore Connections { get; } = connections ?? new ConnectionStore(null);

    /// <summary>Working in the open window (so a big deletion asks the person first).</summary>
    public bool Live { get; init; }

    public TimeSpan BusyWait { get; init; } = TimeSpan.FromSeconds(20);

    public TimeSpan BusyPoll { get; init; } = TimeSpan.FromMilliseconds(200);

    public async Task<T> RunAsync<T>(string action, Func<IDesignSession, Task<T>> work, CancellationToken cancel = default)
    {
        await _gate.WaitAsync(cancel).ConfigureAwait(false);
        try
        {
            var clock = Stopwatch.StartNew();
            while (true)
            {
                cancel.ThrowIfCancellationRequested();
                var (ran, result, busy) = await ui.InvokeAsync(async () =>
                {
                    if (desk.Busy is { } why) return (false, default(T), why);
                    try
                    {
                        var r = await work(desk.Session);
                        desk.Report($"AI agent: {action}.");
                        return (true, r, (string?)null);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        desk.Report($"AI agent: {action} failed: {e.Message}");
                        throw;
                    }
                }).ConfigureAwait(false);
                if (ran) return result!;
                if (clock.Elapsed >= BusyWait)
                    throw new DesignBusyException($"AVAUIDesigner is busy: {busy}. The call waited {BusyWait.TotalSeconds:0} seconds; ask the person to finish, then try again.");
                await Task.Delay(BusyPoll, cancel).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}

public sealed class DesignBusyException(string message) : Exception(message);

/// <summary>A design with no window round it, as <c>AVAUIDesigner --mcp</c> serves it.</summary>
public sealed class HeadlessSession : IDesignSession
{
    private readonly CodeBehindGenerator _codeBehind = new();
    private readonly ViewModelGenerator _viewModels = new();

    public HeadlessSession(ControlDescriptorRegistry registry)
    {
        Registry = registry;
        Document = Parser.Parse(Templates.Blank("UserControl", "MyApp.Views.MainView", mvvm: false), registry);
        History = new UndoRedoStack { Document = Document };
    }

    public DesignDocument Document { get; private set; }

    public UndoRedoStack History { get; }

    public ControlDescriptorRegistry Registry { get; }

    public XamlParser Parser { get; } = new();

    public XamlSerializer Serializer { get; } = new();

    public Dictionary<string, string> Pending { get; } = new(StringComparer.Ordinal);

    public CodeGenOptions Options => DesignFiles.Options(Document);

    public string CodeBehind => _codeBehind.Generate(Document, DesignFiles.Read(Document.FilePath + ".cs"), Options);

    public string ViewModel => Document.CodeStyle == CodeBehindStyle.MVVM
        ? _viewModels.Generate(Document, Options, DesignFiles.ExistingViewModel(this))
        : "";

    public string ViewModelPath(string axamlPath) => ProjectContext.Standalone().GetViewModelPath(axamlPath, Document.ClassName + "ViewModel");

    public void Replace(DesignDocument document, bool changed)
    {
        Document = document;
        History.Document = document;
        document.IsDirty = changed;
    }

    public Task SaveAsync(string path) => DesignFiles.SaveAsync(this, path, _codeBehind, _viewModels);

    public void Refresh() { }

    public void Select(IReadOnlyList<DesignElement> elements) { }
}

public sealed class HeadlessDesk(ControlDescriptorRegistry registry, TextWriter? log = null) : IDesignDesk
{
    public IDesignSession Session { get; } = new HeadlessSession(registry);

    public string? Busy => null;

    public void Report(string text) => log?.WriteLine(text);
}

/// <summary>Writing a design and its code, the way the designer's own Save does.</summary>
public static class DesignFiles
{
    public static string? Read(string path)
    {
        try
        {
            return path.Length > 3 && File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>The view model's source as it stands: a pending one a tool made, else the file on disk.</summary>
    public static string? ExistingViewModel(IDesignSession s)
    {
        if (string.IsNullOrEmpty(s.Document.FilePath)) return s.Pending.Values.FirstOrDefault(v => v.Contains($"class {s.Document.ClassName}ViewModel", StringComparison.Ordinal));
        var path = s.ViewModelPath(s.Document.FilePath);
        return s.Pending.TryGetValue(path, out var pending) ? pending : Read(path);
    }

    public static CodeGenOptions Options(DesignDocument doc)
    {
        string? vmNs = null;
        if (doc.GetNamespaceUri("vm") is { } uri && uri.StartsWith("clr-namespace:", StringComparison.Ordinal))
            vmNs = uri["clr-namespace:".Length..].Split(';')[0];
        return new CodeGenOptions
        {
            Namespace = doc.ClassNamespace ?? "MyApp.Views",
            ClassName = doc.ClassName,
            BaseClass = CodeIdentifiers.BareTypeName(doc.RootElementType),
            Style = doc.CodeStyle,
            ViewModelNamespace = vmNs,
            // Avalonia's name generator (on in every new app) declares the named controls; declaring them too would clash.
            GenerateFieldDeclarations = false,
        };
    }

    /// <summary>Writes the pending companions (each once), then the .axaml, its code-behind and its view model.</summary>
    public static async Task SaveAsync(IDesignSession s, string path, CodeBehindGenerator codeBehind, ViewModelGenerator viewModels)
    {
        var doc = s.Document;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await WritePendingAsync(s).ConfigureAwait(false);
        await File.WriteAllTextAsync(path, s.Serializer.Serialize(doc)).ConfigureAwait(false);
        var options = Options(doc);
        await File.WriteAllTextAsync(path + ".cs", codeBehind.Generate(doc, Read(path + ".cs"), options)).ConfigureAwait(false);
        if (doc.CodeStyle == CodeBehindStyle.MVVM)
        {
            var vmPath = s.ViewModelPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(vmPath)!);
            await File.WriteAllTextAsync(vmPath, viewModels.Generate(doc, options, Read(vmPath))).ConfigureAwait(false);
        }
        doc.FilePath = Path.GetFullPath(path);
        doc.IsDirty = false;
    }

    /// <summary>Writes the companion files tools made, and forgets them.</summary>
    public static async Task WritePendingAsync(IDesignSession s)
    {
        foreach (var (file, code) in s.Pending.ToList())
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, code).ConfigureAwait(false);
            s.Pending.Remove(file);
        }
    }
}

/// <summary>Starting designs.</summary>
public static class Templates
{
    /// <summary>An empty Window or UserControl with a class, optionally with a view model (MVVM), holding a Grid.</summary>
    public static string Blank(string kind, string xClass, bool mvvm, double? width = null, double? height = null, string? title = null)
    {
        var cls = xClass[(xClass.LastIndexOf('.') + 1)..];
        var ns = xClass.Contains('.') ? xClass[..xClass.LastIndexOf('.')] : "MyApp.Views";
        var vmNs = CodeGenOptions.DefaultViewModelNamespace(ns);
        var window = kind == "Window";
        var size = window
            ? $" Title=\"{System.Security.SecurityElement.Escape(title ?? cls)}\" Width=\"{width ?? 1000}\" Height=\"{height ?? 680}\""
            : $" d:DesignWidth=\"{width ?? 1000}\" d:DesignHeight=\"{height ?? 680}\"";
        var vm = mvvm ? $" xmlns:vm=\"clr-namespace:{vmNs}\" x:DataType=\"vm:{cls}ViewModel\"" : "";
        var dc = mvvm ? $"\n  <{kind}.DataContext>\n    <vm:{cls}ViewModel />\n  </{kind}.DataContext>" : "";
        return $"""
            <{kind} xmlns="{XamlNamespaceMap.Avalonia}" xmlns:x="{XamlNamespaceMap.Xaml}" xmlns:d="{XamlNamespaceMap.Blend}" xmlns:mc="{XamlNamespaceMap.MarkupCompatibility}" mc:Ignorable="d"{vm} x:Class="{xClass}"{size}>{dc}
              <Grid x:Name="layoutRoot" />
            </{kind}>
            """;
    }
}

/// <summary>
/// One AI agent's change as one undo step: the whole design before and after (with its namespaces, class and code
/// style), so a change of any size undoes in one go.
/// </summary>
public sealed class AgentEditCommand(DesignDocument after, string description) : IDesignCommand
{
    private readonly ReplaceRootCommand _replace = new(after, description);
    private readonly CodeBehindStyle _styleAfter = after.CodeStyle;
    private CodeBehindStyle _styleBefore;

    public string Description { get; } = description;

    public void Execute(DesignDocument document)
    {
        _styleBefore = document.CodeStyle;
        _replace.Execute(document);
        document.CodeStyle = _styleAfter;
    }

    public void Undo(DesignDocument document)
    {
        _replace.Undo(document);
        document.CodeStyle = _styleBefore;
    }
}
