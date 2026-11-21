using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Serialization;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AVAUIDesigner.Mcp;

/// <summary>A control to add (add_controls): its type, name, properties and children.</summary>
public sealed class ControlSpec
{
    [JsonPropertyName("type"), Description("The control's type, as in XAML: \"TextBox\", \"Button\", \"Grid\", \"StackPanel\", \"DataGrid\"… (list_controls).")]
    public string Type { get; set; } = "";

    [JsonPropertyName("name"), Description("Its x:Name (\"firstNameBox\"). Optional: one is made up if missing.")]
    public string? Name { get; set; }

    [JsonPropertyName("text"), Description("Its text: Text for a TextBlock or TextBox, Header for an Expander or TabItem, Content for a Button, CheckBox or Label. Optional.")]
    public string? Text { get; set; }

    [JsonPropertyName("properties"), Description("Property values as XAML text, attached ones with their owner: {\"Margin\": \"8\", \"Grid.Row\": \"1\", \"Text\": \"{Binding Name, Mode=TwoWay}\"}.")]
    public Dictionary<string, string>? Properties { get; set; }

    [JsonPropertyName("events"), Description("Events and their handler methods in the code-behind: {\"Click\": \"SaveButton_Click\"}.")]
    public Dictionary<string, string>? Events { get; set; }

    [JsonPropertyName("children"), Description("Controls inside it (for panels and containers), in order.")]
    public List<ControlSpec>? Children { get; set; }
}

/// <summary>
/// The MCP tools: what an agent can do with AVAUIDesigner. Each handler reaches the design only through
/// <see cref="IDesignHost.RunAsync{T}"/>, so the same code serves headless and live. Every change is one undo step,
/// a call that fails leaves the design as it was, and failures come back as MCP tool errors with a message that says
/// what to do instead.
/// </summary>
/// <remarks>The parameter names are the JSON names agents see, hence snake_case.</remarks>
public sealed partial class DesignTools(IDesignHost host)
{
    private readonly Workspace _files = new(host.Root);

    private const string TargetHelp = "The element: its x:Name, its path from get_document (\"0/2/1\"), or \"root\".";

    // ───────────────────────────── the design ─────────────────────────────

    [McpServerTool(Name = "get_document", ReadOnly = true, Idempotent = true)]
    [Description("The open design: its file, class, kind (Window or UserControl), whether it is MVVM and has unsaved changes, and its element tree (each element's type, x:Name or path, text and properties). Start here.")]
    public Task<string> GetDocument(
        [Description("Only the part under this element (its name or path). Default: the whole design.")] string? target = null,
        [Description("How many levels deep to show. Default 12.")] int depth = 12,
        CancellationToken cancel = default) =>
        Read("read the design", s =>
        {
            var d = s.Document;
            var start = target is null ? d.RootElement : Elements.Find(d, target);
            return new JsonObject
            {
                ["file"] = d.FilePath.Length > 0 ? Relative(d.FilePath) : null,
                ["class"] = d.XClass,
                ["kind"] = d.RootElementType,
                ["mvvm"] = d.CodeStyle == CodeBehindStyle.MVVM,
                ["view_model"] = d.CodeStyle == CodeBehindStyle.MVVM ? $"{s.Options.EffectiveViewModelNamespace}.{d.ClassName}ViewModel" : null,
                ["changed"] = d.IsDirty,
                ["elements"] = d.AllElements.Count(),
                ["pending_files"] = s.Pending.Count > 0 ? new JsonArray(s.Pending.Keys.Select(k => (JsonNode)Relative(k)).ToArray()) : null,
                ["tree"] = Elements.Describe(start, Math.Clamp(depth, 1, 40)),
            };
        }, cancel);

    [McpServerTool(Name = "get_xaml", ReadOnly = true, Idempotent = true)]
    [Description("The design's XAML as it would be saved, or one element's.")]
    public Task<string> GetXaml(
        [Description("Only this element (its name or path). Default: the whole design.")] string? target = null,
        CancellationToken cancel = default) =>
        Run("read the XAML", s =>
            target is null ? s.Serializer.Serialize(s.Document) : s.Serializer.SerializeElement(Elements.Find(s.Document, target)), cancel);

    [McpServerTool(Name = "set_xaml", Destructive = true)]
    [Description("Replaces the whole design with XAML (a complete Window or UserControl with its xmlns declarations), as one undo step. Use it to restructure everything at once; add_xaml is better for adding to what is there.")]
    public Task<string> SetXaml(
        [Description("The complete .axaml text.")] string xaml,
        CancellationToken cancel = default) =>
        Edit("replaced the XAML", (s, scratch) =>
        {
            var parsed = s.Parser.ParseDetailed(xaml, s.Registry);
            Elements.NameAll(parsed.Document, parsed.Document.RootElement);
            scratch.RootElement = parsed.Document.RootElement;
            scratch.XClass = parsed.Document.XClass ?? scratch.XClass;
            scratch.Namespaces.Clear();
            scratch.Namespaces.AddRange(parsed.Document.Namespaces);
            scratch.CodeStyle = parsed.Document.CodeStyle;
            return new JsonObject
            {
                ["elements"] = scratch.AllElements.Count(),
                ["warnings"] = Warnings(parsed.Warnings.Select(w => w.Message).Concat(Elements.Check(scratch.RootElement, s.Registry))),
            };
        }, cancel);

