using System.IO.Pipelines;
using System.Text.Json;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using AVAUIDesigner.Chat;
using AVAUIDesigner.Mcp;
using AVAUIDesigner.Shell.Agents;
using AVAUIDesigner.Shell.ViewModels;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;
using static AVAUIDesigner.Shell.Tests.Agents.FakeModelServer;

namespace AVAUIDesigner.Shell.Tests.Agents;

/// <summary>Agents in the open window: the live host, the AI menu, the chat panel, the doors and the person's Save.</summary>
public class LiveAgentTests
{
    internal static (MainWindow Window, MainWindowViewModel Vm) Open(int width = 1400, int height = 900)
    {
        var vm = TestHelpers.CreateViewModel();
        var window = new MainWindow(vm) { Width = width, Height = height };
        window.AgentPipe = "avauidesigner-test-" + Guid.NewGuid().ToString("N");
        window.ChatKeys = (new ApiKeyStore(null, null), new ApiKeyStore(null, null));
        window.Agents.Connections = new ConnectionStore(null);
        // Another test may have left the chat open or agents allowed in the shared test settings.
        window.AgentSettings.ChatOpen = false;
        window.AgentSettings.AllowAgents = false;
        window.AgentSettings.AllowNetworkAgents = false;
        window.Show();
        vm.Initialize();
        TestHelpers.Pump();
        return (window, vm);
    }

    private static Dictionary<string, JsonElement> Args(object o) =>
        JsonSerializer.SerializeToElement(o).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

    [AvaloniaFact]
    public void An_agents_calls_build_in_the_window_and_ctrl_z_undoes_them()
    {
        var (w, vm) = Open();
        var root = Directory.CreateTempSubdirectory("avauid-root-").FullName;
        var bridge = AgentTests.Wait(McpToolBridge.StartAsync(w.Agents.Host(root)));
        var outcome = AgentTests.Wait(bridge.CallAsync("add_xaml", Args(new { parent = "root", xaml = "<StackPanel x:Name=\"agentPanel\"><TextBlock Text=\"Hello\" /></StackPanel>", replace = true })));
        Assert.False(outcome.IsError, outcome.Text);
        Assert.True(vm.CurrentDocument!.NamedElements.ContainsKey("agentPanel"));
        Assert.Equal("AI agent: added XAML.", vm.StatusMessage);
        Assert.Contains("agentPanel", vm.XamlSource.XamlText);
        Assert.Equal("agentPanel", Assert.Single(vm.SelectionManager.SelectedElements).Name);

        Avalonia.VisualTree.VisualExtensions.FindDescendantOfType<AVAUIDesigner.Shell.Controls.DesignCanvas>(w)!.Focus();
        w.KeyPress(Avalonia.Input.Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null);
        TestHelpers.Pump();
        Assert.False(vm.CurrentDocument!.NamedElements.ContainsKey("agentPanel"));
        AgentTests.Wait(bridge.DisposeAsync().AsTask().ContinueWith(_ => true));
    }

    [AvaloniaFact]
    public void F12_shows_the_ai_chat_and_allowing_agents_opens_the_door()
    {
        var (w, _) = Open();
        Assert.False(w.ChatHolder.IsVisible);
        w.KeyPress(Avalonia.Input.Key.F12, RawInputModifiers.None, PhysicalKey.F12, null);
        TestHelpers.Pump();
        Assert.True(w.ChatHolder.IsVisible);
        Assert.NotNull(w.Chat);
        Assert.Contains("build a screen", w.Chat!.Input.Watermark);

        Assert.StartsWith("AI agents may connect", w.SetAllowAgents(true));
        Assert.True(LiveListener.Answers(w.AgentPipe));
        Assert.Equal("AI agents are shut out.", w.SetAllowAgents(false));
        Dispatcher.UIThread.RunJobs();
        Assert.False(w.Agents.Running);
    }

