using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAUIDesigner.Chat;
using AVAUIDesigner.Controls;
using AVAUIDesigner.Data;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.UndoRedo;
using AVAUIDesigner.Mcp;
using AVAUIDesigner.Shell.Agents;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Server;
using Xunit;
using static AVAUIDesigner.Shell.Tests.Agents.FakeModelServer;

namespace AVAUIDesigner.Shell.Tests.Agents;

internal sealed class AvaloniaThread : IUiThread
{
    public Task<T> InvokeAsync<T>(Func<Task<T>> work) => Dispatcher.UIThread.InvokeAsync(work);
}

/// <summary>
/// The MCP server and the AI Chat, driven as agents drive them: through an in-process MCP client over the real server,
/// on Avalonia's headless platform. The chat's model is a scripted fake OpenAI-compatible server and the database a
/// SQLite file; no real model, key or server is ever used.
/// </summary>
public class AgentTests
{
    /// <summary>Every tool the server offers. A new tool goes here, with descriptions on it and its arguments.</summary>
    internal static readonly string[] ToolNames =
    [
        "get_document", "get_xaml", "set_xaml", "new_document", "open_document", "save_document", "list_controls",
        "describe_control", "add_xaml", "add_controls", "set_properties", "set_event", "delete_elements", "move_element",
        "set_grid", "wrap_in", "make_mvvm", "get_code", "render_view", "list_files", "read_file", "undo", "redo",
        "read_entities", "list_connections", "add_connection", "list_tables", "describe_table", "add_form", "scaffold_crud",
        "add_assessment", "batch", "get_recording", "save_steps", "run_steps", "checkpoint", "diff", "render_checkpoint",
        "restore_checkpoint",
    ];

    internal const string PatientClass = """
        using System.ComponentModel.DataAnnotations;
        using System.ComponentModel.DataAnnotations.Schema;

        namespace Clinic.Models;

        public enum Sex { Female, Male, Intersex, Unknown }

        [Table("patients")]
        public class Patient
        {
            [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
            public int PatientId { get; set; }

            [Required, MaxLength(50), Display(Name = "First name")]
            public string FirstName { get; set; } = "";

            [Required, MaxLength(50)]
            public string LastName { get; set; } = "";

            public DateOnly? DateOfBirth { get; set; }

            public Sex? Sex { get; set; }

            [EmailAddress]
            public string? Email { get; set; }

            [Range(0, 400)]
            public decimal? WeightKg { get; set; }

            public bool Active { get; set; }

            public List<Visit> Visits { get; set; } = [];
        }

        public record Visit(int VisitId, int PatientId, DateTime Seen, string Reason);
        """;

    internal const string Schema = """
        -- The clinic's tables.
        CREATE TABLE providers (provider_id INTEGER PRIMARY KEY, full_name TEXT NOT NULL);
        CREATE TABLE patients (
            patient_id INTEGER PRIMARY KEY,
            first_name VARCHAR(50) NOT NULL,
            last_name VARCHAR(50) NOT NULL,
            date_of_birth DATE,
            sex TEXT CHECK (sex IN ('Female', 'Male', 'Other')),
            email VARCHAR(120),
            notes TEXT,
            provider_id INTEGER REFERENCES providers(provider_id)
        );
        """;

    /// <summary>A server on a design of its own, in a temporary working folder with a clinic database, and a client.</summary>
    internal sealed class Rig : IDisposable
    {
        private readonly Window _window;

        public HeadlessDesk Desk { get; } = new(TestHelpers.Registry);
        public string Root { get; } = Directory.CreateTempSubdirectory("avauid-agent-").FullName;
        public ConnectionStore Connections { get; } = new(null);
        public McpToolBridge Bridge { get; }
        public DesignDocument Document => Desk.Session.Document;
        public UndoRedoStack History => Desk.Session.History;