    [McpServerTool(Name = "new_document")]
    [Description("Starts a new design, replacing the open one: a Window or UserControl with an empty Grid named layoutRoot to build in, saved later as folder/class_name.axaml. MVVM gives it a view model. Fails if the open design has unsaved changes, unless discard_changes is true.")]
    public Task<string> NewDocument(
        [Description("The class name (\"PatientChart\").")] string class_name,
        [Description("Window or UserControl (the default: a view that sits inside an app's window).")] string kind = "UserControl",
        [Description("The C# namespace for views. Default \"MyApp.Views\".")] string? @namespace = null,
        [Description("Give it a view model and bind with {Binding}. Default true.")] bool mvvm = true,
        [Description("The folder it is saved in, relative to the working folder. Default \"Views\".")] string folder = "Views",
        [Description("Design width in pixels. Default 1000.")] double? width = null,
        [Description("Design height in pixels. Default 680.")] double? height = null,
        [Description("A Window's title. Default: the class name.")] string? title = null,
        [Description("Throw away unsaved changes to the open design. Default false.")] bool discard_changes = false,
        CancellationToken cancel = default) =>
        Read("started a new design", s =>
        {
            Discardable(s, discard_changes);
            var doc = NewDesign(s, class_name, kind, @namespace, mvvm, folder, width, height, title);
            s.Pending.Clear();
            s.Replace(doc, true);
            s.Refresh();
            return Summary(s);
        }, cancel);

    [McpServerTool(Name = "open_document")]
    [Description("Opens an .axaml file from the working folder, replacing the open design. Fails if the open design has unsaved changes, unless discard_changes is true.")]
    public Task<string> OpenDocument(
        [Description("The file, relative to the working folder.")] string path,
        [Description("Throw away unsaved changes to the open design. Default false.")] bool discard_changes = false,
        CancellationToken cancel = default) =>
        Read("opened a design", s =>
        {
            Discardable(s, discard_changes);
            var full = _files.Resolve(path, "A design", ".axaml", ".xaml");
            if (!File.Exists(full)) throw new FileNotFoundException($"There's no {path} in the working folder. list_files shows what is there.");
            var parsed = s.Parser.ParseDetailed(File.ReadAllText(full), s.Registry);
            parsed.Document.FilePath = full;
            s.Pending.Clear();
            s.Replace(parsed.Document, false);
            s.Refresh();
            var o = Summary(s);
            o["warnings"] = Warnings(parsed.Warnings.Select(w => w.Message));
            return o;
        }, cancel);

