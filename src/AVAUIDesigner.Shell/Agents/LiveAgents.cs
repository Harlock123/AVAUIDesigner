using Avalonia.Threading;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Serialization;
using AVAUIDesigner.Engine.UndoRedo;
using AVAUIDesigner.Mcp;
using AVAUIDesigner.Shell.ViewModels;

namespace AVAUIDesigner.Shell.Agents;

/// <summary>The design open in the window, as an agent works on it: the person's own document, history and Save.</summary>
internal sealed class WindowSession(MainWindowViewModel vm) : IDesignSession
{
    public DesignDocument Document => vm.EnsureDocument();

    public UndoRedoStack History => vm.UndoRedo;

    public ControlDescriptorRegistry Registry => vm.Registry;

    public XamlParser Parser => vm.Parser;

    public XamlSerializer Serializer => vm.Serializer;

    public CodeGenOptions Options => vm.BuildCodeGenOptions(Document);

    public string CodeBehind => vm.CodeBehindText;

    public string ViewModel => vm.ViewModelText;

    public string ViewModelPath(string axamlPath) => vm.ViewModelPathOf(axamlPath);

    public Dictionary<string, string> Pending => vm.PendingFiles;

    public void Replace(DesignDocument document, bool changed) => vm.AdoptDocument(document, changed);

    public async Task SaveAsync(string path)
    {
        if (!await vm.SaveToAsync(path)) throw new IOException("The design wasn't saved (the person said no, or the file couldn't be written).");
    }

    public void Refresh() => vm.AgentRefresh();

    public void Select(IReadOnlyList<DesignElement> elements) => vm.SelectionManager.SetSelection(elements);
}

/// <summary>
/// AI agents in the open window: the session and host the chat and outside agents share, the door on this computer
/// (a pipe, for <c>AVAUIDesigner --mcp --attach</c>) and the network door (MCP over HTTP with a token).
/// </summary>
internal sealed class LiveAgents(MainWindow window, MainWindowViewModel vm) : IDesignDesk, IUiThread, IAsyncDisposable
{
    private readonly WindowSession _session = new(vm);
    private LiveListener? _listener;
    private NetworkListener? _network;

    public DesignLibrary Library { get; } = new(window.AgentStage, vm.Registry);

    /// <summary>The person's database connections (connections.json beside the settings; tests give their own).</summary>
    public ConnectionStore Connections { get; set; } = new(Path.Combine(AgentSettings.Folder, ConnectionStore.FileName));

    public bool Running => _listener != null;

    public TimeSpan BusyWait { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>How many outside agents are connected (pipe and network together), when it changes.</summary>
    public event Action<int>? ConnectionsChanged;

    public IDesignSession Session => _session;

    public string? Busy => window.OwnedWindows.Count > 0 || ChatLook.ModalsOpen > 0 ? "a dialog box is open in AVAUIDesigner" : null;

    public void Report(string text) => vm.StatusMessage = text;

    public Task<T> InvokeAsync<T>(Func<Task<T>> work) => Dispatcher.UIThread.InvokeAsync(work);

    /// <summary>The host outside agents work through, in <paramref name="root"/>.</summary>
    public DesignHost Host(string root) => new(this, this, Library, root, Connections) { BusyWait = BusyWait, Live = true };

    /// <summary>The host the AI Chat runs its in-process MCP server on; its status lines name the chat's model.</summary>
    public DesignHost ChatHost(string root, Func<string> who) => new(this, new ChatDesk(this, who), Library, root, Connections) { BusyWait = BusyWait, Live = true };

    private sealed class ChatDesk(LiveAgents agents, Func<string> who) : IDesignDesk
    {
        public IDesignSession Session => agents.Session;

        public string? Busy => agents.Busy;

        public void Report(string text) =>
            agents.Report(text.StartsWith("AI agent: ", StringComparison.Ordinal) ? who() + ": " + text["AI agent: ".Length..] : text);
    }

    /// <summary>Opens the door on a pipe; returns why it couldn't (another AVAUIDesigner has it), or null.</summary>
    public string? Start(string pipeName, Func<string> root)
    {
        if (_listener != null) return null;
        var listener = new LiveListener(pipeName, asked => Host(Directory.Exists(asked) ? asked : root()));
        listener.ConnectionsChanged += n => Dispatcher.UIThread.Post(() => ConnectionsChanged?.Invoke(n));
        try
        {
            listener.Start();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ = listener.DisposeAsync();
            return e.Message;
        }
        _listener = listener;
        return null;
    }

    public async Task StopAsync()
    {
        var listener = _listener;
        _listener = null;
        if (listener == null) return;
        listener.Stop();
        await listener.DisposeAsync();
    }

    public bool NetworkRunning => _network != null;

    public NetworkSettings? NetworkSettings { get; private set; }

    public string? NetworkRoot { get; private set; }

    public IReadOnlyList<string> NetworkUrls => _network?.Urls ?? [];

    public async Task<string?> StartNetworkAsync(NetworkSettings settings, string root)
    {
        if (_network != null) return null;
        if (!Directory.Exists(root)) return $"the working folder {root} doesn't exist.";
        var listener = new NetworkListener(settings, DesignServer.Name, o => DesignServer.Configure(o, Host(root)));
        listener.ConnectionsChanged += n => Dispatcher.UIThread.Post(() => ConnectionsChanged?.Invoke(n));
        try
        {
            await listener.StartAsync();
        }
        catch (IOException e)
        {
            return e.Message;
        }
        _network = listener;
        NetworkSettings = settings;
        NetworkRoot = root;
        return null;
    }

    public async Task StopNetworkAsync()
    {
        var listener = _network;
        _network = null;
        NetworkSettings = null;
        NetworkRoot = null;
        if (listener != null) await listener.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await StopNetworkAsync();
    }
}