    [AvaloniaFact]
    public void An_outside_agent_attaches_through_the_pipe_and_builds_in_the_window()
    {
        var (w, vm) = Open();
        Assert.StartsWith("AI agents may connect", w.SetAllowAgents(true));
        var root = Directory.CreateTempSubdirectory("avauid-root-").FullName;
        var bridge = new LiveBridge(w.AgentPipe, root);
        Pipe up = new(), down = new();
        var server = McpServer.Create(new StreamServerTransport(up.Reader.AsStream(), down.Writer.AsStream(), "attach"), bridge.Options());
        using var stop = new CancellationTokenSource();
        var serving = Task.Run(() => server.RunAsync(stop.Token));
        var client = AgentTests.Wait(McpClient.CreateAsync(new StreamClientTransport(up.Writer.AsStream(), down.Reader.AsStream())));
        Assert.Contains("live app", client.ServerInstructions);
        var result = AgentTests.Wait(client.CallToolAsync("add_assessment", new Dictionary<string, object?> { ["parent"] = "root", ["instrument"] = "gad7" }).AsTask());
        Assert.True(result.IsError == true, "the root holds one child");
        result = AgentTests.Wait(client.CallToolAsync("add_xaml", new Dictionary<string, object?> { ["parent"] = "root", ["xaml"] = "<StackPanel x:Name=\"page\" />", ["replace"] = true }).AsTask());
        Assert.False(result.IsError == true, string.Join(" ", result.Content.OfType<TextContentBlock>().Select(t => t.Text)));
        result = AgentTests.Wait(client.CallToolAsync("add_assessment", new Dictionary<string, object?> { ["parent"] = "page", ["instrument"] = "gad7" }).AsTask());
        Assert.False(result.IsError == true, string.Join(" ", result.Content.OfType<TextContentBlock>().Select(t => t.Text)));
        Assert.True(vm.CurrentDocument!.NamedElements.ContainsKey("gad7Assessment"));
        Assert.Contains("Gad7Total", vm.ViewModelText);
        // Everything this test started ends with it: work left running would reach into the next test's app.
        AgentTests.Wait(client.DisposeAsync().AsTask().ContinueWith(_ => true));
        stop.Cancel();
        AgentTests.Wait(serving.ContinueWith(_ => true));
        AgentTests.Wait(bridge.DisposeAsync().AsTask().ContinueWith(_ => true));
        w.SetAllowAgents(false);
        AgentTests.Wait(w.Agents.StopAsync().ContinueWith(_ => true));
    }

    [AvaloniaFact]
    public void The_network_door_opens_on_the_settings_and_wants_the_token()
    {
        var (w, _) = Open();
        w.AgentSettings.AllowNetworkAgents = true;
        w.AgentSettings.NetworkAgentsAddress = "localhost";
        w.AgentSettings.NetworkAgentsPort = 0;
        w.AgentSettings.NetworkAgentsFolder = Directory.CreateTempSubdirectory("avauid-net-").FullName;
        Assert.StartsWith("AI agents on other computers may connect", AgentTests.Wait(w.SyncNetworkAsync()));
        using var http = new HttpClient();
        var refused = AgentTests.Wait(http.PostAsync(w.Agents.NetworkUrls.First(), new StringContent("{}", System.Text.Encoding.UTF8, "application/json")));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, refused.StatusCode);
        w.AgentSettings.Save();
        Assert.DoesNotContain(w.NetworkToken, File.ReadAllText(AgentSettings.Path));
        w.AgentSettings.AllowNetworkAgents = false;
        Assert.Equal("AI agents on other computers are shut out.", AgentTests.Wait(w.SyncNetworkAsync()));
    }

    [AvaloniaFact]
    public void The_persons_own_save_writes_the_code_an_agent_made()
    {
        var (w, vm) = Open();
        var root = Directory.CreateTempSubdirectory("avauid-root-").FullName;
        var bridge = AgentTests.Wait(McpToolBridge.StartAsync(w.Agents.Host(root)));
        var made = AgentTests.Wait(bridge.CallAsync("scaffold_crud", Args(new
        {
            discard_changes = true,
            engine = "mysql",
            entity = new { name = "Allergy", fields = new object[] { new { name = "AllergyId", type = "int", is_key = true, generated = true }, new { name = "Allergen", required = true }, new { name = "Severity", options = new[] { "Mild", "Moderate", "Severe" } } } },
        })));
        Assert.False(made.IsError, made.Text);
        Assert.Equal(4, vm.PendingFiles.Count);
        Assert.Contains("AllergiesViewModel", vm.ViewModelText);
        Assert.Contains("SeverityOptions", vm.ViewModelText);
        Assert.True(AgentTests.Wait(vm.SaveToAsync(vm.CurrentDocument!.FilePath)));
        Assert.Empty(vm.PendingFiles);
        Assert.Contains("LAST_INSERT_ID", File.ReadAllText(Path.Combine(root, "Data", "AllergyRepository.cs")));
        Assert.True(File.Exists(Path.Combine(root, "Views", "Allergies.axaml.cs")));
        AgentTests.Wait(bridge.DisposeAsync().AsTask().ContinueWith(_ => true));
    }
}