        public Rig()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Models"));
            File.WriteAllText(Path.Combine(Root, "Models", "Patient.cs"), PatientClass);
            File.WriteAllText(Path.Combine(Root, "schema.sql"), Schema);
            var db = Path.Combine(Root, "clinic.db");
            using (var c = new SqliteConnection($"Data Source={db};Pooling=False"))
            {
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = Schema + "INSERT INTO providers (full_name) VALUES ('Dr Adams');";
                cmd.ExecuteNonQuery();
            }
            Connections.Put(new DataConnection { Name = "clinic", Provider = "sqlite", ConnectionString = $"Data Source={db};Pooling=False" }, save: false);
            var stage = new Panel();
            _window = new Window { Width = 20, Height = 20, Content = stage };
            _window.Show();
            Bridge = Wait(McpToolBridge.StartAsync(new DesignHost(new AvaloniaThread(), Desk, new DesignLibrary(stage, TestHelpers.Registry), Root, Connections)));
        }

        public ToolOutcome Call(string tool, object? args = null) =>
            Wait(Bridge.CallAsync(tool, JsonSerializer.SerializeToElement(args ?? new { }).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())));

        public JsonElement Ok(string tool, object? args = null)
        {
            var o = Call(tool, args);
            Assert.False(o.IsError, $"{tool} failed: {o.Text}");
            return o.Text.StartsWith('{') ? JsonDocument.Parse(o.Text).RootElement.Clone() : default;
        }

        public string Xaml => Desk.Session.Serializer.Serialize(Document);

        public void Dispose()
        {
            Wait(Bridge.DisposeAsync().AsTask().ContinueWith(_ => true));
            _window.Close();
        }
    }

    internal static T Wait<T>(Task<T> task)
    {
        for (var i = 0; i < 6000 && !task.IsCompleted; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(2);
        }
        Assert.True(task.IsCompleted, "It didn't finish in time.");
        Dispatcher.UIThread.RunJobs();
        return task.GetAwaiter().GetResult();
    }

    /// <summary>C# that has no syntax errors (the generated code is also built for real by GeneratedCrudBuildTests).</summary>
    internal static void Parses(string code, string what)
    {
        var errors = CSharpSyntaxTree.ParseText(code).GetDiagnostics().Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, $"{what}: {string.Join("; ", errors)}\n{code}");
    }

    [AvaloniaFact]
    public void The_tools_are_the_ones_listed_and_each_says_what_it_and_its_arguments_are()
    {
        using var rig = new Rig();
        var tools = Wait(rig.Bridge.ListToolsAsync());
        Assert.Equal(ToolNames.Order(), tools.Select(t => t.Name).Order());
        foreach (var t in tools)
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Description), $"{t.Name} has no description.");
            foreach (var p in t.InputSchema.GetProperty("properties").EnumerateObject())
                Assert.True(p.Value.TryGetProperty("description", out var d) && d.GetString()!.Length > 0, $"{t.Name}'s {p.Name} has no description.");
        }
    }

    [AvaloniaFact]
    public void Every_recipe_runs_call_by_call()
    {
        var recipes = typeof(Recipes).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.GetCustomAttribute<McpServerPromptAttribute>() != null).ToList();
        Assert.Equal(11, recipes.Count);
        foreach (var recipe in recipes)
        {
            using var rig = new Rig();
            if (recipe.Name == nameof(Recipes.CrudFromTable)) File.Delete(Path.Combine(rig.Root, "Models", "Patient.cs"));
            var text = (string)recipe.Invoke(null, recipe.GetParameters().Select(_ => (object?)null).ToArray())!;
            var calls = text.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith(Recipes.CallMarker, StringComparison.Ordinal)).ToList();
            Assert.True(calls.Count >= 2, $"{recipe.Name} has few steps.");
            foreach (var call in calls)
            {
                var rest = call[Recipes.CallMarker.Length..];
                var space = rest.IndexOf(' ');
                var o = rig.Call(rest[..space], JsonDocument.Parse(rest[(space + 1)..]).RootElement);
                Assert.False(o.IsError, $"{recipe.Name}: {rest[..space]} failed: {o.Text}");
                Assert.DoesNotContain("\"warnings\":[", o.Text);
            }
            Assert.True(rig.Document.AllElements.Count() >= 12, $"{recipe.Name} built little.");
            foreach (var (file, code) in rig.Desk.Session.Pending) Parses(code, $"{recipe.Name}: {Path.GetFileName(file)}");
            Parses(rig.Desk.Session.CodeBehind, recipe.Name + " code-behind");
            if (rig.Document.CodeStyle == CodeBehindStyle.MVVM) Parses(rig.Desk.Session.ViewModel, recipe.Name + " view model");
        }
    }

    [AvaloniaFact]
    public void Xaml_is_added_named_placed_and_undone_in_one_step()
    {
        using var rig = new Rig();
        rig.Ok("new_document", new { class_name = "Login", kind = "Window", mvvm = false });
        var added = rig.Ok("add_xaml", new
        {
            parent = "layoutRoot",
            xaml = """
                <StackPanel Margin="24" Spacing="8">
                  <TextBlock Text="Sign in" FontSize="20" />
                  <TextBox Watermark="User name" />
                  <TextBox x:Name="passwordBox" PasswordChar="•" />
                  <Button Content="Sign in" />
                </StackPanel>
                """,
        });
        Assert.Equal("0/0", added.GetProperty("added")[0].GetProperty("path").GetString());
        Assert.Contains("passwordBox", added.GetProperty("names").EnumerateArray().Select(n => n.GetString()));
        Assert.Contains("button1", rig.Document.NamedElements.Keys);
        var depth = rig.History.UndoCount;

        rig.Ok("set_properties", new { target = "button1", properties = new Dictionary<string, string?> { ["Content"] = "Log in", ["HorizontalAlignment"] = "Right" } });
        rig.Ok("set_event", new { target = "button1", @event = "Click" });
        Assert.Contains("void Button1_Click(", rig.Desk.Session.CodeBehind);
        Assert.Equal(depth + 2, rig.History.UndoCount);
        Assert.Equal("Log in", rig.Document.NamedElements["button1"].GetValue("Content"));
        rig.Ok("undo");
        rig.Ok("undo");
        Assert.Equal("Sign in", rig.Document.NamedElements["button1"].GetValue("Content"));

        // A mistake is named, and leaves the design as it was.
        var before = rig.Xaml;
        Assert.Contains("did you mean Watermark", rig.Ok("set_properties", new { target = "textBox1", properties = new Dictionary<string, string?> { ["Placeholder"] = "x" } }).GetProperty("warnings")[0].GetString());
        rig.Ok("undo");
        Assert.Equal(before, rig.Xaml);
        Assert.Contains("doesn't parse", rig.Call("add_xaml", new { parent = "layoutRoot", xaml = "<Button Content=\"x\">" }).Text);
        Assert.Contains("There's no element called \"nope\"", rig.Call("delete_elements", new { target = "nope" }).Text);
        Assert.Contains("holds one child", rig.Call("add_xaml", new { parent = "root", xaml = "<Button />" }).Text);
        Assert.Equal(before, rig.Xaml);

        // Grids: rows, columns and cells.
        rig.Ok("wrap_in", new { container = "Grid", target = "0/0", name = "outer" });
        rig.Ok("set_grid", new { target = "outer", columns = "*,320,*", cells = new Dictionary<string, int[]> { ["0/0/0"] = [0, 1] } });
        Assert.Equal("1", rig.Document.NamedElements["outer"].Children[0].GetValue("Grid.Column"));
        Assert.Contains("isn't a list of sizes", rig.Call("set_grid", new { target = "outer", rows = "Auto,big" }).Text);
    }

    [AvaloniaFact]
    public void Controls_are_added_from_specs_and_a_view_turns_mvvm()
    {
        using var rig = new Rig();
        rig.Ok("new_document", new { class_name = "Settings", mvvm = false });
        rig.Ok("add_controls", new
        {
            parent = "layoutRoot",
            controls = new object[]
            {
                new
                {
                    type = "StackPanel", name = "settingsPanel", properties = new { Margin = "16", Spacing = "8" },
                    children = new object[]
                    {
                        new { type = "CheckBox", name = "darkCheck", text = "Dark theme" },
                        new { type = "TextBox", name = "nameBox", properties = new { Watermark = "Your name" } },
                        new { type = "Button", name = "applyButton", text = "Apply", events = new { Click = "Apply_Click" } },
                    },
                },
            },
        });
        Assert.Equal("Dark theme", rig.Document.NamedElements["darkCheck"].GetValue("Content"));
        rig.Ok("make_mvvm", new { bind_controls = true });
        Assert.Equal(CodeBehindStyle.MVVM, rig.Document.CodeStyle);
        Assert.Equal("{Binding NameBoxText, Mode=TwoWay}", rig.Document.NamedElements["nameBox"].GetValue("Text"));
        Assert.Contains("_nameBoxText", rig.Desk.Session.ViewModel);
        rig.Ok("undo");
        Assert.Equal(CodeBehindStyle.CodeBehind, rig.Document.CodeStyle);
        Assert.Null(rig.Document.NamedElements["nameBox"].GetValue("Text"));

        var described = rig.Ok("describe_control", new { type = "DataGrid" });
        Assert.Contains(described.GetProperty("properties").EnumerateArray(), p => p.GetProperty("name").GetString() == "ItemsSource");
        Assert.True(rig.Ok("list_controls").GetProperty("categories").EnumerateObject().Count() >= 3);
    }

    [AvaloniaFact]
    public void Entities_are_read_from_csharp_and_sql_and_a_database_catalogue()
    {
        using var rig = new Rig();
        var cs = rig.Ok("read_entities", new { path = "Models/Patient.cs" }).GetProperty("entities");
        Assert.Equal(2, cs.GetArrayLength());
        var patient = cs[0];
        Assert.Equal("Clinic.Models", patient.GetProperty("namespace").GetString());
        var fields = patient.GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!);
        Assert.False(fields.ContainsKey("Visits"));
        Assert.True(fields["PatientId"].GetProperty("generated").GetBoolean());
        Assert.Equal(50, fields["FirstName"].GetProperty("max_length").GetInt32());
        Assert.Equal("choice", fields["Sex"].GetProperty("editor").GetString());
        Assert.Equal("email", fields["Email"].GetProperty("editor").GetString());
        Assert.Equal(400, fields["WeightKg"].GetProperty("max").GetDouble());
        Assert.Equal("date", fields["DateOfBirth"].GetProperty("editor").GetString());

        var sql = rig.Ok("read_entities", new { path = "schema.sql" }).GetProperty("entities");
        var p = sql.EnumerateArray().Single(e => e.GetProperty("name").GetString() == "Patient");
        var pf = p.GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!);
        Assert.Equal(3, pf["Sex"].GetProperty("options").GetArrayLength());
        Assert.Equal("providers", pf["ProviderId"].GetProperty("references").GetString());
        Assert.Equal("multiline", pf["Notes"].GetProperty("editor").GetString());

        Assert.Equal(["patients", "providers"], rig.Ok("list_tables", new { connection = "clinic" }).GetProperty("tables").EnumerateArray().Select(t => t.GetString()));
        var table = rig.Ok("describe_table", new { connection = "clinic", table = "patients" });
        var tf = table.GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!);
        Assert.Equal("lookup", tf["ProviderId"].GetProperty("editor").GetString());
        Assert.Contains("shows full_name", tf["ProviderId"].GetProperty("references").GetString());
        Assert.Equal(50, tf["FirstName"].GetProperty("max_length").GetInt32());
        Assert.Contains("There's no table", rig.Call("describe_table", new { connection = "clinic", table = "visits" }).Text);
    }

    [AvaloniaFact]
    public void Connections_never_show_their_passwords()
    {
        using var rig = new Rig();
        rig.Connections.Put(new DataConnection { Name = "ehr", Provider = "postgresql", ConnectionString = "Host=db1;Database=ehr;Username=reader;Password=Sekrit-99" }, save: false);
        var listed = rig.Call("list_connections").Text;
        Assert.Contains("db1", listed);
        Assert.DoesNotContain("Sekrit-99", listed);
        Assert.Contains("There's no database connection \"x\"", rig.Call("list_tables", new { connection = "x" }).Text);
        Assert.Contains("Couldn't connect", rig.Call("add_connection", new { name = "bad", engine = "sqlite", connection_string = "Data Source=/nonexistent/dir/x.db;Mode=ReadOnly" }).Text);

        var path = Path.Combine(rig.Root, "connections.json");
        var store = new ConnectionStore(path);
        store.Put(new DataConnection { Name = "kept", Provider = "mssql", ConnectionString = "Server=x;Password=p" }, save: true);
        Assert.Equal("SqlServer", new ConnectionStore(path).Get("KEPT").Provider);
        if (!OperatingSystem.IsWindows()) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [AvaloniaFact]
    public void A_crud_screen_is_scaffolded_from_a_table_and_saved_with_its_code()
    {
        using var rig = new Rig();
        File.Delete(Path.Combine(rig.Root, "Models", "Patient.cs"));
        var made = rig.Ok("scaffold_crud", new { connection = "clinic", table = "patients", @namespace = "Clinic.Views" });
        Assert.Equal("SQLite", made.GetProperty("engine").GetString());
        Assert.Equal("Clinic.Views.Patients", rig.Document.XClass);
        Assert.Equal(CodeBehindStyle.MVVM, rig.Document.CodeStyle);
        foreach (var n in new[] { "patientsGrid", "patientForm", "searchBox", "saveButton", "firstNameBox", "providerIdCombo", "statusText" })
            Assert.True(rig.Document.NamedElements.ContainsKey(n), n);
        Assert.Equal("{Binding Selected.FirstName, Mode=TwoWay}", rig.Document.NamedElements["firstNameBox"].GetValue("Text"));
        var code = made.GetProperty("code").EnumerateArray().Select(c => c.GetString()).ToList();
        Assert.Equal(["ViewModels/PatientsViewModel.cs", "Data/PatientRepository.cs", "Data/LookupItem.cs", "Models/Patient.cs"], code);
        Assert.Contains(rig.Call("render_view").Images, i => i.Data.Length > 2000);

        var saved = rig.Ok("save_document").GetProperty("saved").EnumerateArray().Select(s => s.GetString()).ToList();
        Assert.Contains("Views/Patients.axaml", saved);
        Assert.Empty(rig.Desk.Session.Pending);
        var vm = File.ReadAllText(Path.Combine(rig.Root, "ViewModels", "PatientsViewModel.cs"));
        var repo = File.ReadAllText(Path.Combine(rig.Root, "Data", "PatientRepository.cs"));
        Parses(vm, "view model");
        Parses(repo, "repository");
        Parses(File.ReadAllText(Path.Combine(rig.Root, "Models", "Patient.cs")), "model");
        // The designer's own view-model pass found everything the view binds already there: nothing doubled.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(vm, @"Task Load\("));
        Assert.DoesNotContain("TODO", vm);
        Assert.Contains("RETURNING \\\"patient_id\\\"", repo);
        Assert.Contains("ProviderIdLookupAsync", repo);
        Assert.Contains("<DataGrid", File.ReadAllText(Path.Combine(rig.Root, "Views", "Patients.axaml")));

        // Saving doesn't write over the person's own files unless told to (this session's may be written again).
        using var other = new Rig();
        other.Ok("scaffold_crud", new { connection = "clinic", table = "patients" });
        Assert.Contains("Models/Patient.cs already exists", other.Call("save_document").Text);
        Assert.Equal(PatientClass, File.ReadAllText(Path.Combine(other.Root, "Models", "Patient.cs")));
        other.Ok("save_document", new { overwrite = true });
    }

    [AvaloniaFact]
    public void A_crud_screen_for_a_class_uses_the_class_and_its_dates_go_through_converters()
    {
        using var rig = new Rig();
        var made = rig.Ok("scaffold_crud", new { file = "Models/Patient.cs", class_name = "Patient", engine = "postgresql" });
        var code = made.GetProperty("code").EnumerateArray().Select(c => c.GetString()).ToList();
        Assert.DoesNotContain("Models/Patient.cs", code);
        Assert.Contains("ViewModels/Converters.cs", code);
        Assert.Contains("using Clinic.Models;", rig.Desk.Session.Pending.Single(p => p.Key.EndsWith("PatientRepository.cs")).Value);
        Assert.Contains("Converter={x:Static vm:Converters.DateOnly}", rig.Xaml);
        Assert.Contains("ILIKE", rig.Desk.Session.Pending.Single(p => p.Key.EndsWith("PatientRepository.cs")).Value);
        Assert.Contains("has 2 entities: give class_name", rig.Call("scaffold_crud", new { file = "Models/Patient.cs", discard_changes = true }).Text);
    }

    [AvaloniaFact]
    public void Assessments_are_added_scored_and_their_view_model_merged()
    {
        using var rig = new Rig();
        rig.Ok("new_document", new { class_name = "Screening", @namespace = "Clinic.Views" });
        rig.Ok("add_xaml", new { parent = "layoutRoot", xaml = "<StackPanel x:Name=\"page\" />" });
        var phq = rig.Ok("add_assessment", new { parent = "page", instrument = "phq9" });
        Assert.Equal(9, phq.GetProperty("items").GetInt32());
        Assert.Equal(36, rig.Document.AllElements.Count(e => e.ElementType == "RadioButton"));
        Assert.Equal("{Binding Phq9Q9, Converter={x:Static vm:Converters.Score}, ConverterParameter=3}", rig.Document.NamedElements["phq9Q9_3"].GetValue("IsChecked"));
        rig.Ok("add_assessment", new { parent = "page", instrument = "gad7" });
        Assert.Contains("already has", rig.Call("add_assessment", new { parent = "page", instrument = "phq9" }).Text);
        var vm = rig.Desk.Session.Pending.Single(p => p.Key.EndsWith("ScreeningViewModel.cs")).Value;
        Assert.Contains("_phq9Q9", vm);
        Assert.Contains("Gad7Total", vm);
        Assert.Contains("suicide-risk protocol", vm);
        Parses(vm, "view model");
        Parses(rig.Desk.Session.ViewModel, "merged view model");
        Assert.DoesNotContain("bool? _phq9Q1", rig.Desk.Session.ViewModel);
        rig.Ok("save_document");
        Assert.True(File.Exists(Path.Combine(rig.Root, "ViewModels", "Converters.cs")));

        var cans = Assessments.Cans();
        Assert.Equal(45, cans.ItemCount);
        var members = Assessments.Members(cans);
        Assert.Contains("CansActionCount", members);
        Assert.Contains("strengths to build on", members);
        Assert.Contains("isn't a scale point", rig.Call("add_assessment", new { parent = "page", definition = new { key = "X", title = "X", scale = new[] { "zero" }, sections = new[] { new { items = new[] { "a" } } } } }).Text);
    }

    [AvaloniaFact]
    public void Files_outside_the_working_folder_and_binary_ones_are_refused()
    {
        using var rig = new Rig();
        Assert.Contains("outside the working folder", rig.Call("read_file", new { path = "../../etc/passwd" }).Text);
        Assert.Contains("outside the working folder", rig.Call("read_entities", new { path = "/etc/hosts.cs" }).Text);
        File.WriteAllBytes(Path.Combine(rig.Root, "blob.bin"), [1, 2, 0, 3]);
        Assert.Contains("isn't text", rig.Call("read_file", new { path = "blob.bin" }).Text);
        var listed = rig.Ok("list_files", new { recursive = true, pattern = "*.cs" });
        Assert.Equal("Models/Patient.cs", listed.GetProperty("files")[0].GetProperty("path").GetString()!.Replace('\\', '/'));
        Assert.Contains("outside the working folder", rig.Call("new_document", new { class_name = "X", folder = "../../x", discard_changes = true }).Text);
    }

    [AvaloniaFact]
    public void A_call_that_fails_changes_nothing_and_a_batch_is_one_step_or_none()
    {
        using var rig = new Rig();
        rig.Ok("new_document", new { class_name = "Form" });
        rig.Ok("add_xaml", new { parent = "layoutRoot", xaml = "<StackPanel x:Name=\"panel\"><TextBlock x:Name=\"a\" Text=\"A\" /></StackPanel>" });
        var before = rig.Xaml;
        var depth = rig.History.UndoCount;

        var failed = rig.Call("batch", new { steps = new object[] { new { tool = "set_properties", args = new { target = "a", properties = new { Text = "B" } } }, new { tool = "delete_elements", args = new { target = "nope" } } } });
        Assert.Contains("Step 2 (delete_elements) failed", failed.Text);
        Assert.Equal(before, rig.Xaml);
        Assert.Equal(depth, rig.History.UndoCount);

        var dry = rig.Ok("batch", new { dry_run = true, steps = new object[] { new { tool = "add_xaml", args = new { parent = "panel", xaml = "<Button />" } } } });
        Assert.Equal(1, dry.GetProperty("would_change").GetProperty("added").GetArrayLength());
        Assert.Equal(before, rig.Xaml);

        rig.Ok("batch", new
        {
            steps = new object[]
            {
                new { tool = "add_xaml", args = new { parent = "panel", xaml = "<Button x:Name=\"go\" Content=\"Go\" />" } },
                new { tool = "set_properties", args = new { target = "a", properties = new { Text = "Ready?" } } },
            },
        });
        Assert.Equal(depth + 1, rig.History.UndoCount);
        rig.Ok("undo");
        Assert.Equal(before, rig.Xaml);
    }

    [AvaloniaFact]
    public void A_session_is_kept_as_steps_and_run_again_with_other_values()
    {
        using var rig = new Rig();
        rig.Ok("new_document", new { class_name = "Form" });
        rig.Ok("add_xaml", new { parent = "layoutRoot", xaml = "<TextBlock x:Name=\"t\" Text=\"Hello\" />" });
        Assert.Equal("add_xaml", rig.Ok("get_recording").GetProperty("steps")[0].GetProperty("tool").GetString());
        rig.Ok("save_steps", new
        {
            path = "heading",
            steps = new object[] { new { tool = "add_xaml", args = new { parent = "layoutRoot", xaml = "<TextBlock Text=\"${title}\" FontSize=\"${size}\" />" } } },
            parameters = new { title = "Patients", size = "20" },
        });
        rig.Ok("run_steps", new { path = "heading.avasteps.json", values = new { title = "Providers" } });
        Assert.Contains(rig.Document.AllElements, e => e.GetValue("Text") == "Providers" && e.GetValue("FontSize") == "20");
        Assert.Contains("no parameter", rig.Call("run_steps", new { path = "heading.avasteps.json", values = new { colour = "red" } }).Text);
    }

    [AvaloniaFact]
    public void A_checkpoint_is_compared_with_drawn_and_gone_back_to()
    {
        using var rig = new Rig();
        rig.Ok("new_document", new { class_name = "Form" });
        rig.Ok("checkpoint", new { name = "before" });
        rig.Ok("add_xaml", new { parent = "layoutRoot", xaml = "<StackPanel><TextBox /><Button /></StackPanel>" });
        var diff = rig.Ok("diff", new { since = "before" });
        Assert.Equal("+3 elements, 1 changed", ChatSession.Summarise(JsonNode.Parse(diff.GetRawText())!.AsObject()));
        Assert.Single(rig.Call("render_checkpoint", new { name = "before" }).Images);
        rig.Ok("restore_checkpoint", new { name = "before" });
        Assert.Empty(rig.Document.NamedElements["layoutRoot"].Children);
        rig.Ok("undo");
        Assert.Single(rig.Document.NamedElements["layoutRoot"].Children);
    }

    [AvaloniaFact]
    public void A_design_is_saved_and_opened_again_with_its_code()
    {
        using var rig = new Rig();
        rig.Ok("new_document", new { class_name = "About", kind = "Window", mvvm = false });
        rig.Ok("add_xaml", new { parent = "layoutRoot", xaml = "<Button x:Name=\"okButton\" Content=\"OK\" Click=\"Ok_Click\" />" });
        rig.Ok("save_document");
        var cs = File.ReadAllText(Path.Combine(rig.Root, "Views", "About.axaml.cs"));
        Assert.Contains("void Ok_Click(", cs);
        File.WriteAllText(Path.Combine(rig.Root, "Views", "About.axaml.cs"), cs.Replace("// TODO", "Close(); // kept"));
        rig.Ok("new_document", new { class_name = "Other" });
        rig.Ok("open_document", new { path = "Views/About.axaml", discard_changes = true });
        Assert.Equal("OK", rig.Document.NamedElements["okButton"].GetValue("Content"));
        rig.Ok("set_properties", new { target = "okButton", properties = new { Content = "Close" } });
        rig.Ok("save_document");
        Assert.Contains("Close(); // kept", File.ReadAllText(Path.Combine(rig.Root, "Views", "About.axaml.cs")));
        Assert.Contains("x:Class=\"MyApp.Views.About\"", rig.Call("get_xaml").Text);
    }

    [AvaloniaFact]
    public void A_chat_on_a_local_model_builds_a_screening_form_and_its_reply_can_be_undone()
    {
        using var rig = new Rig();
        rig.Ok("new_document", new { class_name = "Screening" });
        var server = new FakeModelServer()
            .Reply(Say("I'll add the PHQ-9."), Call("c1", "add_assessment", new { parent = "layoutRoot", instrument = "phq9" }))
            .Reply(Call("c2", "render_view", new { scale = 0.4 }))
            .Reply(Say("Done: the PHQ-9 with its total and severity."));
        var chat = new ChatSession(rig.Bridge) { Provider = ChatProvider.OpenAICompatible, Server = server.Client(), ServerModel = "qwen3" };
        Wait(chat.SendAsync("add a PHQ-9 screening").ContinueWith(t => { t.GetAwaiter().GetResult(); return true; }));

        Assert.Equal(["add_assessment", "render_view"], chat.Entries.OfType<ToolEntry>().Select(t => t.Name));
        Assert.All(chat.Entries.OfType<ToolEntry>(), t => Assert.False(t.IsError, t.Result));
        Assert.Contains("AVAUIDesigner", (string?)server.Requests[0]["messages"]![0]!["content"]);
        Assert.True(rig.Document.NamedElements.ContainsKey("phq9Assessment"));

        var undo = Assert.Single(chat.Entries.OfType<ReplyUndoEntry>());
        Assert.Contains("+", undo.Summary);
        Assert.True(Wait(chat.UndoReplyAsync(undo)));
        Assert.False(rig.Document.NamedElements.ContainsKey("phq9Assessment"));
    }

    [AvaloniaFact]
    public void Core_tools_for_small_models_are_all_real_tools()
    {
        foreach (var name in ChatTools.Core) Assert.Contains(name, ToolNames);
    }

    [Fact]
    public void Words_names_and_types_come_out_right()
    {
        Assert.Equal("Date of birth", Words.FromIdentifier("DateOfBirth"));
        Assert.Equal("Patient ID", Words.FromIdentifier("patient_id"));
        Assert.Equal("Allergies", Words.Plural("Allergy"));
        Assert.Equal("Diagnoses", Words.Plural("Diagnosis"));
        Assert.Equal("allergy", Words.Singular("allergies"));
        Assert.Equal("Diagnosis", Words.Singular("Diagnoses"));
        Assert.Equal("FirstName", Identifiers.Pascal("FIRST_NAME"));
        Assert.Equal("long", TypeMap.FromSql("NUMBER", 10, 0));
        Assert.Equal("decimal", TypeMap.FromSql("numeric(6,2)", 6, 2));
        Assert.Equal("Host=db;Database=x", ConnectionStore.Redact("Host=db;Database=x;Password=y"));
        foreach (var p in SqlDialect.All.Keys)
        {
            var e = new EntitySpec { Name = "Note", Fields = [new() { Name = "Id", Type = "int" }, new() { Name = "Text", Required = true }] }.ToModel();
            var code = CrudGenerator.Repository(e, CrudNames.For(e, "App.Views"), p, false);
            Parses(code, p.ToString());
        }
    }
}
