using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AVAUIDesigner.Chat;
using AVAUIDesigner.Data;
using AVAUIDesigner.Mcp;
using AVAUIDesigner.Shell.Agents;
using AVAUIDesigner.Shell.ViewModels;

namespace AVAUIDesigner.Shell;

/// <summary>
/// The window's AI side: the AI menu, the AI Chat docked on the right (F12), the door for agents outside the program
/// (AI › Allow AI Agents: <c>AVAUIDesigner --mcp --attach</c>), the network door (AI › Connect an Agent) and the
/// database connections agents read structures from (AI › Data Connections).
/// </summary>
public partial class MainWindow
{
    private LazyToolBridge? _chatTools;
    private string? _networkToken;
    private ToolsHolder? _toolsHolder;

    internal AgentSettings AgentSettings { get; private set; } = new();

    internal LiveAgents Agents { get; private set; } = null!;

    internal ChatPanel? Chat { get; private set; }

    /// <summary>The pipe outside agents connect on (tests give one of their own).</summary>
    public string AgentPipe { get; set; } = LiveProtocol.PipeName;

    /// <summary>Tests' own API key stores (never the person's real keys).</summary>
    internal (ApiKeyStore Keys, ApiKeyStore ServerKeys)? ChatKeys { get; set; }

    /// <summary>
    /// The folder the chat and attached agents work in: the one chosen, else the open project's, else the app folder the
    /// design is in (the parent of a Views folder), else Documents.
    /// </summary>
    internal string AgentRoot
    {
        get
        {
            if (AgentSettings.WorkingFolder is { Length: > 0 } chosen && Directory.Exists(chosen)) return chosen;
            if (_vm?.ProjectContext is { IsStandalone: false, ProjectDirectory: { } project }) return project;
            if (_vm?.CurrentDocument is { FilePath.Length: > 0 } doc && Path.GetDirectoryName(doc.FilePath) is { Length: > 0 } folder && Directory.Exists(folder))
                return Path.GetFileName(folder).Equals("Views", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(folder)! : folder;
            return DocumentsFolder;
        }
    }

    private static string DocumentsFolder =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) is { Length: > 0 } d && Directory.Exists(d) ? d
        : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private void SetUpAgents(MainWindowViewModel vm)
    {
        if (Agents != null) return;
        AgentSettings = AgentSettings.Load();
        Agents = new LiveAgents(this, vm);
        Agents.ConnectionsChanged += n => vm.StatusMessage = n == 0 ? "No AI agents are connected now." : $"{n} AI agent{(n == 1 ? " is" : "s are")} connected.";
        AllowAgentsMenuItem.IsChecked = AgentSettings.AllowAgents;
        Opened += async (_, _) =>
        {
            if (AgentSettings.AllowAgents) vm.StatusMessage = SetAllowAgents(true);
            if (AgentSettings.AllowNetworkAgents && await SyncNetworkAsync() is { Length: > 0 } line) vm.StatusMessage = line;
            if (AgentSettings.ChatOpen && !ChatHolder.IsVisible) ToggleChat();
        };
        Closed += async (_, _) =>
        {
            AgentSettings.Save();
            if (_chatTools is not null) await _chatTools.DisposeAsync();
            await Agents.DisposeAsync();
        };
    }

    private void Say(string text)
    {
        if (_vm != null) _vm.StatusMessage = text;
    }

    // ---- the AI menu ----

    private void OnToggleChatClick(object? sender, RoutedEventArgs e) => ToggleChat();

    private void OnAllowAgentsClick(object? sender, RoutedEventArgs e) => Say(SetAllowAgents(!AgentSettings.AllowAgents));

    private void OnConnectAgentClick(object? sender, RoutedEventArgs e) => _ = ShowConnect();

    private void OnDataConnectionsClick(object? sender, RoutedEventArgs e) => _ = ShowDataConnections();

