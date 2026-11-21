using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AVAUIDesigner.Data;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Serialization;
using ModelContextProtocol.Server;

namespace AVAUIDesigner.Mcp;

/// <summary>A field of an entity described by hand.</summary>
public sealed class FieldSpec
{
    [JsonPropertyName("name"), Description("The field's name (\"FirstName\", \"date_of_birth\").")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type"), Description("Its C# type: string (the default), int, long, decimal, double, bool, DateTime, DateOnly, TimeOnly, Guid.")]
    public string? Type { get; set; }

    [JsonPropertyName("label"), Description("The label people see. Default: the name in words.")]
    public string? Label { get; set; }

    [JsonPropertyName("required"), Description("Must be filled in. Default false.")]
    public bool? Required { get; set; }

    [JsonPropertyName("nullable"), Description("May be left empty in the database. Default: not required.")]
    public bool? Nullable { get; set; }

    [JsonPropertyName("is_key"), Description("Part of the primary key. Default: a field named Id or {Entity}Id.")]
    public bool? IsKey { get; set; }

    [JsonPropertyName("generated"), Description("The database fills it in (identity, sequence). Default: true for a whole-number key.")]
    public bool? Generated { get; set; }

    [JsonPropertyName("max_length"), Description("Longest text allowed. Optional.")]
    public int? MaxLength { get; set; }

    [JsonPropertyName("min"), Description("Smallest number allowed. Optional.")]
    public double? Min { get; set; }

    [JsonPropertyName("max"), Description("Largest number allowed. Optional.")]
    public double? Max { get; set; }

    [JsonPropertyName("options"), Description("A fixed list of choices (a drop-down). Optional.")]
    public List<string>? Options { get; set; }

    [JsonPropertyName("editor"), Description("How it is edited, if not the type's usual way: text, multiline, email, phone, password, date, time, datetime, number, decimal, money, check, choice, readonly.")]
    public string? Editor { get; set; }

    [JsonPropertyName("column"), Description("Its column in the table, if not its name.")]
    public string? Column { get; set; }

    [JsonPropertyName("references"), Description("The table it refers to (a foreign key). Optional.")]
    public string? References { get; set; }
}

/// <summary>An entity (a data structure a screen edits) described by hand.</summary>
public sealed class EntitySpec
{
    [JsonPropertyName("name"), Description("The entity's name, singular (\"Patient\", \"Medication\").")]
    public string Name { get; set; } = "";

    [JsonPropertyName("table"), Description("Its table. Default: the name.")]
    public string? Table { get; set; }

    [JsonPropertyName("schema"), Description("The table's schema (\"dbo\", \"public\"). Optional.")]
    public string? Schema { get; set; }

    [JsonPropertyName("label"), Description("What people call one (\"Patient\"). Optional.")]
    public string? Label { get; set; }

    [JsonPropertyName("fields"), Description("Its fields, in the order a form shows them.")]
    public List<FieldSpec> Fields { get; set; } = [];

    public EntityModel ToModel() => new EntityModel
    {
        Name = Name,
        Table = Table,
        Schema = Schema,
        Label = Label,
        Source = "described",
        Fields = Fields.Select(f => new FieldModel
        {
            Name = f.Name,
            Type = f.Type ?? "string",
            Label = f.Label,
            Required = f.Required ?? false,
            Nullable = f.Nullable ?? !(f.Required ?? false),
            IsKey = f.IsKey ?? false,
            IsGenerated = f.Generated ?? false,
            MaxLength = f.MaxLength,
            Min = f.Min,
            Max = f.Max,
            Options = f.Options,
            Editor = f.Editor,
            Column = f.Column,
            References = f.References,
        }).ToList(),
    }.Normalise();
}

/// <summary>An assessment described by hand (add_assessment): its rating scale, sections of items and scoring.</summary>
public sealed class AssessmentSpec
{
    [JsonPropertyName("key"), Description("A short name for its controls and view-model members (\"Audit\", \"FallRisk\").")]
    public string Key { get; set; } = "";

