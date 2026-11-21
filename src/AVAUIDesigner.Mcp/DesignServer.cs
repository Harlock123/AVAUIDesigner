using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AVAUIDesigner.Mcp;

/// <summary>
/// The MCP server: <see cref="DesignTools"/>' methods as MCP tools (the SDK's attribute model, read by reflection),
/// with instructions that tell an agent AVAUIDesigner's conventions. The same server runs over stdio headless, over a
/// pipe for agents attached to the open designer, and in-process for the program's own AI Chat.
/// </summary>
public static class DesignServer
{
    public const string Name = "AVAUIDesigner";

    /// <summary>What every agent is told on connecting: the conventions the tools' descriptions build on.</summary>
    public const string Instructions = """
        AVAUIDesigner is a visual designer for Avalonia user interfaces: it edits .axaml views (a Window or a UserControl) on a live design surface, keeps their XAML in step, and writes their code-behind and, for MVVM views, their view models (CommunityToolkit.Mvvm). You design with the same controls and properties a person uses in it, and look at your work with render_view.

        Conventions (they matter):
        - A design is a tree of elements. Each element is named by its x:Name ("patientForm"), or by its path of child positions from the root ("0/2/1"; "root" is the root itself). get_document shows the tree with each element's name or path; every element you add gets an x:Name, so name things meaningfully (firstNameBox, saveButton).
        - Property values are XAML text, exactly as written in a .axaml file: "12", "Auto,*,Auto", "#1E88E5", "{Binding Patient.FirstName, Mode=TwoWay}", "{StaticResource …}". Attached properties are written with their owner: "Grid.Row", "Grid.ColumnSpan", "DockPanel.Dock", "Canvas.Left".
        - Layout is by panels, as Avalonia apps are built: Grid (RowDefinitions and ColumnDefinitions such as "Auto,*" with Grid.Row and Grid.Column on the children), StackPanel (Orientation, Spacing), DockPanel, WrapPanel, UniformGrid, ScrollViewer, Border, TabControl with TabItems, Expander. Use Canvas only for drawings. Prefer Margin, Spacing and alignment to fixed sizes.
        - add_xaml takes a fragment of ordinary Avalonia XAML (one or more elements) and puts it into a parent: the quickest way to build anything. add_controls does the same from a list of control specs.
        - MVVM: a view whose root has x:DataType and a DataContext of its view model ("vm:PatientsViewModel") is MVVM. Bind with {Binding …}; saving writes the view model with a property for each binding and a command for each Command binding, keeping any code already there. make_mvvm turns a view into one.
        - Every call that changes the design is one undo step (undo takes it back; in the program Ctrl+Z does too), and a call that fails changes nothing.

        Screens from data: an entity is a data structure a screen edits. read_entities reads them from C# classes and records or SQL CREATE TABLE scripts in the working folder; list_tables and describe_table read them from a connected database's own catalogue (structure only: no rows are ever read). add_form lays out a form for an entity's fields (the right editor for each type: text, multi-line, dates, numbers, money, check boxes, choices, lookups of related tables). scaffold_crud makes a whole CRUD screen as a new view: a searchable list and an edit form (or an editable grid), with its view model (load, search, new, save, delete, checks before saving) and, when saved, the model class and a repository with the database's own parameterised SQL (SQL Server, PostgreSQL, MySQL or MariaDB, Oracle, SQLite, Db2 or ODBC).

        Clinical and record-keeping screens: add_assessment adds a rated assessment with its scoring: PHQ-9, GAD-7, a CANS core set, or any instrument described by its scale, sections and items. The server's prompts are saved recipes for common healthcare screens (a patient chart with a banner and tabs, an intake form, vitals, a medication list, a SOAP progress note, PHQ-9 and CANS assessments, appointment scheduling, and CRUD screens from a table or a class): step-by-step plans with the exact tool calls, to follow or adapt.

        A good way to work: get_document; build with add_xaml or add_form; render_view to look at it; fix what doesn't read well (cramped or misaligned controls, text cut off, a form too wide); save_document when asked (it writes the .axaml, its code-behind, its view model and any generated model and repository files). batch runs several changes as one undo step (dry_run to preview it), and save_steps keeps a sequence as a steps file with ${parameters} that run_steps runs again with other values.

        Never put real patient data in a design: use made-up names in samples and watermarks. Generated screens read and write data only when the finished application runs.
        """;

    /// <summary>The MCP tools, bound to a host.</summary>
    public static IEnumerable<McpServerTool> Tools(IDesignHost host) => Tools(new DesignTools(host));

    /// <summary>The MCP tools of one set of handlers (one session's: its checkpoints and its recording).</summary>
    public static IEnumerable<McpServerTool> Tools(DesignTools target)
    {
        foreach (var method in typeof(DesignTools).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            if (method.GetCustomAttribute<McpServerToolAttribute>() != null)
                yield return McpServerTool.Create(method, target);
    }

    /// <summary>The saved recipes (<see cref="Recipes"/>) as MCP prompts.</summary>
    public static IEnumerable<McpServerPrompt> Prompts() =>
        typeof(Recipes).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<McpServerPromptAttribute>() != null)
            .Select(m => McpServerPrompt.Create(m, target: null));

    /// <summary>The tools' MCP definitions (names, descriptions, input schemas), without a host to run them.</summary>
    public static IList<Tool> Definitions() => [.. _definitions.Value];