    private async void OnWorkingFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "The folder AI agents read and write in", AllowMultiple = false });
        if (folders.Count == 0 || folders[0].TryGetLocalPath() is not { } path) return;
        AgentSettings.WorkingFolder = path;
        AgentSettings.Save();
        await RestartChatToolsAsync();
        Say($"AI agents now read and write in {path}.");
    }

    /// <summary>The chat's tools start again in the new working folder (its conversation stays).</summary>
    private async Task RestartChatToolsAsync()
    {
        if (_toolsHolder is null || Chat is null) return;
        var old = _toolsHolder.Bridge;
        _toolsHolder.Bridge = NewChatTools(Chat);
        _chatTools = _toolsHolder.Bridge;
        await old.DisposeAsync();
    }

    /// <summary>Lets agents outside the program in, or shuts them out, and remembers it.</summary>
    public string SetAllowAgents(bool allow)
    {
        if (AgentSettings.AllowAgents != allow)
        {
            AgentSettings.AllowAgents = allow;
            AgentSettings.Save();
        }
        AllowAgentsMenuItem.IsChecked = allow;
        if (!allow)
        {
            _ = Agents.StopAsync();
            return "AI agents are shut out.";
        }
        return Agents.Start(AgentPipe, () => AgentRoot) is { } problem
            ? $"AI agents can't connect: {problem}"
            : "AI agents may connect (AVAUIDesigner --mcp --attach). They work on the design in this window.";
    }

    public string NetworkToken => _networkToken ??= TryToken(t => t.Get());

    private static string TryToken(Func<NetworkToken, string> get)
    {
        try
        {
            return get(global::AVAUIDesigner.Mcp.NetworkToken.In(AgentSettings.Folder));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return NetworkListener.NewToken();
        }
    }

    public async Task<string> SyncNetworkAsync()
    {
        var s = AgentSettings;
        var want = s.AllowNetworkAgents
            ? new NetworkSettings(s.NetworkAgentsAddress, s.NetworkAgentsPort, NetworkToken, Blank(s.NetworkAgentsCertificate), Blank(s.NetworkAgentsKey))
            : null;
        var root = Blank(s.NetworkAgentsFolder) ?? DocumentsFolder;
        if (Agents.NetworkRunning && (want == null || want != Agents.NetworkSettings || root != Agents.NetworkRoot))
        {
            await Agents.StopNetworkAsync();
            if (want == null) return "AI agents on other computers are shut out.";
        }
        if (want == null || Agents.NetworkRunning) return "";
        if (want.Problem() is { } bad) return $"AI agents on other computers can't connect: {bad}";
        return await Agents.StartNetworkAsync(want, root) is { } problem
            ? $"AI agents on other computers can't connect: {problem}"
            : $"AI agents on other computers may connect (MCP): {Agents.NetworkUrls.FirstOrDefault()}, with the access token (AI › Connect an Agent).";
    }

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static TextBlock Words(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>How to connect an agent: on this computer through the pipe, or from another over the network.</summary>
    public async Task ShowConnect()
    {
        var exe = Environment.ProcessPath ?? "AVAUIDesigner";
        var local = new SelectableTextBlock
        {
            FontSize = 12, TextWrapping = TextWrapping.Wrap,
            Text = $"On this computer: turn on AI › Allow AI Agents (it's {(AgentSettings.AllowAgents ? "on" : "off")} now), then add the server to the agent. For Claude Code:\n\n" +
                   $"    claude mcp add avauidesigner -- \"{exe}\" --mcp --attach\n\n" +
                   $"The agent then designs in this window, reading and writing files in {AgentRoot} (AI › Working Folder changes it), and Ctrl+Z undoes its steps. Without --attach the server has a design of its own and no window.",
        };
        var allow = new CheckBox { Content = "Let AI agents on other computers connect (MCP over HTTP)", IsChecked = AgentSettings.AllowNetworkAgents };
        var port = new TextBox { Text = AgentSettings.NetworkAgentsPort.ToString(System.Globalization.CultureInfo.InvariantCulture), Width = 80 };
        var folder = new TextBox { Text = AgentSettings.NetworkAgentsFolder, Watermark = DocumentsFolder, MinWidth = 300 };
        var token = new TextBox { Text = new string('•', 20), IsReadOnly = true, Width = 300 };
        var show = new Button { Content = "Show" };
        show.Click += (_, _) => token.Text = NetworkToken;
        var copy = new Button { Content = "Copy" };
        copy.Click += async (_, _) => { if (Clipboard is { } c) await c.SetTextAsync(NetworkToken); };
        var renew = new Button { Content = "New token" };
        renew.Click += (_, _) => { _networkToken = TryToken(t => t.Renew()); token.Text = NetworkToken; };
        var where = new SelectableTextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };
        void ShowWhere() => where.Text = Agents.NetworkRunning
            ? "Listening at " + string.Join(", ", Agents.NetworkUrls) + ". Agents send \"Authorization: Bearer <token>\"."
            : "Not listening.";
        ShowWhere();
        var close = new Button { Content = "Close", MinWidth = 90 };
        var apply = new Button { Content = "Apply", MinWidth = 90 };
        var dialog = new Window { Title = "Connect an Agent", Width = 640, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        apply.Click += async (_, _) =>
        {
            if (!int.TryParse(port.Text, out var p) || p is < 1 or > 65535) { where.Text = "The port is a number from 1 to 65535."; return; }
            AgentSettings.AllowNetworkAgents = allow.IsChecked == true;
            AgentSettings.NetworkAgentsPort = p;
            AgentSettings.NetworkAgentsFolder = folder.Text?.Trim() ?? "";
            AgentSettings.Save();
            var line = await SyncNetworkAsync();
            ShowWhere();
            if (line.Length > 0) { Say(line); if (line.Contains("can't")) where.Text = line; }
        };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 12,
            Children =
            {
                local,
                new Separator(),
                allow,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Words("Port"), port, Words("Working folder"), folder } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Words("Access token"), token, show, copy, renew } },
                where,
                new TextBlock
                {
                    Opacity = 0.7, FontSize = 11, TextWrapping = TextWrapping.Wrap,
                    Text = "Plain HTTP sends the token and the work unencrypted: use it on a network you trust, or run AVAUIDesigner --mcp --http with --cert and --key for HTTPS. Anyone with the token can change the open design.",
                },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { apply, close } },
            },
        };
        await ChatLook.ShowModal(dialog, this);
    }

    /// <summary>
    /// The database connections agents may read structures from: each named, with its engine and connection string,
    /// tested before it is kept. Agents see the names and servers, never the passwords.
    /// </summary>
    public async Task ShowDataConnections()
    {
        var store = Agents.Connections;
        var list = new ListBox { Height = 140 };
        var name = new TextBox { Watermark = "clinic", Width = 200 };
        var engine = new ComboBox { Width = 260, ItemsSource = SqlDialect.All.Values.Select(d => d.DisplayName).ToList(), SelectedIndex = 0 };
        var conn = new TextBox { Watermark = "Server=…;Database=…;User ID=…;Password=…", PasswordChar = '•', AcceptsReturn = false, MinWidth = 480 };
        var reveal = new CheckBox { Content = "Show" };
        reveal.IsCheckedChanged += (_, _) => conn.PasswordChar = reveal.IsChecked == true ? '\0' : '•';
        var note = new TextBox { Watermark = "What it is (optional)", MinWidth = 480 };
        var result = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        DbProvider Engine() => SqlDialect.All.Keys.ElementAt(Math.Max(0, engine.SelectedIndex));
        void Refill()
        {
            list.ItemsSource = store.All.Select(c => $"{c.Name}  ·  {SqlDialect.For(c.Engine).DisplayName}  ·  {c.Redacted}").ToList();
        }
        Refill();
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= store.All.Count) return;
            var c = store.All[list.SelectedIndex];
            name.Text = c.Name;
            engine.SelectedIndex = SqlDialect.All.Keys.ToList().IndexOf(c.Engine);
            conn.Text = c.ConnectionString;
            note.Text = c.Note;
        };
        var test = new Button { Content = "Test" };
        test.Click += async (_, _) =>
        {
            result.Text = "Connecting…";
            var e = Engine();
            var cs = conn.Text ?? "";
            try { result.Text = "Connected to " + await Task.Run(() => SchemaReader.Test(e, cs)) + "."; }
            catch (Exception ex) { result.Text = "Couldn't connect: " + ex.Message; }
        };
        var keep = new Button { Content = "Keep", Classes = { "accent" } };
        keep.Click += async (_, _) =>
        {
            var c = new DataConnection { Name = name.Text?.Trim() ?? "", Provider = Engine().ToString(), ConnectionString = conn.Text?.Trim() ?? "", Note = string.IsNullOrWhiteSpace(note.Text) ? null : note.Text.Trim() };
            try
            {
                var version = await Task.Run(() => SchemaReader.Test(c.Engine, c.ConnectionString));
                store.Put(c, save: true);
                Refill();
                result.Text = $"Kept {c.Name} ({version}). AI agents can now read its tables' structure.";
            }
            catch (Exception ex)
            {
                result.Text = "Not kept: " + ex.Message;
            }
        };
        var remove = new Button { Content = "Remove" };
        remove.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) return;
            result.Text = store.Remove(name.Text.Trim()) ? $"Removed {name.Text.Trim()}." : $"There's no {name.Text.Trim()}.";
            Refill();
        };
        var close = new Button { Content = "Close", MinWidth = 90 };
        var dialog = new Window { Title = "Data Connections", Width = 720, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap, FontSize = 12,
                    Text = "Databases AI agents may read the structure of (tables, columns, keys), to build screens for them. They never read rows, so no patient or personal data leaves the database this way. Agents see each connection's name and server, never its password. Db2 and other databases connect through an installed ODBC driver.",
                },
                list,
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), RowSpacing = 6, ColumnSpacing = 10,
                    Children =
                    {
                        Cell(Words("Name"), 0, 0), Cell(name, 0, 1),
                        Cell(Words("Engine"), 1, 0), Cell(engine, 1, 1),
                        Cell(Words("Connection string"), 2, 0), Cell(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { conn, reveal } }, 2, 1),
                        Cell(Words("Note"), 3, 0), Cell(note, 3, 1),
                    },
                },
                result,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { test, remove, keep, close } },
            },
        };
        await ChatLook.ShowModal(dialog, this);

        static Control Cell(Control c, int row, int column)
        {
            Grid.SetRow(c, row);
            Grid.SetColumn(c, column);
            return c;
        }
    }

    // ---- the AI Chat ----

    /// <summary>Shows or hides the AI Chat (F12), making it the first time.</summary>
    public void ToggleChat()
    {
        if (Chat is null) MakeChat();
        ChatHolder.IsVisible = !ChatHolder.IsVisible;
        ChatSplitter.IsVisible = ChatHolder.IsVisible;
        ChatMenuItem.IsChecked = ChatHolder.IsVisible;
        if (AgentSettings.ChatOpen != ChatHolder.IsVisible)
        {
            AgentSettings.ChatOpen = ChatHolder.IsVisible;
            AgentSettings.Save();
        }
        if (ChatHolder.IsVisible) Chat!.Input.Focus();
    }

    private LazyToolBridge NewChatTools(ChatPanel panel) =>
        new(async cancel => await McpToolBridge.StartAsync(Agents.ChatHost(AgentRoot, () => panel.Session.Provider == ChatProvider.Anthropic ? "Claude" : "AI Chat"), cancel));

    private void MakeChat()
    {
        var folder = AgentSettings.Folder;
        var keys = ChatKeys?.Keys ?? ApiKeyStore.ForApp(folder);
        var serverKeys = ChatKeys?.ServerKeys ?? ApiKeyStore.ForServer(folder);
        var holder = new ToolsHolder();
        ChatPanel? panel = null;
        holder.Bridge = new LazyToolBridge(async cancel => await McpToolBridge.StartAsync(Agents.ChatHost(AgentRoot, () => panel?.Session.Provider == ChatProvider.Anthropic ? "Claude" : "AI Chat"), cancel));
        _chatTools = holder.Bridge;
        panel = new ChatPanel(holder, keys, Say, AgentSettings, serverKeys) { SaveSettings = AgentSettings.Save };
        panel.CloseRequested += ToggleChat;
        if (ChatKeys is null)
        {
            panel.History = new ChatHistory(Path.Combine(folder, "Chats"));
            panel.Ledger = new UsageLedger(Path.Combine(folder, "chat-usage.json"));
            panel.RestoreLast();
        }
        Chat = panel;
        _toolsHolder = holder;
        ChatHolder.Child = panel;
    }

    /// <summary>The chat's tools, which can be swapped for a new bridge when the working folder changes.</summary>
    private sealed class ToolsHolder : IToolBridge
    {
        private LazyToolBridge _bridge = null!;
        private Func<string, CancellationToken, Task<bool>>? _ask;

        public LazyToolBridge Bridge
        {
            get => _bridge;
            set
            {
                _bridge = value;
                _bridge.Ask = _ask;
            }
        }

        public Task<IReadOnlyList<ModelContextProtocol.Protocol.Tool>> ListToolsAsync(CancellationToken cancel = default) => Bridge.ListToolsAsync(cancel);

        public Task<ToolOutcome> CallAsync(string name, IReadOnlyDictionary<string, System.Text.Json.JsonElement> input, CancellationToken cancel = default) => Bridge.CallAsync(name, input, cancel);

        public Task<ToolOutcome> CallAsync(string name, IReadOnlyDictionary<string, System.Text.Json.JsonElement> input, IProgress<(double Done, string? Message)>? progress, CancellationToken cancel = default) => Bridge.CallAsync(name, input, progress, cancel);

        public Func<string, CancellationToken, Task<bool>>? Ask
        {
            get => _ask;
            set
            {
                _ask = value;
                _bridge.Ask = value;
            }
        }

        public string? CheckpointFolder { get => Bridge.CheckpointFolder; set => Bridge.CheckpointFolder = value; }
    }
}