    [JsonPropertyName("title"), Description("Its title.")]
    public string Title { get; set; } = "";

    [JsonPropertyName("instructions"), Description("What the person filling it in is told. Optional.")]
    public string? Instructions { get; set; }

    [JsonPropertyName("scale"), Description("The rating points, low to high, each \"value: label\" (\"0: Never\", \"1: Sometimes\"…).")]
    public List<string> Scale { get; set; } = [];

    [JsonPropertyName("sections"), Description("The sections, each with a title and its items.")]
    public List<AssessmentSectionSpec> Sections { get; set; } = [];

    [JsonPropertyName("scoring"), Description("sum (a total read against bands; the default) or action (a count of items rated at action_at or above).")]
    public string? Scoring { get; set; }

    [JsonPropertyName("bands"), Description("For sum scoring: what totals mean, each \"min-max: label\" (\"0-4: Minimal\").")]
    public List<string>? Bands { get; set; }

    [JsonPropertyName("action_at"), Description("For action scoring: the rating from which an item needs action. Default 2.")]
    public int? ActionAt { get; set; }

    [JsonPropertyName("source"), Description("Where the instrument comes from and its terms of use. Optional.")]
    public string? Source { get; set; }

    public AssessmentDefinition ToDefinition()
    {
        static ScalePoint Point(string text)
        {
            var at = text.IndexOf(':');
            if (at < 1 || !int.TryParse(text[..at].Trim(), out var v)) throw new ArgumentException($"\"{text}\" isn't a scale point: write \"value: label\", such as \"0: Never\".");
            return new ScalePoint(v, text[(at + 1)..].Trim());
        }
        static ScoreBand Band(string text)
        {
            var m = System.Text.RegularExpressions.Regex.Match(text, @"^\s*(\d+)\s*[-–]\s*(\d+)\s*:\s*(.+)$");
            if (!m.Success) throw new ArgumentException($"\"{text}\" isn't a band: write \"min-max: label\", such as \"0-4: Minimal\".");
            return new ScoreBand(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Value.Trim());
        }
        return new AssessmentDefinition
        {
            Key = Key,
            Title = Title,
            Instructions = Instructions,
            Scale = Scale.Select(Point).ToList(),
            Sections = Sections.Select(s => new AssessmentSection
            {
                Title = s.Title ?? "",
                Items = s.Items,
                Strengths = s.Strengths ?? false,
                Scale = s.Scale is { Count: > 0 } sc ? sc.Select(Point).ToList() : null,
            }).ToList(),
            Scoring = (Scoring ?? "sum").Trim().ToLowerInvariant(),
            Bands = (Bands ?? []).Select(Band).ToList(),
            ActionAt = ActionAt ?? 2,
            Source = Source,
        }.Normalise();
    }
}

public sealed class AssessmentSectionSpec
{
    [JsonPropertyName("title"), Description("The section's title. Optional for a one-section assessment.")]
    public string? Title { get; set; }

    [JsonPropertyName("items"), Description("Its items, in order.")]
    public List<string> Items { get; set; } = [];

    [JsonPropertyName("strengths"), Description("A strengths section: a low rating is the good one. Default false.")]
    public bool? Strengths { get; set; }

    [JsonPropertyName("scale"), Description("This section's own scale, if not the assessment's (\"value: label\" each).")]
    public List<string>? Scale { get; set; }
}

/// <summary>Screens from data: entities, databases, forms, CRUD screens and assessments.</summary>
public sealed partial class DesignTools
{
    private const string EntityHelp = "An entity described by hand. Or give file (and class_name), or connection and table.";
    private const string FileHelp = "A C# (.cs) or SQL (.sql) file in the working folder to read the entity from.";
    private const string ClassHelp = "Which class or table in the file, if it has several.";
    private const string ConnectionHelp = "A database connection's name (list_connections), with table.";
    private const string TableHelp = "The table or view, with connection (\"patients\", \"dbo.Patient\").";