    private static readonly Lazy<Tool[]> _definitions = new(() => Tools(new NoHost()).Select(t => t.ProtocolTool).ToArray());

    public static Implementation Info => new()
    {
        Name = Name,
        Title = "AVAUIDesigner",
        Version = typeof(DesignServer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "1.0",
    };

    public static McpServerOptions Options(IDesignHost host) => Configure(new McpServerOptions(), host);

    /// <summary>Fills a new session's options (ones the network door's HTTP handler made) as <see cref="Options"/> makes them.</summary>
    public static McpServerOptions Configure(McpServerOptions options, IDesignHost host)
    {
        options.ServerInfo = Info;
        options.ServerInstructions = Instructions;
        options.ToolCollection = [];
        var target = new DesignTools(host);
        options.Filters.Request.CallToolFilters.Add(next => async (request, cancel) =>
        {
            var tool = request.Params is { } p && options.ToolCollection.TryGetPrimitive(p.Name, out var t) ? t.ProtocolTool : null;
            if (tool != null && Arguments.Check(tool, request.Params!.Arguments) is { } problem) return Arguments.Error(problem);
            try
            {
                var result = await next(request, cancel).ConfigureAwait(false);
                // The session's changes, as steps save_steps can keep: every call that worked and may change something.
                if (tool != null && result.IsError != true && tool.Annotations?.ReadOnlyHint != true)
                    target.Record(tool.Name, request.Params!.Arguments);
                return result;
            }
            catch (Exception e) when (e is not McpException and not OperationCanceledException)
            {
                // The SDK couldn't bind the arguments to the handler (a value of the wrong kind): say which, not just that it failed.
                return Arguments.Error(tool != null ? $"{tool.Name}: {e.Message} Check the arguments against the tool's input schema." : e.Message);
            }
        });
        foreach (var tool in Tools(target)) options.ToolCollection.Add(tool);
        options.PromptCollection = [];
        foreach (var prompt in Prompts()) options.PromptCollection.Add(prompt);
        return options;
    }

    /// <summary>Serves one MCP session over a transport until the client goes away or <paramref name="cancel"/> fires.</summary>
    public static async Task RunAsync(ITransport transport, IDesignHost host, CancellationToken cancel = default)
    {
        await using var server = McpServer.Create(transport, Options(host));
        await server.RunAsync(cancel).ConfigureAwait(false);
    }

    /// <summary>Checks a call's arguments against its tool's input schema before the SDK binds them.</summary>
    internal static class Arguments
    {
        public static CallToolResult Error(string message) => new() { IsError = true, Content = [new TextContentBlock { Text = message }] };

        /// <summary>What's wrong with the arguments (a name the tool doesn't take, a value of the wrong kind), or null.</summary>
        public static string? Check(Tool tool, IDictionary<string, JsonElement>? args)
        {
            if (args == null || !tool.InputSchema.TryGetProperty("properties", out var props)) return null;
            foreach (var (name, value) in args)
            {
                if (!props.TryGetProperty(name, out var schema))
                {
                    var names = props.EnumerateObject().Select(x => x.Name).ToList();
                    return $"{tool.Name} has no argument \"{name}\". It takes: {(names.Count == 0 ? "none" : string.Join(", ", names))}.";
                }
                if (Kinds(schema) is { Count: > 0 } kinds && !kinds.Any(k => Fits(k, value)))
                    return $"{tool.Name}: {name} must be {string.Join(" or ", kinds.Where(k => k != "null").Select(Describe))}, not {Describe(value)}.";
            }
            return null;
        }

        private static List<string> Kinds(JsonElement schema) =>
            schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("type", out var type) ? []
            : type.ValueKind == JsonValueKind.String ? [type.GetString()!]
            : type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];

        private static bool Fits(string kind, JsonElement v) => kind switch
        {
            "string" => v.ValueKind == JsonValueKind.String,
            "number" => v.ValueKind == JsonValueKind.Number,
            "integer" => v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out _),
            "boolean" => v.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "array" => v.ValueKind == JsonValueKind.Array,
            "object" => v.ValueKind == JsonValueKind.Object,
            "null" => v.ValueKind == JsonValueKind.Null,
            _ => true,
        };

        private static string Describe(string kind) => kind switch
        {
            "string" => "text", "number" => "a number", "integer" => "a whole number", "boolean" => "true or false",
            "array" => "a list", "object" => "an object", _ => kind,
        };

        private static string Describe(JsonElement v) => v.ValueKind switch
        {
            JsonValueKind.String => $"the text \"{v.GetString()}\"",
            JsonValueKind.Number => $"the number {v.GetRawText()}",
            JsonValueKind.True or JsonValueKind.False => v.GetRawText(),
            JsonValueKind.Array => "a list",
            JsonValueKind.Object => "an object",
            _ => "null",
        };
    }

    /// <summary>A host for reading the tools' definitions only.</summary>
    private sealed class NoHost : IDesignHost
    {
        public string Root => Path.GetTempPath();
        public IDesignLibrary Library => throw new InvalidOperationException("Not connected to a document.");
        public ConnectionStore Connections { get; } = new(null);
        public Task<T> RunAsync<T>(string action, Func<IDesignSession, Task<T>> work, CancellationToken cancel = default) =>
            throw new InvalidOperationException("Not connected to a document.");
    }
}