    [McpServerTool(Name = "save_document")]
    [Description("Saves the design: its .axaml, its code-behind (.axaml.cs) and, if MVVM, its view model, keeping any code already in them; and any model, repository and converter files that scaffold_crud, add_form or add_assessment made. Existing files are only written over with overwrite (the design's own .axaml and code are always updated).")]
    public Task<string> SaveDocument(
        [Description("Where, relative to the working folder (\"Views/Patients.axaml\"). Default: where it was opened or meant to go.")] string? path = null,
        [Description("Write over companion files that already exist. Default false.")] bool overwrite = false,
        CancellationToken cancel = default) =>
        RunAsync("saved the design", async s =>
        {
            var target = path is { Length: > 0 } ? _files.Resolve(path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) ? path : path + ".axaml")
                : s.Document.FilePath.Length > 0 ? s.Document.FilePath
                : throw new ArgumentException("The design has no file yet: give a path such as \"Views/MainView.axaml\".");
            _files.Resolve(Relative(target));
            foreach (var file in s.Pending.Keys)
            {
                _files.Resolve(Relative(file));
                if (File.Exists(file) && !overwrite && !_files.WroteBefore(file) && file != s.ViewModelPath(target))
                    throw new IOException($"{Relative(file)} already exists. Call again with overwrite true to replace it, or move it aside.");
            }
            var written = s.Pending.Keys.Select(Relative).ToList();
            var vmPath = s.Document.CodeStyle == CodeBehindStyle.MVVM ? s.ViewModelPath(target) : null;
            // A view model that a tool made goes where the design's view model goes, so it is merged, not lost.
            if (vmPath != null && !s.Pending.ContainsKey(vmPath) && s.Pending.Keys.FirstOrDefault(k => Path.GetFileName(k) == Path.GetFileName(vmPath)) is { } elsewhere)
            {
                s.Pending[vmPath] = s.Pending[elsewhere];
                s.Pending.Remove(elsewhere);
            }
            await s.SaveAsync(target);
            foreach (var f in written) _files.Wrote(_files.Resolve(f));
            _files.Wrote(target);
            s.Refresh();
            var files = new List<string> { Relative(target), Relative(target + ".cs") };
            if (vmPath != null) files.Add(Relative(vmPath));
            files.AddRange(written.Where(w => !files.Contains(w)));
            return new JsonObject { ["saved"] = new JsonArray(files.Select(f => (JsonNode)f).ToArray()) }.ToJsonString(Json.Options);
        }, cancel);

    // ───────────────────────────── controls ─────────────────────────────

    [McpServerTool(Name = "list_controls", ReadOnly = true, Idempotent = true)]
    [Description("The controls the designer knows, by category (Common Controls, Containers, Data, …), each with what it is for. describe_control gives one's properties and events.")]
    public Task<string> ListControls(
        [Description("Only this category. Optional.")] string? category = null,
        CancellationToken cancel = default) =>
        Read("listed the controls", s =>
        {
            var all = s.Registry.GetAll().Where(d => d.IsToolboxVisible && (category is null || d.Category.Equals(category, StringComparison.OrdinalIgnoreCase))).ToList();
            if (all.Count == 0) throw new ArgumentException($"There's no category \"{category}\". The categories: {string.Join(", ", s.Registry.GetAll().Select(d => d.Category).Distinct())}.");
            return new JsonObject
            {
                ["categories"] = new JsonObject(all.GroupBy(d => d.Category).Select(g => KeyValuePair.Create(g.Key, (JsonNode?)new JsonArray(g.Select(d => (JsonNode)new JsonObject
                {
                    ["type"] = d.ElementName,
                    ["about"] = d.Description,
                    ["holds"] = d.LayoutMode switch
                    {
                        ContainerLayoutMode.None => null,
                        ContainerLayoutMode.Single => "one child",
                        ContainerLayoutMode.Items => "items",
                        _ => "children",
                    },
                }).ToArray())))),
                ["also"] = "Any other Avalonia control can be written in XAML too; the designer shows ones it doesn't know as placeholders.",
            };
        }, cancel);

    [McpServerTool(Name = "describe_control", ReadOnly = true, Idempotent = true)]
    [Description("One control's properties (name, type, category, choices and default), events, the attached properties its children take, and what it holds.")]
    public Task<string> DescribeControl(
        [Description("The control's type (\"Grid\", \"DataGrid\", \"TextBox\").")] string type,
        CancellationToken cancel = default) =>
        Read("described a control", s =>
        {
            var d = s.Registry.GetByElementName(type.Trim()) ?? throw new ArgumentException($"The designer doesn't know \"{type}\". list_controls lists the controls it does.");
            return new JsonObject
            {
                ["type"] = d.ElementName,
                ["about"] = d.Description,
                ["category"] = d.Category,
                ["holds"] = d.LayoutMode.ToString(),
                ["content_property"] = d.ContentProperty,
                ["properties"] = new JsonArray(d.Properties.Where(p => p.IsBrowsable).Select(p => (JsonNode)new JsonObject
                {
                    ["name"] = p.FullName,
                    ["type"] = p.PropertyType.Name,
                    ["choices"] = p.StandardValues is { } sv ? new JsonArray(sv.Select(v => (JsonNode)v).ToArray())
                        : p.PropertyType.IsEnum ? new JsonArray(Enum.GetNames(p.PropertyType).Select(v => (JsonNode)v).ToArray()) : null,
                    ["about"] = p.Description,
                }).ToArray()),
                ["events"] = new JsonArray(d.Events.Select(e => (JsonNode)e.Name).ToArray()),
                ["children_take"] = d.ChildAttachedProperties.Count > 0 ? new JsonArray(d.ChildAttachedProperties.Select(p => (JsonNode)p.FullName).ToArray()) : null,
            };
        }, cancel);

    // ───────────────────────────── building ─────────────────────────────

    [McpServerTool(Name = "add_xaml")]
    [Description("Adds a fragment of Avalonia XAML (one or more elements, nested as deep as needed, with bindings, attached properties and property elements such as Grid.RowDefinitions) into a parent element. Elements without an x:Name get one. The quickest way to build: write the XAML you would write by hand.")]
    public Task<string> AddXaml(
        [Description("The parent: its x:Name or path, or \"root\". For a Grid, set Grid.Row and Grid.Column on the new elements.")] string parent,
        [Description("The XAML: elements in the default Avalonia namespace, without xmlns declarations (the design's prefixes such as vm: may be used).")] string xaml,
        [Description("Where among the parent's children, from 0. Default: at the end.")] int? index = null,
        [Description("Remove the parent's children first (to rebuild a panel). Default false.")] bool replace = false,
        CancellationToken cancel = default) =>
        Edit("added XAML", (s, scratch) =>
        {
            var host = Elements.Find(scratch, parent);
            List<DesignElement> added;
            try
            {
                added = s.Parser.ParseFragment(xaml, scratch, s.Registry);
            }
            catch (XamlParseException e)
            {
                throw new ArgumentException($"The XAML doesn't parse: {e.Message}");
            }
            if (added.Count == 0) throw new ArgumentException("The XAML has no elements.");
            Elements.Insert(host, added, index, replace, s.Registry);
            foreach (var e in added) Elements.NameAll(scratch, e);
            return Added(s, added);
        }, cancel);

    [McpServerTool(Name = "add_controls")]
    [Description("Adds controls into a parent element, each from a spec (type, name, text, properties, events, children), as one undo step. add_xaml does the same from XAML.")]
    public Task<string> AddControls(
        [Description("The parent: its x:Name or path, or \"root\".")] string parent,
        [Description("The controls, in order.")] List<ControlSpec> controls,
        [Description("Where among the parent's children, from 0. Default: at the end.")] int? index = null,
        CancellationToken cancel = default) =>
        Edit("added controls", (s, scratch) =>
        {
            if (controls.Count == 0) throw new ArgumentException("Give at least one control.");
            var host = Elements.Find(scratch, parent);
            var made = controls.Select(c => Build(s, scratch, c)).ToList();
            Elements.Insert(host, made, index, false, s.Registry);
            foreach (var e in made) Elements.NameAll(scratch, e);
            return Added(s, made);
        }, cancel);

    [McpServerTool(Name = "set_properties")]
    [Description("Sets properties on one or more elements, as XAML text (\"Margin\": \"8\", \"Grid.Row\": \"2\", \"IsEnabled\": \"{Binding CanEdit}\"); a null value removes the property. x:Name renames.")]
    public Task<string> SetProperties(
        [Description("The properties and their XAML values; null removes one.")] Dictionary<string, string?> properties,
        [Description(TargetHelp)] string? target = null,
        [Description("Several elements, by name or path, instead of target.")] List<string>? targets = null,
        CancellationToken cancel = default) =>
        Edit("set properties", (s, scratch) =>
        {
            var list = Elements.Targets(scratch, target, targets);
            if (properties.Count == 0) throw new ArgumentException("Give at least one property.");
            var warnings = new List<string>();
            foreach (var e in list)
                foreach (var (name, value) in properties)
                {
                    if (name is "x:Name" or "Name")
                    {
                        if (string.IsNullOrWhiteSpace(value) || !CodeIdentifiers.IsValidIdentifier(value)) throw new ArgumentException($"\"{value}\" isn't a usable x:Name: letters, digits and underscores, not starting with a digit.");
                        if (scratch.NamedElements.TryGetValue(value, out var other) && !ReferenceEquals(other, e)) throw new ArgumentException($"Another element is already called {value}.");
                        e.Name = value;
                        continue;
                    }
                    e.SetValue(name, value);
                    if (value != null) warnings.AddRange(Elements.CheckProperty(e, name, s.Registry));
                }
            return new JsonObject
            {
                ["changed"] = new JsonArray(list.Select(e => (JsonNode)Elements.Label(e)).ToArray()),
                ["warnings"] = Warnings(warnings),
            };
        }, cancel);

    [McpServerTool(Name = "set_event")]
    [Description("Wires an element's event to a handler method in the code-behind (\"Click\" → \"SaveButton_Click\"); saving writes the handler stub, keeping the body of one already written. No handler removes it.")]
    public Task<string> SetEvent(
        [Description(TargetHelp)] string target,
        [Description("The event (describe_control lists them): \"Click\", \"SelectionChanged\", \"TextChanged\"…")] string @event,
        [Description("The handler method's name. Default: a name from the element and event. Empty removes it.")] string? handler = null,
        CancellationToken cancel = default) =>
        Edit("wired an event", (s, scratch) =>
        {
            var e = Elements.Find(scratch, target);
            if (e.Descriptor is { } d && d.FindEvent(@event) is null && d.Events.Count > 0)
                throw new ArgumentException($"{d.ElementName} has no event \"{@event}\". Its events: {string.Join(", ", d.Events.Select(x => x.Name))}.");
            var name = handler is null ? HandlerNaming.Suggest(e, @event, scratch) : handler.Trim();
            if (name.Length > 0 && !CodeIdentifiers.IsValidIdentifier(name)) throw new ArgumentException($"\"{name}\" isn't a C# method name.");
            e.SetEventHandler(@event, name.Length == 0 ? null : name);
            return new JsonObject { ["element"] = Elements.Label(e), ["event"] = @event, ["handler"] = name.Length == 0 ? null : name };
        }, cancel);

    [McpServerTool(Name = "delete_elements", Destructive = true)]
    [Description("Deletes elements (and everything inside them). In the open window, deleting most of the design asks the person first.")]
    public async Task<string> DeleteElements(
        McpServer server,
        [Description(TargetHelp)] string? target = null,
        [Description("Several elements, by name or path, instead of target.")] List<string>? targets = null,
        CancellationToken cancel = default)
    {
        var (count, total) = await Run("counted", s =>
        {
            var list = Elements.Targets(s.Document, target, targets);
            return (list.Sum(e => e.SelfAndAllDescendants().Count()), s.Document.AllElements.Count());
        }, cancel).ConfigureAwait(false);
        if (host is DesignHost { Live: true } && count >= 8 && count * 2 >= total)
            await Confirm(server, $"An AI agent wants to delete {count} of the design's {total} elements. Go ahead?", cancel).ConfigureAwait(false);
        return await Edit("deleted elements", (s, scratch) =>
        {
            var list = Elements.Targets(scratch, target, targets);
            if (list.Any(e => e.Parent is null)) throw new ArgumentException("The root can't be deleted: set_xaml or new_document replaces the whole design.");
            foreach (var e in list) e.Detach();
            return new JsonObject { ["deleted"] = new JsonArray(list.Select(e => (JsonNode)Elements.Label(e)).ToArray()), ["elements_removed"] = count };
        }, cancel).ConfigureAwait(false);
    }

    [McpServerTool(Name = "move_element")]
    [Description("Moves an element into another parent (or to another place in the same one).")]
    public Task<string> MoveElement(
        [Description(TargetHelp)] string target,
        [Description("The new parent: its name or path, or \"root\".")] string parent,
        [Description("Where among the parent's children, from 0. Default: at the end.")] int? index = null,
        CancellationToken cancel = default) =>
        Edit("moved an element", (s, scratch) =>
        {
            var e = Elements.Find(scratch, target);
            var to = Elements.Find(scratch, parent);
            if (e.Parent is null) throw new ArgumentException("The root can't be moved.");
            if (ReferenceEquals(e, to) || e.IsAncestorOf(to)) throw new ArgumentException("An element can't be moved into itself.");
            e.Detach();
            Elements.Insert(to, [e], index, false, s.Registry);
            return new JsonObject { ["moved"] = Elements.Label(e), ["into"] = Elements.Label(to), ["path"] = Elements.PathOf(e) };
        }, cancel);

    [McpServerTool(Name = "set_grid")]
    [Description("Sets a Grid's rows and columns (\"Auto,*,Auto\", \"200,*\", \"2*,3*\") and, optionally, places children in cells: {\"firstNameBox\": [1, 2]} puts it at row 1, column 2 (and [row, column, row_span, column_span] spans).")]
    public Task<string> SetGrid(
        [Description("The Grid, by name or path.")] string target,
        [Description("Row heights, comma-separated (Auto, *, 2*, or pixels). Optional.")] string? rows = null,
        [Description("Column widths, comma-separated. Optional.")] string? columns = null,
        [Description("Cells for children, by name or path: [row, column] or [row, column, row_span, column_span].")] Dictionary<string, List<int>>? cells = null,
        CancellationToken cancel = default) =>
        Edit("laid out a grid", (s, scratch) =>
        {
            var g = Elements.Find(scratch, target);
            if (CodeIdentifiers.BareTypeName(g.ElementType) != "Grid") throw new ArgumentException($"{Elements.Label(g)} is a {g.ElementType}, not a Grid.");
            if (rows != null) Set(g, "RowDefinitions", rows);
            if (columns != null) Set(g, "ColumnDefinitions", columns);
            foreach (var (name, cell) in cells ?? [])
            {
                var child = Elements.Find(scratch, name);
                if (!ReferenceEquals(child.Parent, g)) throw new ArgumentException($"{Elements.Label(child)} isn't directly in {Elements.Label(g)}.");
                if (cell.Count is not (2 or 4)) throw new ArgumentException($"{name}: a cell is [row, column] or [row, column, row_span, column_span].");
                child.SetValue("Grid.Row", cell[0] == 0 ? null : cell[0].ToString());
                child.SetValue("Grid.Column", cell[1] == 0 ? null : cell[1].ToString());
                if (cell.Count == 4)
                {
                    child.SetValue("Grid.RowSpan", cell[2] <= 1 ? null : cell[2].ToString());
                    child.SetValue("Grid.ColumnSpan", cell[3] <= 1 ? null : cell[3].ToString());
                }
            }
            return new JsonObject { ["grid"] = Elements.Label(g), ["rows"] = g.GetValue("RowDefinitions"), ["columns"] = g.GetValue("ColumnDefinitions") };

            static void Set(DesignElement g, string name, string value)
            {
                var parts = value.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Any(p => !System.Text.RegularExpressions.Regex.IsMatch(p, @"^(Auto|\*|\d+(\.\d+)?\*|\d+(\.\d+)?)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
                    throw new ArgumentException($"\"{value}\" isn't a list of sizes: use Auto, *, 2* or pixels, separated by commas.");
                if (g.GetPropertyElement(name) is { } element) g.Properties.Remove(element);
                g.SetValue(name, string.Join(",", parts));
            }
        }, cancel);

    [McpServerTool(Name = "wrap_in")]
    [Description("Puts elements inside a new container (Border, StackPanel, Grid, ScrollViewer, Expander…) where they were, keeping their order.")]
    public Task<string> WrapIn(
        [Description("The container's type.")] string container,
        [Description(TargetHelp)] string? target = null,
        [Description("Several elements with the same parent, instead of target.")] List<string>? targets = null,
        [Description("Properties for the container, as XAML text.")] Dictionary<string, string>? properties = null,
        [Description("The container's x:Name. Optional.")] string? name = null,
        CancellationToken cancel = default) =>
        Edit("wrapped elements", (s, scratch) =>
        {
            var list = Elements.Targets(scratch, target, targets);
            var parent = list[0].Parent ?? throw new ArgumentException("The root can't be wrapped.");
            if (list.Any(e => !ReferenceEquals(e.Parent, parent) || e.OwnerProperty != null)) throw new ArgumentException("The elements to wrap must sit side by side in one parent.");
            var spec = new ControlSpec { Type = container, Name = name, Properties = properties };
            var box = Build(s, scratch, spec);
            if (box.Descriptor is { MaxChildren: 1 } && list.Count > 1) throw new ArgumentException($"A {container} holds one child: wrap them in a StackPanel or Grid first, or use one of those.");
            var at = list.Min(e => e.IndexInParent);
            // The wrapped elements' places in a grid or dock move to the container.
            foreach (var attached in list[0].AttachedProperties.ToList())
                if (attached.OwnerType is "Grid" or "DockPanel" or "Canvas")
                {
                    box.SetValue(attached.FullName, attached.StringValue);
                    foreach (var e in list) e.SetValue(attached.FullName, null);
                }
            foreach (var e in list.OrderBy(e => e.IndexInParent)) e.Detach();
            parent.AddChild(box, at);
            foreach (var e in list) box.AddChild(e);
            Elements.NameAll(scratch, box);
            return new JsonObject { ["container"] = Elements.Label(box), ["path"] = Elements.PathOf(box) };
        }, cancel);

    // ───────────────────────────── MVVM and code ─────────────────────────────

    [McpServerTool(Name = "make_mvvm")]
    [Description("Makes the design an MVVM view: a view model class (\"{Class}ViewModel\") as its data context, with x:DataType for compiled bindings. With bind_controls, also binds named inputs to view-model properties (a TextBox's Text, a CheckBox's IsChecked, a Button's Command…).")]
    public Task<string> MakeMvvm(
        [Description("Also bind named controls that have no binding yet. Default false.")] bool bind_controls = false,
        CancellationToken cancel = default) =>
        Edit("made the view MVVM", (s, scratch) =>
        {
            var options = DesignFiles.Options(scratch);
            if (scratch.CodeStyle != CodeBehindStyle.MVVM)
                MvvmScaffolder.Apply(scratch, options.EffectiveViewModelNamespace, scratch.ClassName + "ViewModel");
            var bound = new JsonArray();
            if (bind_controls)
                foreach (var (e, prop, binding) in MvvmBindingHeuristics.Suggest(scratch).ToList())
                {
                    e.SetValue(prop, binding);
                    bound.Add((JsonNode)$"{Elements.Label(e)}.{prop} = {binding}");
                }
            return new JsonObject { ["view_model"] = $"{options.EffectiveViewModelNamespace}.{scratch.ClassName}ViewModel", ["bound"] = bound };
        }, cancel);

    [McpServerTool(Name = "get_code", ReadOnly = true, Idempotent = true)]
    [Description("The code the design would save now: its code-behind, its view model (MVVM), or a generated companion file (a model, repository or converters, by its path from get_document's pending_files).")]
    public Task<string> GetCode(
        [Description("code_behind, view_model, or the path of a pending file.")] string which = "code_behind",
        CancellationToken cancel = default) =>
        Run("read the code", s => which.Trim().ToLowerInvariant() switch
        {
            "code_behind" or "codebehind" or "code-behind" => s.CodeBehind,
            "view_model" or "viewmodel" => s.Document.CodeStyle == CodeBehindStyle.MVVM ? s.ViewModel : throw new ArgumentException("The design isn't MVVM, so it has no view model (make_mvvm makes it one)."),
            _ => s.Pending.FirstOrDefault(p => Relative(p.Key).Equals(which.Trim().Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) || Path.GetFileName(p.Key).Equals(which.Trim(), StringComparison.OrdinalIgnoreCase)).Value
                 ?? throw new ArgumentException($"\"{which}\" is none of code_behind, view_model or a pending file ({(s.Pending.Count == 0 ? "there are none" : string.Join(", ", s.Pending.Keys.Select(Relative)))})."),
        }, cancel);

    // ───────────────────────────── looking ─────────────────────────────

    [McpServerTool(Name = "render_view", ReadOnly = true, Idempotent = true)]
    [Description("A picture of the design as the designer's surface draws it: look at it after building, to check the layout reads well (alignment, spacing, text cut off, nothing overlapping). Bindings show no data at design time.")]
    public async Task<CallToolResult> RenderView(
        [Description("Scale, from 0.2 to 2. Default: fit about 1100 pixels wide.")] double? scale = null,
        CancellationToken cancel = default)
    {
        if (scale is < 0.2 or > 2) throw new McpException("scale is from 0.2 to 2.");
        var png = await Run("looked at the design", s =>
        {
            var w = Elements.Number(s.Document.RootElement.GetValue("Width")) ?? Elements.Number(s.Document.RootElement.GetValue("d:DesignWidth")) ?? 1000;
            var k = scale ?? Math.Clamp(1100 / Math.Max(1, w), 0.2, 1);
            return host.Library.Picture(s.Document, k, s.Document.FilePath.Length > 0 ? Path.GetDirectoryName(s.Document.FilePath) : host.Root);
        }, cancel).ConfigureAwait(false);
        return new CallToolResult { Content = [ImageContentBlock.FromBytes(png, "image/png")] };
    }

    // ───────────────────────────── files ─────────────────────────────

    [McpServerTool(Name = "list_files", ReadOnly = true, Idempotent = true)]
    [Description("Lists files in the working folder (or a folder inside it): views, models, SQL scripts, to find one to open or read. Optionally only those matching a pattern such as \"*.cs\", and through sub-folders.")]
    public Task<string> ListFiles(
        [Description("A folder inside the working folder. Default: the working folder.")] string folder = ".",
        [Description("A file name pattern, such as \"*.axaml\" or \"*.sql\". Default: every file.")] string pattern = "*",
        [Description("Look through sub-folders too. Default false.")] bool recursive = false,
        CancellationToken cancel = default) =>
        Read("listed files", _ =>
        {
            var full = folder is "." or "" ? _files.Root : _files.Resolve(folder);
            if (!Directory.Exists(full)) throw new DirectoryNotFoundException($"There's no folder {folder} in the working folder.");
            var skip = new[] { "bin", "obj", "node_modules", ".git", ".vs", ".idea" };
            var files = Directory.EnumerateFiles(full, pattern, new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true })
                .Where(f => !Path.GetRelativePath(full, f).Split(Path.DirectorySeparatorChar).Any(part => skip.Contains(part, StringComparer.OrdinalIgnoreCase)))
                .Take(501).ToList();
            var folders = recursive ? [] : Directory.EnumerateDirectories(full).Select(Path.GetFileName).Where(n => n is not null && !skip.Contains(n, StringComparer.OrdinalIgnoreCase)).Take(200).ToList();
            return new JsonObject
            {
                ["folder"] = Path.GetRelativePath(_files.Root, full),
                ["folders"] = new JsonArray(folders.Select(f => (JsonNode)f!).ToArray()),
                ["files"] = new JsonArray(files.Take(500).Select(f => (JsonNode)new JsonObject { ["path"] = Path.GetRelativePath(_files.Root, f), ["bytes"] = new FileInfo(f).Length }).ToArray()),
                ["more"] = files.Count > 500 ? "More than 500: narrow it with a pattern or a folder." : null,
            };
        }, cancel);

    /// <summary>How many characters read_file returns at most in one call.</summary>
    internal const int ReadLimit = 60_000;

    [McpServerTool(Name = "read_file", ReadOnly = true, Idempotent = true)]
    [Description("Reads a text file in the working folder (source code, a SQL script, a spec, an existing screen's XAML), with its lines numbered. A long file comes in pieces: give start_line to read on from where the last piece ended.")]
    public Task<string> ReadFile(
        [Description("The file, relative to the working folder.")] string path,
        [Description("The first line to read, from 1. Default 1.")] int start_line = 1,
        [Description("At most this many lines. Default 600.")] int max_lines = 600,
        CancellationToken cancel = default) =>
        Read("read a file", _ =>
        {
            var full = _files.Resolve(path);
            if (!File.Exists(full)) throw new FileNotFoundException($"There's no {path} in the working folder. list_files shows what is there.");
            var info = new FileInfo(full);
            if (info.Length > 20_000_000) throw new IOException($"{path} is {info.Length / 1_000_000} MB: too big to read here.");
            var bytes = File.ReadAllBytes(full);
            if (bytes.Take(8000).Any(b => b == 0)) throw new NotSupportedException($"{path} isn't text (it has binary data in it).");
            var all = Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n").Split('\n');
            if (start_line < 1 || start_line > Math.Max(1, all.Length)) throw new ArgumentException($"start_line is from 1 to {all.Length}.");
            var take = Math.Clamp(max_lines, 1, 5000);
            var text = new StringBuilder();
            var last = start_line - 1;
            for (var i = start_line - 1; i < all.Length && i < start_line - 1 + take; i++)
            {
                var line = $"{i + 1,5}  {all[i]}\n";
                if (text.Length + line.Length > ReadLimit && text.Length > 0) break;
                text.Append(line);
                last = i + 1;
            }
            return new JsonObject
            {
                ["path"] = Path.GetRelativePath(_files.Root, full),
                ["lines"] = all.Length,
                ["from"] = start_line,
                ["to"] = last,
                ["text"] = text.ToString(),
                ["more"] = last < all.Length ? $"Lines {last + 1} to {all.Length} remain: call again with start_line {last + 1}." : null,
            };
        }, cancel);

    // ───────────────────────────── history ─────────────────────────────

    [McpServerTool(Name = "undo")]
    [Description("Takes back the last change to the design (anyone's: the person's or an agent's), as Ctrl+Z does.")]
    public Task<string> Undo(CancellationToken cancel = default) =>
        Read("undid a change", s =>
        {
            if (!s.History.CanUndo) throw new InvalidOperationException("There's nothing to undo.");
            var what = s.History.UndoDescription;
            s.History.Undo();
            s.Refresh();
            return new JsonObject { ["undone"] = what };
        }, cancel);

    [McpServerTool(Name = "redo")]
    [Description("Puts back the change undo last took back.")]
    public Task<string> Redo(CancellationToken cancel = default) =>
        Read("redid a change", s =>
        {
            if (!s.History.CanRedo) throw new InvalidOperationException("There's nothing to redo.");
            var what = s.History.RedoDescription;
            s.History.Redo();
            s.Refresh();
            return new JsonObject { ["redone"] = what };
        }, cancel);

    // ───────────────────────────── helpers ─────────────────────────────

    private string Relative(string full) => Path.GetRelativePath(host.Root, full).Replace('\\', '/');

    private JsonObject Summary(IDesignSession s) => new()
    {
        ["file"] = s.Document.FilePath.Length > 0 ? Relative(s.Document.FilePath) : null,
        ["class"] = s.Document.XClass,
        ["kind"] = s.Document.RootElementType,
        ["mvvm"] = s.Document.CodeStyle == CodeBehindStyle.MVVM,
        ["elements"] = s.Document.AllElements.Count(),
    };

    /// <summary>A new design document: a class in a namespace, saved later in a folder of the working folder.</summary>
    private DesignDocument NewDesign(IDesignSession s, string className, string kind, string? ns, bool mvvm, string folder, double? width, double? height, string? title)
    {
        var cls = CodeIdentifiers.MakeIdentifier(className.Trim());
        if (!CodeIdentifiers.IsValidIdentifier(cls)) throw new ArgumentException($"\"{className}\" isn't a usable class name.");
        var k = kind.Trim().ToLowerInvariant() switch
        {
            "window" => "Window",
            "usercontrol" or "user_control" or "view" or "control" => "UserControl",
            _ => throw new ArgumentException("kind is Window or UserControl."),
        };
        var space = string.IsNullOrWhiteSpace(ns) ? "MyApp.Views" : ns.Trim();
        var doc = s.Parser.Parse(Templates.Blank(k, $"{space}.{cls}", mvvm, width, height, title), s.Registry);
        doc.FilePath = Path.Combine(_files.Resolve(string.IsNullOrWhiteSpace(folder) ? "." : folder), cls + ".axaml");
        return doc;
    }

    private static void Discardable(IDesignSession s, bool discard)
    {
        if (s.Document.IsDirty && !discard)
            throw new InvalidOperationException("The open design has changes that haven't been saved. Save it first (save_document), or call again with discard_changes true to throw them away.");
    }

    /// <summary>An element built from a spec, with its children.</summary>
    private static DesignElement Build(IDesignSession s, DesignDocument doc, ControlSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.Type)) throw new ArgumentException("Each control needs a type.");
        var type = spec.Type.Trim();
        var descriptor = s.Registry.GetByElementName(type);
        var e = descriptor != null ? new DesignElement(descriptor.ElementName, descriptor) : new DesignElement(type);
        if (!string.IsNullOrWhiteSpace(spec.Name))
        {
            if (!CodeIdentifiers.IsValidIdentifier(spec.Name.Trim())) throw new ArgumentException($"\"{spec.Name}\" isn't a usable x:Name.");
            e.Name = spec.Name.Trim();
        }
        if (spec.Text != null) e.SetValue(TextProperty(type, descriptor), spec.Text);
        foreach (var (name, value) in spec.Properties ?? []) e.SetValue(name, value);
        foreach (var (ev, handler) in spec.Events ?? []) e.SetEventHandler(ev, handler);
        foreach (var child in spec.Children ?? [])
        {
            if (descriptor is { IsContainer: false }) throw new ArgumentException($"A {type} can't hold other controls.");
            e.AddChild(Build(s, doc, child));
        }
        if (descriptor is { MaxChildren: { } max } && e.Children.Count > max)
            throw new ArgumentException($"A {type} holds {max} child: put its controls in a StackPanel or Grid inside it.");
        return e;
    }

    private static string TextProperty(string type, ControlDescriptor? d) => CodeIdentifiers.BareTypeName(type) switch
    {
        "TextBlock" or "TextBox" or "SelectableTextBlock" or "AutoCompleteBox" => "Text",
        "Expander" or "TabItem" or "MenuItem" or "HeaderedContentControl" => "Header",
        "Window" => "Title",
        _ => d?.ContentProperty is "Content" ? "Content" : "Content",
    };

    private JsonObject Added(IDesignSession s, List<DesignElement> added)
    {
        var warnings = added.SelectMany(e => Elements.Check(e, s.Registry)).ToList();
        return new JsonObject
        {
            ["added"] = new JsonArray(added.Select(e => (JsonNode)new JsonObject { ["element"] = Elements.Label(e), ["path"] = Elements.PathOf(e), ["inside"] = e.SelfAndAllDescendants().Count() - 1 }).ToArray()),
            ["names"] = new JsonArray(added.SelectMany(e => e.SelfAndAllDescendants()).Where(e => e.Name != null).Take(60).Select(e => (JsonNode)e.Name!).ToArray()),
            ["warnings"] = Warnings(warnings),
        };
    }

    private static JsonArray? Warnings(IEnumerable<string> list)
    {
        var all = list.Distinct().Take(20).ToList();
        return all.Count == 0 ? null : new JsonArray(all.Select(w => (JsonNode)w).ToArray());
    }

    /// <summary>Runs a call that changes nothing (or replaces the design wholesale) and answers in JSON.</summary>
    private Task<string> Read(string action, Func<IDesignSession, JsonObject> work, CancellationToken cancel) =>
        Run(action, s => work(s).ToJsonString(Json.Options), cancel);

    /// <summary>
    /// Runs a change as one undo step: the work is done on a copy of the design, and only if it succeeds does the copy
    /// replace the design (one <see cref="AgentEditCommand"/>). If it fails, the design is untouched.
    /// </summary>
    private Task<string> Edit(string label, Func<IDesignSession, DesignDocument, JsonObject> work, CancellationToken cancel) =>
        Run(label, s =>
        {
            var before = s.Serializer.Serialize(s.Document);
            var scratch = s.Parser.Parse(before, s.Registry);
            scratch.FilePath = s.Document.FilePath;
            scratch.CodeStyle = s.Document.CodeStyle;
            scratch.AssemblyName = s.Document.AssemblyName;
            var result = work(s, scratch);
            var after = s.Serializer.Serialize(scratch);
            if (after == before && scratch.CodeStyle == s.Document.CodeStyle)
            {
                result["unchanged"] = true;
                return result.ToJsonString(Json.Options);
            }
            // The change must read back as it will be saved.
            if (!s.Parser.TryParse(after, s.Registry, out _, out var error))
                throw new InvalidOperationException($"That change would leave XAML that doesn't parse ({error}), so it wasn't made.");
            s.History.Execute(new AgentEditCommand(scratch, "AI: " + label));
            var named = new List<DesignElement>();
            if (result["names"] is JsonArray names)
                foreach (var n in names.Take(1))
                    if (n?.GetValue<string>() is { } name && s.Document.NamedElements.TryGetValue(name, out var e)) named.Add(e);
            s.Select(named);
            s.Refresh();
            return result.ToJsonString(Json.Options);
        }, cancel);

    private async Task<T> Run<T>(string action, Func<IDesignSession, T> work, CancellationToken cancel)
    {
        try
        {
            return await host.RunAsync(action, work, cancel).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not McpException and not OperationCanceledException)
        {
            throw new McpException(Explain(e), e);
        }
    }

    private async Task<T> RunAsync<T>(string action, Func<IDesignSession, Task<T>> work, CancellationToken cancel)
    {
        try
        {
            return await host.RunAsync(action, work, cancel).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not McpException and not OperationCanceledException)
        {
            throw new McpException(Explain(e), e);
        }
    }

    /// <summary>Asks the person (MCP elicitation; the in-app chat shows buttons). Throws if they say no or can't be asked.</summary>
    private static async Task Confirm(McpServer server, string question, CancellationToken cancel)
    {
        ElicitResult answer;
        try
        {
            answer = await server.ElicitAsync(new ElicitRequestParams
            {
                Message = question,
                RequestedSchema = new ElicitRequestParams.RequestSchema { Properties = { } },
            }, cancel).ConfigureAwait(false);
        }
        catch (Exception e) when (e is McpException or InvalidOperationException or NotSupportedException)
        {
            throw new McpException($"{question} The person couldn't be asked ({e.Message}), so nothing was changed.");
        }
        if (answer.Action != "accept")
            throw new McpException($"The person said no: {question.Replace(" Go ahead?", "", StringComparison.Ordinal)}. Nothing was changed. Ask them what they want, or do less at a time.");
    }

    internal static string Explain(Exception e) => e switch
    {
        ArgumentException or InvalidOperationException or FormatException or NotSupportedException or IOException
            or UnauthorizedAccessException or DesignBusyException or JsonException or InvalidDataException or KeyNotFoundException
            or XamlParseException => e.Message,
        System.Data.Common.DbException => $"The database said: {e.Message}",
        _ => $"Something went wrong ({e.GetType().Name}): {e.Message}",
    };
}