    // ───────────────────────────── entities ─────────────────────────────

    [McpServerTool(Name = "read_entities", ReadOnly = true, Idempotent = true)]
    [Description("Reads the data structures in a C# file (classes and records, with their [Key], [Required], [MaxLength], [Range], [Display] and [Column] attributes) or a SQL script (its CREATE TABLE statements): each entity's fields, types, keys and limits, and the editor a form would use for each. For another language, read_file it and describe the entity yourself.")]
    public async Task<string> ReadEntities(
        [Description("The file, relative to the working folder.")] string path,
        CancellationToken cancel = default)
    {
        var list = await Task.Run(() => ReadEntitiesFrom(path), cancel).ConfigureAwait(false);
        return new JsonObject { ["entities"] = new JsonArray(list.Select(e => (JsonNode)Describe(e)).ToArray()) }.ToJsonString(Json.Options);
    }

    private List<EntityModel> ReadEntitiesFrom(string path)
    {
        try
        {
            var full = _files.Resolve(path);
            if (!File.Exists(full)) throw new FileNotFoundException($"There's no {path} in the working folder. list_files shows what is there.");
            var list = SourceReaders.Read(Relative(full), File.ReadAllText(full));
            if (list.Count == 0) throw new InvalidDataException($"{path} has no classes, records or CREATE TABLE statements with fields a form could edit.");
            return list;
        }
        catch (Exception e) when (e is not ModelContextProtocol.McpException)
        {
            throw new ModelContextProtocol.McpException(Explain(e), e);
        }
    }

    [McpServerTool(Name = "list_connections", ReadOnly = true, Idempotent = true)]
    [Description("The database connections set up for the designer: each one's name, engine and server (never its password). The person adds them in AI › Data Connections.")]
    public Task<string> ListConnections(CancellationToken cancel = default) =>
        Task.FromResult(new JsonObject
        {
            ["connections"] = new JsonArray(host.Connections.All.Select(c => (JsonNode)new JsonObject
            {
                ["name"] = c.Name,
                ["engine"] = SqlDialect.For(c.Engine).DisplayName,
                ["connection"] = c.Redacted,
                ["note"] = c.Note,
            }).ToArray()),
            ["engines"] = "sqlserver, postgresql, mysql (or mariadb), oracle, sqlite, db2 (through ODBC), odbc",
        }.ToJsonString(Json.Options));

    [McpServerTool(Name = "add_connection")]
    [Description("Adds a database connection under a name, for this session (or kept, with save), after checking it connects. Prefer asking the person to add it in AI › Data Connections: a connection string given here passes through this conversation. The designer only ever reads a database's structure, never its rows.")]
    public async Task<string> AddConnection(
        [Description("A name for it (\"clinic\").")] string name,
        [Description("The engine: sqlserver, postgresql, mysql, mariadb, oracle, sqlite, db2 or odbc.")] string engine,
        [Description("The ADO.NET connection string (for db2 and odbc, an ODBC one: \"Driver={IBM DB2 ODBC DRIVER};Database=…\").")] string connection_string,
        [Description("Keep it for next time. Default false.")] bool save = false,
        CancellationToken cancel = default)
    {
        try
        {
            var c = new DataConnection { Name = name, Provider = engine, ConnectionString = connection_string };
            var version = await Task.Run(() => SchemaReader.Test(c.Engine, connection_string), cancel).ConfigureAwait(false);
            host.Connections.Put(c, save);
            return new JsonObject { ["connection"] = c.Name, ["connected_to"] = version, ["kept"] = save }.ToJsonString(Json.Options);
        }
        catch (Exception e) when (e is not ModelContextProtocol.McpException and not OperationCanceledException)
        {
            throw new ModelContextProtocol.McpException("Couldn't connect: " + Explain(e) + " Nothing was added.", e);
        }
    }