/// <summary>
/// The README's picture of the AI Chat making a CRUD screen from a database table: the real window, a local model's
/// side replayed from a script, the tool calls run for real against a SQLite database. Writes docs/screenshots/ai-chat.png
/// only with AVAUIDESIGNER_SCREENSHOTS set; checks what it shows either way.
/// </summary>
public class AiChatReadmeScreenshots
{
    [AvaloniaFact]
    public void The_ai_chat_scaffolds_a_crud_screen_from_a_table()
    {
        var root = Directory.CreateTempSubdirectory("avauid-shot-").FullName;
        var db = Path.Combine(root, "clinic.db");
        using (var c = new SqliteConnection($"Data Source={db};Pooling=False"))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = AgentTests.Schema;
            cmd.ExecuteNonQuery();
        }
        var (w, vm) = LiveAgentTests.Open(1600, 940);
        w.Agents.Connections.Put(new DataConnection { Name = "clinic", Provider = "sqlite", ConnectionString = $"Data Source={db};Pooling=False" }, save: false);
        w.AgentSettings.WorkingFolder = root;
        w.AgentSettings.ChatProvider = "OpenAICompatible";
        w.AgentSettings.ChatServerModel = "qwen3";

        var server = new FakeModelServer()
            .Reply(Say("I'll read the patients table's structure first."), Call("c1", "describe_table", new { connection = "clinic", table = "patients" }))
            .Reply(Say("Eight columns; patient_id is generated and provider_id points at providers. Building the screen."),
                Call("c2", "scaffold_crud", new { connection = "clinic", table = "patients", @namespace = "Clinic.Views", discard_changes = true }))
            .Reply(Call("c3", "render_view", new { scale = 0.35 }))
            .Reply(Say("Done: Patients, a searchable list beside an edit form, with Provider as a drop-down of providers. Save writes the view, its view model, the Patient model and a SQLite repository. Ctrl+Z or Undo this reply takes it back."));

        w.ToggleChat();
        var chat = w.Chat!;
        chat.ServerFactory = (_, _) => server.Client();
        chat.LoadSettings();
        chat.Input.Text = "Make a CRUD screen for the patients table in clinic";
        chat.Send();
        for (var i = 0; i < 3000 && (chat.Session.Working || server.Left > 0); i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(2);
        }

        Assert.Equal(0, server.Left);
        Assert.All(chat.Session.Entries.OfType<ToolEntry>(), t => Assert.False(t.IsError, t.Result));
        Assert.Equal("Clinic.Views.Patients", vm.CurrentDocument!.XClass);
        Assert.Contains(chat.Session.Entries.OfType<ReplyUndoEntry>(), e => e.CanUndo);

        if (Environment.GetEnvironmentVariable("AVAUIDESIGNER_SCREENSHOTS") is not { Length: > 0 } folder) return;
        vm.ZoomFactor = 0.52;
        TestHelpers.Pump();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        TestHelpers.Pump();
        Directory.CreateDirectory(folder);
        w.CaptureRenderedFrame()!.Save(Path.Combine(folder, "ai-chat.png"));
    }

    /// <summary>The README's pictures of two recipes' screens, as render_view draws them (only with AVAUIDESIGNER_SCREENSHOTS set).</summary>
    [AvaloniaFact]
    public void The_recipes_screens_are_drawn()
    {
        if (Environment.GetEnvironmentVariable("AVAUIDESIGNER_SCREENSHOTS") is not { Length: > 0 } folder) return;
        foreach (var (recipe, file) in new (Func<string>, string)[] { (() => Recipes.PatientChart(), "ai-patient-chart.png"), (Recipes.Phq9Screening, "ai-phq9.png") })
        {
            using var rig = new AgentTests.Rig();
            ToolOutcome? last = null;
            foreach (var call in recipe().Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith(Recipes.CallMarker, StringComparison.Ordinal)))
            {
                var rest = call[Recipes.CallMarker.Length..];
                var space = rest.IndexOf(' ');
                last = rig.Call(rest[..space], JsonDocument.Parse(rest[(space + 1)..]).RootElement);
                Assert.False(last.IsError, last.Text);
            }
            File.WriteAllBytes(Path.Combine(folder, file), Assert.Single(last!.Images).Data);
        }
    }
}