    [McpServerTool(Name = "list_tables", ReadOnly = true, Idempotent = true)]
    [Description("The tables and views in a connected database (from its catalogue: no rows are read).")]
    public Task<string> ListTables(
        [Description("The connection's name (list_connections).")] string connection,
        [Description("Only this schema or owner. Default: the usual one.")] string? schema = null,
        [Description("Only names containing this text. Optional.")] string? filter = null,
        CancellationToken cancel = default) =>
        OnDatabase(connection, c =>
        {
            var tables = SchemaReader.ListTables(c.Engine, c.ConnectionString, schema)
                .Where(t => filter is null || t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            return new JsonObject
            {
                ["connection"] = c.Name,
                ["tables"] = new JsonArray(tables.Take(500).Select(t => (JsonNode)(t.FullName + (t.IsView ? " (view)" : ""))).ToArray()),
                ["more"] = tables.Count > 500 ? $"{tables.Count} in all: narrow with schema or filter." : null,
            };
        }, cancel);

    [McpServerTool(Name = "describe_table", ReadOnly = true, Idempotent = true)]
    [Description("A table's structure from the database's catalogue, as an entity: its columns, types, lengths, which may be empty, the key (and whether the database generates it), and foreign keys with what a lookup list would show. No rows are read.")]
    public Task<string> DescribeTable(
        [Description("The connection's name.")] string connection,
        [Description("The table or view (\"patients\", \"dbo.Patient\").")] string table,
        CancellationToken cancel = default) =>
        OnDatabase(connection, c => Describe(SchemaReader.Describe(c.Engine, c.ConnectionString, table, $"{c.Name}:{table}")), cancel);

    private async Task<string> OnDatabase(string name, Func<DataConnection, JsonObject> work, CancellationToken cancel)
    {
        try
        {
            var c = host.Connections.Get(name);
            return (await Task.Run(() => work(c), cancel).ConfigureAwait(false)).ToJsonString(Json.Options);
        }
        catch (Exception e) when (e is not ModelContextProtocol.McpException and not OperationCanceledException)
        {
            throw new ModelContextProtocol.McpException(Explain(e), e);
        }
    }

    private static JsonObject Describe(EntityModel e) => new()
    {
        ["name"] = e.Name,
        ["table"] = e.Schema is null ? e.TableName : $"{e.Schema}.{e.TableName}",
        ["namespace"] = e.Namespace,
        ["source"] = e.Source,
        ["fields"] = new JsonArray(e.Fields.Select(f => (JsonNode)new JsonObject
        {
            ["name"] = f.Name,
            ["column"] = f.ColumnName != f.Name ? f.ColumnName : null,
            ["type"] = f.CSharpType,
            ["key"] = f.IsKey ? true : null,
            ["generated"] = f.IsGenerated ? true : null,
            ["computed"] = f.IsComputed ? true : null,
            ["required"] = f.Required ? true : null,
            ["max_length"] = f.MaxLength,
            ["min"] = f.Min,
            ["max"] = f.Max,
            ["options"] = f.Options is { } o ? new JsonArray(o.Select(x => (JsonNode)x).ToArray()) : null,
            ["references"] = f.References is { } r ? (f.ReferencesKey is { } k ? $"{r}.{k} (shows {f.ReferencesDisplay})" : r) : null,
            ["editor"] = f.EditorKind,
        }).ToArray()),
    };

    /// <summary>The entity a call names: described by hand, read from a file, or read from a database (off the UI thread).</summary>
    private async Task<EntityModel> ResolveEntity(EntitySpec? entity, string? file, string? className, string? connection, string? table, CancellationToken cancel)
    {
        try
        {
            var given = (entity != null ? 1 : 0) + (file != null ? 1 : 0) + (connection != null || table != null ? 1 : 0);
            if (given != 1) throw new ArgumentException("Say which entity: describe it (entity), or give file (with class_name if it has several), or connection and table.");
            if (entity != null) return entity.ToModel();
            if (file != null)
            {
                var list = await Task.Run(() => ReadEntitiesFrom(file), cancel).ConfigureAwait(false);
                if (className is null && list.Count > 1)
                    throw new ArgumentException($"{file} has {list.Count} entities: give class_name, one of {string.Join(", ", list.Select(e => e.Name))}.");
                return className is null ? list[0]
                    : list.FirstOrDefault(e => e.Name.Equals(className, StringComparison.OrdinalIgnoreCase) || e.TableName.Equals(className, StringComparison.OrdinalIgnoreCase))
                      ?? throw new ArgumentException($"{file} has no {className}. It has {string.Join(", ", list.Select(e => e.Name))}.");
            }
            if (connection is null || table is null) throw new ArgumentException("Give both connection and table.");
            var c = host.Connections.Get(connection);
            return await Task.Run(() => SchemaReader.Describe(c.Engine, c.ConnectionString, table, $"{c.Name}:{table}"), cancel).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not ModelContextProtocol.McpException and not OperationCanceledException)
        {
            throw new ModelContextProtocol.McpException(Explain(e), e);
        }
    }

    // ───────────────────────────── forms ─────────────────────────────

    [McpServerTool(Name = "add_form")]
    [Description("Adds a form for an entity's fields into a parent: a label and the right editor for each field (text, multi-line notes, date and time pickers, numbers, money, check boxes, drop-downs of choices), required ones marked *, in one or two columns. In an MVVM view it binds each editor to the view model's {Entity} property and, when saved, writes the model class (unless it came from your code) and that property.")]
    public async Task<string> AddForm(
        [Description("The parent element: its name or path, or \"root\".")] string parent,
        [Description(EntityHelp)] EntitySpec? entity = null,
        [Description(FileHelp)] string? file = null,
        [Description(ClassHelp)] string? class_name = null,
        [Description(ConnectionHelp)] string? connection = null,
        [Description(TableHelp)] string? table = null,
        [Description("Only these fields, in this order (by name). Default: every field a person edits.")] List<string>? fields = null,
        [Description("1 or 2 fields across. Default 1.")] int columns = 1,
        [Description("A heading above the form. Optional.")] string? title = null,
        [Description("Bind the editors (MVVM views). Default: when the view is MVVM.")] bool? bind = null,
        CancellationToken cancel = default)
    {
        var e = await ResolveEntity(entity, file, class_name, connection, table, cancel).ConfigureAwait(false);
        return await Edit("added a form", (s, scratch) =>
        {
            var chosen = Pick(e, fields);
            var binding = bind ?? scratch.CodeStyle == CodeBehindStyle.MVVM;
            if (binding && scratch.CodeStyle != CodeBehindStyle.MVVM) MakeMvvm(scratch);
            var existing = e.Namespace != null;
            var formName = Identifiers.Camel(e.Name).TrimStart('@') + "Form";
            var xaml = FormXaml.Form(chosen, new FormOptions
            {
                BindingPrefix = e.Name + ".",
                Bind = binding,
                Columns = columns,
                Name = scratch.NamedElements.ContainsKey(formName) ? null : formName,
                OptionsFromViewModel = binding,
                ConvertersClass = binding && existing ? "vm:Converters" : null,
            });
            if (!string.IsNullOrWhiteSpace(title))
                xaml = $"<StackPanel Spacing=\"6\">\n<TextBlock Text=\"{FormXaml.Escape(title)}\" FontSize=\"16\" FontWeight=\"SemiBold\" />\n{xaml}\n</StackPanel>";
            var added = s.Parser.ParseFragment(xaml, scratch, s.Registry);
            Elements.Insert(Elements.Find(scratch, parent), added, null, false, s.Registry);
            foreach (var a in added) Elements.NameAll(scratch, a);
            var result = Added(s, added);
            if (binding)
            {
                var ns = scratch.ClassNamespace ?? "MyApp.Views";
                var names = CrudNames.For(e, ns, modelNamespace: e.Namespace);
                var members = $"    /// <summary>The {FormXaml.Escape(e.DisplayLabel.ToLowerInvariant())} the form edits.</summary>\n    [ObservableProperty]\n    private {names.ModelClass} _{Identifiers.Camel(e.Name).TrimStart('@')} = new();\n\n" +
                              string.Concat(chosen.Where(f => f.EditorKind == "choice" && f.Options is { Count: > 0 }).Select(f => CrudGenerator.OptionsMember(f, existing)));
                var files = Companions(s, scratch, members, [names.ModelNamespace], needConverters: existing);
                if (!existing) files.Add(Pend(s, ModelPath(s, scratch, names), CrudGenerator.Model(e, names)));
                result["code"] = new JsonArray(files.Select(f => (JsonNode)f).ToArray());
            }
            return result;
        }, cancel).ConfigureAwait(false);
    }

    private static List<FieldModel> Pick(EntityModel e, List<string>? names)
    {
        if (names is not { Count: > 0 }) return e.Fields.Where(f => !f.IsComputed && !(f.IsKey && f.IsGenerated) && f.Type != "byte[]").ToList();
        return names.Select(n => e.Fields.FirstOrDefault(f => f.Name.Equals(n, StringComparison.OrdinalIgnoreCase) || f.ColumnName.Equals(n, StringComparison.OrdinalIgnoreCase))
                                  ?? throw new ArgumentException($"{e.Name} has no field {n}. Its fields: {string.Join(", ", e.Fields.Select(f => f.Name))}.")).ToList();
    }

    // ───────────────────────────── CRUD ─────────────────────────────

    [McpServerTool(Name = "scaffold_crud")]
    [Description("Makes a whole CRUD screen for an entity as a new MVVM view (replacing the open design): list_detail (a searchable list beside an edit form: the usual), grid (an editable grid) or form (one record). Its view model loads, searches, adds, saves (checking required fields, lengths and ranges first) and deletes (asking twice); saving the design also writes the model class (unless it came from your code) and a repository with the database's own parameterised SQL, including lookup lists for foreign keys. Fails if the open design has unsaved changes, unless discard_changes is true.")]
    public async Task<string> ScaffoldCrud(
        [Description(EntityHelp)] EntitySpec? entity = null,
        [Description(FileHelp)] string? file = null,
        [Description(ClassHelp)] string? class_name = null,
        [Description(ConnectionHelp)] string? connection = null,
        [Description(TableHelp)] string? table = null,
        [Description("list_detail (the default), grid or form.")] string layout = "list_detail",
        [Description("The SQL the repository is written in: sqlserver, postgresql, mysql, oracle, sqlite, db2 or odbc. Default: the connection's engine, else sqlserver.")] string? engine = null,
        [Description("1 or 2 fields across in the form. Default 2.")] int form_columns = 2,
        [Description("The view's class name. Default: the entity's plural (\"Patients\").")] string? view_name = null,
        [Description("The views' namespace. Default \"MyApp.Views\".")] string? @namespace = null,
        [Description("The folder for the view, relative to the working folder. Default \"Views\" (view models go in ViewModels beside it, models in Models, repositories in Data).")] string folder = "Views",
        [Description("Window or UserControl (the default).")] string kind = "UserControl",
        [Description("Throw away unsaved changes to the open design. Default false.")] bool discard_changes = false,
        CancellationToken cancel = default)
    {
        var e = await ResolveEntity(entity, file, class_name, connection, table, cancel).ConfigureAwait(false);
        var provider = engine != null ? SqlDialect.Parse(engine)
            : connection != null ? host.Connections.Get(connection).Engine : DbProvider.SqlServer;
        return await Read("scaffolded a CRUD screen", s =>
        {
            Discardable(s, discard_changes);
            if (!CrudGenerator.Layouts.Contains(layout)) throw new ArgumentException($"layout is one of {string.Join(", ", CrudGenerator.Layouts)}.");
            if (!e.Keys.Any()) throw new ArgumentException($"{e.Name} has no key, so a saved row can't be found again: mark its key field (is_key).");
            var space = string.IsNullOrWhiteSpace(@namespace) ? "MyApp.Views" : @namespace.Trim();
            var existing = e.Namespace != null;
            var names = CrudNames.For(e, space, view_name, e.Namespace);
            var doc = NewDesign(s, names.ViewClass, kind, space, true, folder, 1200, 720, Words.FromIdentifier(e.Plural));
            var content = s.Parser.ParseFragment(CrudGenerator.ViewXaml(e, layout, form_columns, existing), doc, s.Registry);
            Elements.Insert(doc.RootElement, content, null, true, s.Registry);
            foreach (var c in content) Elements.NameAll(doc, c);
            s.Pending.Clear();
            s.Replace(doc, true);
            var files = new List<string>
            {
                Pend(s, s.ViewModelPath(doc.FilePath), CrudGenerator.ViewModel(e, names, existing)),
                Pend(s, Path.Combine(DataFolder(s, doc), names.RepositoryClass + ".cs"), CrudGenerator.Repository(e, names, provider, existing)),
            };
            var lookup = Path.Combine(DataFolder(s, doc), CrudGenerator.LookupFileName);
            if (!File.Exists(lookup)) files.Add(Pend(s, lookup, CrudGenerator.LookupItem(names)));
            if (!existing) files.Add(Pend(s, ModelPath(s, doc, names), CrudGenerator.Model(e, names)));
            else files.Add(Pend(s, Path.Combine(Path.GetDirectoryName(s.ViewModelPath(doc.FilePath))!, ConvertersCode.FileName), ConvertersCode.Source(names.ViewModelNamespace)));
            s.Refresh();
            var o = Summary(s);
            o["entity"] = e.Name;
            o["engine"] = SqlDialect.For(provider).DisplayName;
            o["layout"] = layout;
            o["code"] = new JsonArray(files.Select(f => (JsonNode)f).ToArray());
            o["packages"] = $"The app needs CommunityToolkit.Mvvm, Avalonia.Controls.DataGrid and {SqlDialect.For(provider).Package}.";
            o["wiring"] = $"At run time give the view its view model: new {names.ViewModelClass}(new {names.RepositoryClass}(connectionString)).";
            o["next"] = "render_view to look, then save_document to write the view and its code.";
            return o;
        }, cancel).ConfigureAwait(false);
    }

    // ───────────────────────────── assessments ─────────────────────────────

    [McpServerTool(Name = "add_assessment")]
    [Description("Adds a rated assessment into a parent: its instructions, a grid of items with a radio button per rating, and the result (a total and what it means, or the needs rated for action), with any alert (PHQ-9 item 9). Built in: phq9, gad7, cans (a CANS core set: check it against your program's version). Or describe any other instrument (scale, sections, items, scoring). In an MVVM view, saving writes the view-model members that score it.")]
    public Task<string> AddAssessment(
        [Description("The parent element: its name or path, or \"root\".")] string parent,
        [Description("A built-in instrument: phq9, gad7 or cans. Or describe one with definition.")] string? instrument = null,
        [Description("An instrument described by hand, instead of a built-in one.")] AssessmentSpec? definition = null,
        [Description("Bind the ratings to the view model and score them (MVVM; the view is made MVVM if it isn't). Default true.")] bool bind = true,
        CancellationToken cancel = default) =>
        Edit("added an assessment", (s, scratch) =>
        {
            if ((instrument is null) == (definition is null)) throw new ArgumentException("Give instrument (phq9, gad7, cans) or definition, one of the two.");
            var a = definition?.ToDefinition() ?? Assessments.Named(instrument!);
            if (bind && scratch.CodeStyle != CodeBehindStyle.MVVM) MakeMvvm(scratch);
            if (scratch.NamedElements.ContainsKey(Identifiers.Camel(a.Key).TrimStart('@') + "Assessment"))
                throw new ArgumentException($"The design already has the {a.Title}: give the new one another key.");
            var added = s.Parser.ParseFragment(Assessments.Xaml(a, bind), scratch, s.Registry);
            Elements.Insert(Elements.Find(scratch, parent), added, null, false, s.Registry);
            foreach (var x in added) Elements.NameAll(scratch, x);
            var result = Added(s, added);
            result["items"] = a.ItemCount;
            result["scoring"] = a.Scoring == "sum" ? $"total, read as {string.Join(", ", a.Bands.Select(b => $"{b.Min}-{b.Max} {b.Label}"))}" : $"items rated {a.ActionAt} or more need action";
            if (bind)
                result["code"] = new JsonArray(Companions(s, scratch, Assessments.Members(a), [], needConverters: true).Select(f => (JsonNode)f).ToArray());
            result["names"] = new JsonArray((JsonNode)(Identifiers.Camel(a.Key).TrimStart('@') + "Assessment"));
            return result;
        }, cancel);

    // ───────────────────────────── generated code ─────────────────────────────

    private static void MakeMvvm(DesignDocument doc)
    {
        var options = DesignFiles.Options(doc);
        MvvmScaffolder.Apply(doc, options.EffectiveViewModelNamespace, doc.ClassName + "ViewModel");
    }

    /// <summary>
    /// Adds members to the view's view model (the one waiting to be saved, the file, or a new class) and, if wanted,
    /// the converters file beside it. Returns the files, relative, for the answer.
    /// </summary>
    private List<string> Companions(IDesignSession s, DesignDocument doc, string members, IEnumerable<string> usings, bool needConverters)
    {
        if (doc.FilePath.Length == 0)
        {
            doc.FilePath = Path.Combine(_files.Resolve("Views"), doc.ClassName + ".axaml");
            s.Document.FilePath = doc.FilePath;
        }
        var options = DesignFiles.Options(doc);
        var vmPath = s.ViewModelPath(doc.FilePath);
        var cls = doc.ClassName + "ViewModel";
        var source = s.Pending.TryGetValue(vmPath, out var pending) ? pending : DesignFiles.Read(vmPath)
            ?? Assessments.ViewModelClass(options.EffectiveViewModelNamespace, cls, "");
        var files = new List<string> { Pend(s, vmPath, CodeMerge.AddMembers(source, cls, members, ["CommunityToolkit.Mvvm.ComponentModel", .. usings])) };
        var converters = Path.Combine(Path.GetDirectoryName(vmPath)!, ConvertersCode.FileName);
        if (needConverters && !File.Exists(converters) && !s.Pending.ContainsKey(converters))
            files.Add(Pend(s, converters, ConvertersCode.Source(options.EffectiveViewModelNamespace)));
        return files;
    }

    private string Pend(IDesignSession s, string path, string code)
    {
        _files.Resolve(Relative(path));
        s.Pending[path] = code;
        return Relative(path);
    }

    /// <summary>The app's folder: the one holding Views (or the view's own folder).</summary>
    private static string AppFolder(DesignDocument doc)
    {
        var dir = Path.GetDirectoryName(doc.FilePath)!;
        return Path.GetFileName(dir).Equals("Views", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(dir)! : dir;
    }

    private static string ModelPath(IDesignSession s, DesignDocument doc, CrudNames n) => Path.Combine(AppFolder(doc), "Models", n.ModelClass + ".cs");

    private static string DataFolder(IDesignSession s, DesignDocument doc) => Path.Combine(AppFolder(doc), "Data");
}
