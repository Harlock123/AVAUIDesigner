using System.Text;

namespace AVAUIDesigner.Data;

/// <summary>Names and namespaces of what a CRUD screen is made of.</summary>
public sealed class CrudNames
{
    public required string ViewNamespace { get; init; }
    public required string ViewClass { get; init; }
    public required string ViewModelNamespace { get; init; }
    public required string ViewModelClass { get; init; }
    public required string ModelNamespace { get; init; }
    public required string ModelClass { get; init; }
    public required string DataNamespace { get; init; }
    public required string RepositoryClass { get; init; }

    /// <summary>The usual names for an entity's screen: Clinic.Views.Patients, Clinic.ViewModels.PatientsViewModel, Clinic.Models.Patient, Clinic.Data.PatientRepository.</summary>
    public static CrudNames For(EntityModel e, string viewNamespace, string? viewClass = null, string? modelNamespace = null)
    {
        var app = viewNamespace.EndsWith(".Views", StringComparison.Ordinal) ? viewNamespace[..^6] : viewNamespace;
        var view = string.IsNullOrWhiteSpace(viewClass) ? e.Plural : Identifiers.Pascal(viewClass);
        return new CrudNames
        {
            ViewNamespace = viewNamespace,
            ViewClass = view,
            ViewModelNamespace = app + ".ViewModels",
            ViewModelClass = view + "ViewModel",
            ModelNamespace = modelNamespace ?? app + ".Models",
            ModelClass = e.Name,
            DataNamespace = app + ".Data",
            RepositoryClass = e.Name + "Repository",
        };
    }
}

/// <summary>
/// Writes the parts of a CRUD screen for an entity: the screen's XAML (a searchable list and an edit form, an editable
/// grid, or a form alone), the model class, a repository with the engine's own SQL (parameterised, never built from
/// what people type), and the view model whose commands load, search, add, save and delete.
/// </summary>
public static class CrudGenerator
{
    public static readonly string[] Layouts = ["list_detail", "grid", "form"];

    // ───────────────────────────── the screen ─────────────────────────────

    /// <summary>The screen's content, as a XAML fragment to go inside the view's root.</summary>
    public static string ViewXaml(EntityModel e, string layout, int formColumns, bool existingModel)
    {
        if (!Layouts.Contains(layout)) throw new ArgumentException($"layout is one of {string.Join(", ", Layouts)}, not \"{layout}\".");
        var shown = Shown(e).ToList();
        var formFields = e.Fields.Where(f => !f.IsComputed || f.IsKey).ToList();
        var form = FormXaml.Form(formFields, new FormOptions
        {
            BindingPrefix = "Selected.",
            Columns = formColumns,
            Name = Identifiers.Camel(e.Name).TrimStart('@') + "Form",
            OptionsFromViewModel = true,
            ConvertersClass = existingModel ? "vm:Converters" : null,
        });
        var title = FormXaml.Escape(e.Plural == e.Name ? e.DisplayLabel : Words.FromIdentifier(e.Plural));
        var single = FormXaml.Escape(e.DisplayLabel.ToLowerInvariant());
        var sb = new StringBuilder();
        var toolbar = $"""
              <DockPanel Grid.Row="0" Grid.ColumnSpan="3" Margin="0,0,0,10" LastChildFill="False">
                <TextBlock Text="{title}" FontSize="20" FontWeight="SemiBold" VerticalAlignment="Center" Margin="0,0,16,0" />
                <TextBox x:Name="searchBox" Text="{"{"}Binding SearchText, Mode=TwoWay{"}"}" Watermark="Search {FormXaml.Escape(title.ToLowerInvariant())}" Width="240" VerticalAlignment="Center" />
                <Button x:Name="searchButton" Content="Search" Command="{"{"}Binding SearchCommand{"}"}" IsDefault="True" Margin="6,0,0,0" />
                <Button x:Name="deleteButton" DockPanel.Dock="Right" Content="Delete" Command="{"{"}Binding DeleteCommand{"}"}" IsEnabled="{"{"}Binding HasSelection{"}"}" Margin="6,0,0,0" />
                <Button x:Name="saveButton" DockPanel.Dock="Right" Content="Save" Command="{"{"}Binding SaveCommand{"}"}" IsEnabled="{"{"}Binding HasSelection{"}"}" Classes="accent" Margin="6,0,0,0" />
                <Button x:Name="newButton" DockPanel.Dock="Right" Content="New {single}" Command="{"{"}Binding NewCommand{"}"}" Margin="6,0,0,0" />
                <Button x:Name="refreshButton" DockPanel.Dock="Right" Content="Refresh" Command="{"{"}Binding LoadCommand{"}"}" Margin="6,0,0,0" />
              </DockPanel>
            """;
        var grid = Grid(e, shown, layout == "grid");
        var status = """  <TextBlock x:Name="statusText" Grid.Row="2" Grid.ColumnSpan="3" Text="{Binding Status}" Margin="0,8,0,0" Opacity="0.8" />""";
        switch (layout)
        {
            case "list_detail":
                sb.Append("<Grid x:Name=\"crudLayout\" RowDefinitions=\"Auto,*,Auto\" ColumnDefinitions=\"3*,12,4*\" Margin=\"12\">\n");
                sb.Append(toolbar).Append('\n');
                sb.Append(Indent(grid, "  ").Replace("<DataGrid ", "<DataGrid Grid.Row=\"1\" Grid.Column=\"0\" ", StringComparison.Ordinal)).Append('\n');
                sb.Append($"  <ScrollViewer Grid.Row=\"1\" Grid.Column=\"2\" IsEnabled=\"{{Binding HasSelection}}\">\n");
                sb.Append("    <StackPanel Spacing=\"6\">\n");
                sb.Append($"      <TextBlock Text=\"{FormXaml.Escape(e.DisplayLabel)} details\" FontSize=\"16\" FontWeight=\"SemiBold\" />\n");
                sb.Append(Indent(form, "      ")).Append('\n');
                sb.Append("    </StackPanel>\n  </ScrollViewer>\n");
                sb.Append(status).Append("\n</Grid>");
                break;
            case "grid":
                sb.Append("<Grid x:Name=\"crudLayout\" RowDefinitions=\"Auto,*,Auto\" ColumnDefinitions=\"*,0,0\" Margin=\"12\">\n");
                sb.Append(toolbar).Append('\n');
                sb.Append(Indent(grid, "  ").Replace("<DataGrid ", "<DataGrid Grid.Row=\"1\" Grid.ColumnSpan=\"3\" ", StringComparison.Ordinal)).Append('\n');
                sb.Append(status).Append("\n</Grid>");
                break;
            default:
                sb.Append("<Grid x:Name=\"crudLayout\" RowDefinitions=\"Auto,*,Auto\" ColumnDefinitions=\"*,0,0\" Margin=\"12\">\n");
                sb.Append(toolbar.Replace("Width=\"240\"", "Width=\"200\"", StringComparison.Ordinal)).Append('\n');
                sb.Append("  <ScrollViewer Grid.Row=\"1\" Grid.ColumnSpan=\"3\">\n");
                sb.Append(Indent(form, "    ")).Append('\n');
                sb.Append("  </ScrollViewer>\n");
                sb.Append(status).Append("\n</Grid>");
                break;
        }
        return sb.ToString();
    }

    /// <summary>The columns a list shows: the key, then up to seven short fields.</summary>
    private static IEnumerable<FieldModel> Shown(EntityModel e) =>
        e.Fields.Where(f => f.IsKey).Concat(e.Fields.Where(f => !f.IsKey && f.EditorKind is not ("multiline" or "password") && f.Type != "byte[]").Take(7));

    private static string Grid(EntityModel e, List<FieldModel> shown, bool editable)
    {
        var name = Identifiers.Camel(e.Plural).TrimStart('@') + "Grid";
        var sb = new StringBuilder();
        sb.Append($"<DataGrid x:Name=\"{name}\" ItemsSource=\"{{Binding Items}}\" SelectedItem=\"{{Binding Selected, Mode=TwoWay}}\" AutoGenerateColumns=\"False\" IsReadOnly=\"{(editable ? "False" : "True")}\" CanUserResizeColumns=\"True\" GridLinesVisibility=\"Horizontal\">\n");
        sb.Append("  <DataGrid.Columns>\n");
        foreach (var f in shown)
        {
            var format = f.Type is "DateTime" or "DateOnly" or "DateTimeOffset" ? ", StringFormat={}{0:d}" : f.EditorKind == "money" ? ", StringFormat={}{0:N2}" : "";
            sb.Append($"    <DataGridTextColumn Header=\"{FormXaml.Escape(f.DisplayLabel)}\" Binding=\"{{Binding {f.Name}{format}}}\" />\n");
        }
        sb.Append("  </DataGrid.Columns>\n</DataGrid>");
        return sb.ToString();
    }

    private static string Indent(string text, string pad) => string.Join('\n', text.Split('\n').Select(l => l.Length == 0 ? l : pad + l));

    // ───────────────────────────── the model ─────────────────────────────

    /// <summary>The model class: an observable object with one property per field, so edits show in the list at once.</summary>
    public static string Model(EntityModel e, CrudNames n)
    {
        var sb = new StringBuilder();
        sb.Append($$"""
            // Generated by AVAUIDesigner from {{e.Source}}.
            using CommunityToolkit.Mvvm.ComponentModel;

            namespace {{n.ModelNamespace}};

            /// <summary>A row of {{e.TableName}}.</summary>
            public partial class {{n.ModelClass}} : ObservableObject
            {

            """);
        foreach (var f in e.Fields)
        {
            var type = ModelType(f);
            var init = type == "string" ? " = string.Empty" : "";
            sb.Append($"    /// <summary>{FormXaml.Escape(f.DisplayLabel)} ({f.ColumnName}{(f.IsKey ? ", key" : "")}{(f.Required ? ", required" : "")}{(f.MaxLength is { } m ? $", up to {m}" : "")}).</summary>\n");
            sb.Append($"    [ObservableProperty]\n    private {type} {Field(f)}{init};\n\n");
        }
        sb.Length -= 1;
        sb.Append("}\n");
        return sb.ToString();
    }

    /// <summary>The property type in a generated model: dates as DateTime and times as TimeSpan, which the date and time pickers bind to.</summary>
    internal static string ModelType(FieldModel f)
    {
        var t = f.Type switch { "DateOnly" => "DateTime", "TimeOnly" => "TimeSpan", _ => f.Type };
        var nullable = f.Nullable || !f.IsKey && f.EditorKind is "date" or "datetime" or "time" or "number" or "decimal" or "money" or "lookup";
        if (t == "byte[]") return "byte[]?";
        return t == "string" && !f.Nullable ? "string" : TypeMap.Declared(t, nullable || t == "string");
    }

    private static string Field(FieldModel f) => "_" + Identifiers.Camel(f.Name).TrimStart('@');

    // ───────────────────────────── the repository ─────────────────────────────

    /// <summary>The data access class: list (with a search), get, insert, update and delete, in the engine's SQL.</summary>
    public static string Repository(EntityModel e, CrudNames n, DbProvider provider, bool existingModel)
    {
        var d = SqlDialect.For(provider);
        var keys = e.Keys.ToList();
        if (keys.Count == 0) throw new ArgumentException($"{e.Name} has no key, so a saved row can't be found again to update or delete. Mark its key field (is_key), or describe the table again.");
        var table = d.QualifiedTable(e);
        var cols = e.Fields.ToList();
        var columns = string.Join(", ", cols.Select(f => d.Quote(f.ColumnName)));
        var writable = e.Fields.Where(f => !f.IsGenerated && !f.IsComputed).ToList();
        var updatable = writable.Where(f => !f.IsKey).ToList();
        var generatedKey = keys.Count == 1 && keys[0].IsGenerated ? keys[0] : null;
        var searchable = e.Fields.Where(f => f.Type == "string" && !f.IsKey && f.EditorKind is not "password").Take(4).ToList();
        var orderBy = string.Join(", ", (searchable.FirstOrDefault(f => f.Name.Contains("Name", StringComparison.OrdinalIgnoreCase)) is { } byName ? [byName] : keys).Select(f => d.Quote(f.ColumnName)));
        var keyWhere = string.Join(" AND ", keys.Select(k => $"{d.Quote(k.ColumnName)} = {d.Parameter("k_" + k.Name)}"));
        var model = n.ModelClass;
        var lookups = e.Fields.Where(f => f.EditorKind == "lookup").ToList();
        var sb = new StringBuilder();
        sb.Append($$"""
            // Generated by AVAUIDesigner from {{e.Source}}, for {{d.DisplayName}} ({{d.Package}}).
            // Every value goes in as a parameter: nothing a person types is ever spliced into the SQL.
            using System.Data;
            using System.Data.Common;
            using System.Globalization;
            using {{d.Namespace}};
            using {{n.ModelNamespace}};

            namespace {{n.DataNamespace}};

            /// <summary>Reads and writes {{e.TableName}}.</summary>
            public sealed class {{n.RepositoryClass}}(Func<DbConnection> connect)
            {
                public {{n.RepositoryClass}}(string connectionString) : this(() => new {{d.ConnectionClass}}(connectionString)) { }

                private const string Columns = "{{Cs(columns)}}";

                /// <summary>The rows, those whose text matches <paramref name="search"/> if it is given, at most <paramref name="limit"/>.</summary>
                public async Task<List<{{model}}>> ListAsync(string? search = null, int limit = 500, CancellationToken cancel = default)
                {
                    await using var c = await OpenAsync(cancel);
                    await using var cmd = c.CreateCommand();

            """);
        string Listing(string where) => Interpolated(d.Limit($"SELECT {ColumnsMark} FROM {table}{where} ORDER BY {orderBy}", LimitMark));
        if (searchable.Count > 0)
        {
            var where = string.Join(" OR ", searchable.Select(f => d.Contains(d.Quote(f.ColumnName), d.Parameter("search"))));
            var adds = d.Positional ? string.Concat(searchable.Select(_ => "Add(cmd, \"search\", \"%\" + search.Trim() + \"%\"); ")) : "Add(cmd, \"search\", \"%\" + search.Trim() + \"%\");";
            sb.Append($$"""
                        if (string.IsNullOrWhiteSpace(search))
                        {
                            cmd.CommandText = {{Listing("")}};
                        }
                        else
                        {
                            cmd.CommandText = {{Listing(" WHERE " + where)}};
                            {{adds}}
                        }

                """);
        }
        else
        {
            sb.Append($"""
                        _ = search;
                        cmd.CommandText = {Listing("")};

                """);
        }
        sb.Append($$"""
                    var list = new List<{{model}}>();
                    await using var r = await cmd.ExecuteReaderAsync(cancel);
                    while (await r.ReadAsync(cancel)) list.Add(Read(r));
                    return list;
                }

                /// <summary>One row by its key, or null.</summary>
                public async Task<{{model}}?> GetAsync({{string.Join(", ", keys.Select(k => $"{KeyType(k, existingModel)} {Identifiers.Camel(k.Name)}"))}}, CancellationToken cancel = default)
                {
                    await using var c = await OpenAsync(cancel);
                    await using var cmd = c.CreateCommand();
                    cmd.CommandText = "SELECT " + Columns + " FROM {{Cs(table)}} WHERE {{Cs(keyWhere)}}";
            {{string.Concat(keys.Select(k => $"        Add(cmd, \"k_{k.Name}\", {Identifiers.Camel(k.Name)});\n"))}}        await using var r = await cmd.ExecuteReaderAsync(cancel);
                    return await r.ReadAsync(cancel) ? Read(r) : null;
                }

                /// <summary>Adds a row{{(generatedKey != null ? $"; the {generatedKey.Name} the database gives it is set on <paramref name=\"item\"/>" : "")}}.</summary>
                public async Task InsertAsync({{model}} item, CancellationToken cancel = default)
                {
                    await using var c = await OpenAsync(cancel);
                    await using var cmd = c.CreateCommand();

            """);
        var insertCols = string.Join(", ", writable.Select(f => d.Quote(f.ColumnName)));
        var insertVals = string.Join(", ", writable.Select(f => d.Parameter(f.Name)));
        if (generatedKey != null && d.KeyByOutputParameter)
        {
            sb.Append($$"""
                        cmd.CommandText = "INSERT INTO {{Cs(table)}} ({{Cs(insertCols)}}) VALUES ({{Cs(insertVals)}}) RETURNING {{Cs(d.Quote(generatedKey.ColumnName))}} INTO {{d.Parameter("NewKey")}}";
                {{string.Concat(writable.Select(f => $"        Add(cmd, \"{f.Name}\", item.{f.Name});\n"))}}        var key = cmd.CreateParameter();
                        key.ParameterName = "{{d.ParameterName("NewKey")}}";
                        key.DbType = DbType.Decimal;
                        key.Direction = ParameterDirection.Output;
                        cmd.Parameters.Add(key);
                        await cmd.ExecuteNonQueryAsync(cancel);
                        item.{{generatedKey.Name}} = Convert<{{KeyType(generatedKey, existingModel)}}>(key.Value);
                    }

                """);
        }
        else if (generatedKey != null && d.Provider != DbProvider.Odbc)
        {
            sb.Append($$"""
                        cmd.CommandText = "{{Cs(d.InsertReturningKey(table, insertCols, insertVals, generatedKey.ColumnName))}}";
                {{string.Concat(writable.Select(f => $"        Add(cmd, \"{f.Name}\", item.{f.Name});\n"))}}        item.{{generatedKey.Name}} = Convert<{{KeyType(generatedKey, existingModel)}}>(await cmd.ExecuteScalarAsync(cancel));
                    }

                """);
        }
        else
        {
            sb.Append($$"""
                        cmd.CommandText = "INSERT INTO {{Cs(table)}} ({{Cs(insertCols)}}) VALUES ({{Cs(insertVals)}})";
                {{string.Concat(writable.Select(f => $"        Add(cmd, \"{f.Name}\", item.{f.Name});\n"))}}        await cmd.ExecuteNonQueryAsync(cancel);{{(generatedKey != null ? $"\n        // This ODBC source has no standard way to hand back the new key: reload the list to see {generatedKey.Name}." : "")}}
                    }

                """);
        }
        if (updatable.Count > 0)
        {
            var set = string.Join(", ", updatable.Select(f => $"{d.Quote(f.ColumnName)} = {d.Parameter(f.Name)}"));
            sb.Append($$"""
                    /// <summary>Saves changes to a row; false if it was not there (another person deleted it).</summary>
                    public async Task<bool> UpdateAsync({{model}} item, CancellationToken cancel = default)
                    {
                        await using var c = await OpenAsync(cancel);
                        await using var cmd = c.CreateCommand();
                        cmd.CommandText = "UPDATE {{Cs(table)}} SET {{Cs(set)}} WHERE {{Cs(keyWhere)}}";
                {{string.Concat(updatable.Select(f => $"        Add(cmd, \"{f.Name}\", item.{f.Name});\n"))}}{{string.Concat(keys.Select(k => $"        Add(cmd, \"k_{k.Name}\", item.{k.Name});\n"))}}        return await cmd.ExecuteNonQueryAsync(cancel) > 0;
                    }

                """);
        }
        else
        {
            sb.Append($"""
                    /// <summary>{model} has nothing but its key, so there is nothing to update.</summary>
                    public Task<bool> UpdateAsync({model} item, CancellationToken cancel = default) => Task.FromResult(item is not null);

                """);
        }
        sb.Append($$"""
                /// <summary>Removes a row; false if it was already gone.</summary>
                public async Task<bool> DeleteAsync({{model}} item, CancellationToken cancel = default)
                {
                    await using var c = await OpenAsync(cancel);
                    await using var cmd = c.CreateCommand();
                    cmd.CommandText = "DELETE FROM {{Cs(table)}} WHERE {{Cs(keyWhere)}}";
            {{string.Concat(keys.Select(k => $"        Add(cmd, \"k_{k.Name}\", item.{k.Name});\n"))}}        return await cmd.ExecuteNonQueryAsync(cancel) > 0;
                }

            """);
        foreach (var f in lookups)
        {
            var refTable = string.IsNullOrWhiteSpace(f.ReferencesSchema) ? d.Quote(f.References!) : $"{d.Quote(f.ReferencesSchema!)}.{d.Quote(f.References!)}";
            sb.Append($$"""
                    /// <summary>The rows of {{f.References}} to choose {{FormXaml.Escape(f.DisplayLabel.ToLowerInvariant())}} from.</summary>
                    public async Task<List<LookupItem>> {{f.Name}}LookupAsync(CancellationToken cancel = default)
                    {
                        await using var c = await OpenAsync(cancel);
                        await using var cmd = c.CreateCommand();
                        cmd.CommandText = "{{Cs(d.Limit($"SELECT {d.Quote(f.ReferencesKey!)}, {d.Quote(f.ReferencesDisplay!)} FROM {refTable} ORDER BY {d.Quote(f.ReferencesDisplay!)}", 1000))}}";
                        var list = new List<LookupItem>();
                        await using var r = await cmd.ExecuteReaderAsync(cancel);
                        while (await r.ReadAsync(cancel)) list.Add(new LookupItem(Value<{{KeyType(f, existingModel)}}>(r, 0), System.Convert.ToString(r.GetValue(1), CultureInfo.CurrentCulture) ?? ""));
                        return list;
                    }

                """);
        }
        sb.Append($$"""
                private async Task<DbConnection> OpenAsync(CancellationToken cancel)
                {
                    var c = connect();
                    await c.OpenAsync(cancel);
                    return c;
                }

                private static {{model}} Read(DbDataReader r) => new()
                {
            {{string.Concat(cols.Select((f, i) => $"        {f.Name} = Value<{ReadType(f, existingModel)}>(r, {i}){(ReadType(f, existingModel) == "string" ? " ?? string.Empty" : "")},\n"))}}    };

                private static void Add(DbCommand cmd, string name, object? value)
                {
                    {{(provider == DbProvider.Oracle ? "if (cmd is OracleCommand oracle) oracle.BindByName = true;\n        " : "")}}var p = cmd.CreateParameter();
                    p.ParameterName = {{(d.Positional ? "name" : $"\"{d.ParameterPrefix}\" + name")}};
                    p.Value = value switch
                    {
                        null => DBNull.Value,
                        DateOnly date => date.ToDateTime(TimeOnly.MinValue),
                        TimeOnly time => time.ToTimeSpan(),
                        Enum e => System.Convert.ToInt32(e, CultureInfo.InvariantCulture),
            {{(provider == DbProvider.Oracle ? "            bool b => b ? 1 : 0,\n            Guid g => g.ToByteArray(),\n" : "")}}            string s when s.Length == 0 => DBNull.Value,
                        _ => value,
                    };
                    cmd.Parameters.Add(p);
                }

                private static T? Value<T>(DbDataReader r, int i) => r.IsDBNull(i) ? default : Convert<T>(r.GetValue(i));

                /// <summary>A value as the database gave it, as the type the model wants (engines differ: Oracle's numbers are decimals, SQLite's dates are text).</summary>
                private static T? Convert<T>(object? value)
                {
                    if (value is null or DBNull) return default;
                    if (value is T same) return same;
                    var t = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                    object result = t switch
                    {
                        _ when t.IsEnum => value is string name ? Enum.Parse(t, name, true) : Enum.ToObject(t, System.Convert.ToInt64(value, CultureInfo.InvariantCulture)),
                        _ when t == typeof(DateOnly) => DateOnly.FromDateTime(System.Convert.ToDateTime(value, CultureInfo.InvariantCulture)),
                        _ when t == typeof(TimeOnly) => value is TimeSpan ts ? TimeOnly.FromTimeSpan(ts) : TimeOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture),
                        _ when t == typeof(TimeSpan) => value is TimeSpan span ? span : value is DateTime dt ? dt.TimeOfDay : TimeSpan.Parse(value.ToString()!, CultureInfo.InvariantCulture),
                        _ when t == typeof(DateTime) => value is DateTimeOffset dto ? dto.DateTime : value is DateOnly d ? d.ToDateTime(TimeOnly.MinValue) : System.Convert.ToDateTime(value, CultureInfo.InvariantCulture),
                        _ when t == typeof(Guid) => value is byte[] bytes ? new Guid(bytes) : Guid.Parse(value.ToString()!),
                        _ when t == typeof(bool) => value is string s ? s is "1" or "Y" or "y" or "true" or "True" or "TRUE" : System.Convert.ToBoolean(value, CultureInfo.InvariantCulture),
                        _ when t == typeof(string) => System.Convert.ToString(value, CultureInfo.InvariantCulture)!,
                        _ => System.Convert.ChangeType(value, t, CultureInfo.InvariantCulture),
                    };
                    return (T)result;
                }
            }

            """);
        return sb.ToString();
    }

    /// <summary>The lookup row type the repositories share (one file per app, Data/LookupItem.cs).</summary>
    public const string LookupFileName = "LookupItem.cs";

    public static string LookupItem(CrudNames n) => $$"""
        // Generated by AVAUIDesigner.
        namespace {{n.DataNamespace}};

        /// <summary>A row of another table, for a choice list: its key and what people know it by.</summary>
        public sealed record LookupItem(object? Id, string Name);

        """;

    private static string KeyType(FieldModel f, bool existingModel) => existingModel ? f.Type : ModelType(f).TrimEnd('?');

    private const string ColumnsMark = "\u0001";
    private const int LimitMark = 987654321;

    /// <summary>SQL with the columns and the row limit filled in at run time, as a C# interpolated string.</summary>
    private static string Interpolated(string sql) =>
        "$\"" + Cs(sql).Replace("{", "{{").Replace("}", "}}").Replace(ColumnsMark, "{Columns}").Replace(LimitMark.ToString(), "{limit}") + "\"";

    private static string ReadType(FieldModel f, bool existingModel) =>
        existingModel ? (f.EnumType is { } en ? en + (f.Nullable ? "?" : "") : f.CSharpType) : ModelType(f);

    /// <summary>Text for inside a C# string literal.</summary>
    private static string Cs(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ───────────────────────────── the view model ─────────────────────────────

    /// <summary>The screen's view model: the list, the chosen row, search, and the commands, with checks before saving.</summary>
    public static string ViewModel(EntityModel e, CrudNames n, bool existingModel)
    {
        var model = n.ModelClass;
        var keys = e.Keys.ToList();
        var generatedKey = keys.Count == 1 && keys[0].IsGenerated ? keys[0] : null;
        var options = e.Fields.Where(f => f.EditorKind == "choice" && f.Options is { Count: > 0 }).ToList();
        var lookups = e.Fields.Where(f => f.EditorKind == "lookup").ToList();
        var label = e.DisplayLabel.ToLowerInvariant();
        var checks = new StringBuilder();
        foreach (var f in e.Editable)
        {
            var type = existingModel ? f.CSharpType : ModelType(f);
            if (f.Required)
            {
                if (type.StartsWith("string", StringComparison.Ordinal))
                    checks.Append($"        if (string.IsNullOrWhiteSpace(item.{f.Name})) problems.Add(\"{f.DisplayLabel} is required\");\n");
                else if (type.EndsWith('?'))
                    checks.Append($"        if (item.{f.Name} is null) problems.Add(\"{f.DisplayLabel} is required\");\n");
            }
            if (f.MaxLength is { } max && type.StartsWith("string", StringComparison.Ordinal))
                checks.Append($"        if (item.{f.Name}?.Length > {max}) problems.Add(\"{f.DisplayLabel} is at most {max} characters\");\n");
            if (f.Min is { } min && TypeMap.IsNumber(f.Type))
                checks.Append($"        if (item.{f.Name} < {min.ToString(System.Globalization.CultureInfo.InvariantCulture)}) problems.Add(\"{f.DisplayLabel} is at least {min}\");\n");
            if (f.Max is { } hi && TypeMap.IsNumber(f.Type))
                checks.Append($"        if (item.{f.Name} > {hi.ToString(System.Globalization.CultureInfo.InvariantCulture)}) problems.Add(\"{f.DisplayLabel} is at most {hi}\");\n");
            if (f.EditorKind == "email" && type.StartsWith("string", StringComparison.Ordinal))
                checks.Append($"        if (!string.IsNullOrWhiteSpace(item.{f.Name}) && !item.{f.Name}.Contains('@')) problems.Add(\"{f.DisplayLabel} isn't an email address\");\n");
        }
        var sb = new StringBuilder();
        sb.Append($$"""
            // Generated by AVAUIDesigner from {{e.Source}}. Yours to change: saving the view again only adds what is missing.
            using System.Collections.ObjectModel;
            using CommunityToolkit.Mvvm.ComponentModel;
            using CommunityToolkit.Mvvm.Input;
            using {{n.DataNamespace}};
            using {{n.ModelNamespace}};

            namespace {{n.ViewModelNamespace}};

            /// <summary>The {{FormXaml.Escape(Words.FromIdentifier(e.Plural).ToLowerInvariant())}} screen: a list to search, and the chosen {{FormXaml.Escape(label)}} to edit, add, save or delete.</summary>
            public partial class {{n.ViewModelClass}} : ObservableObject
            {
                private readonly {{n.RepositoryClass}}? _repository;
                private readonly HashSet<{{model}}> _unsaved = [];
                private {{model}}? _deleteAsked;

                /// <summary>For the designer's preview: no database.</summary>
                public {{n.ViewModelClass}}() { }

                public {{n.ViewModelClass}}({{n.RepositoryClass}} repository)
                {
                    _repository = repository;
                    _ = Load();
                }

                [ObservableProperty]
                private ObservableCollection<{{model}}> _items = [];

                [ObservableProperty]
                [NotifyPropertyChangedFor(nameof(HasSelection))]
                private {{model}}? _selected;

                [ObservableProperty]
                private string _searchText = string.Empty;

                [ObservableProperty]
                private string _status = string.Empty;

                [ObservableProperty]
                private bool _isBusy;

                public bool HasSelection => Selected is not null;


            """);
        foreach (var f in options)
            sb.Append(OptionsMember(f, existingModel));
        foreach (var f in lookups)
            sb.Append($"    [ObservableProperty]\n    private List<LookupItem> _{Identifiers.Camel(f.Name).TrimStart('@')}Lookup = [];\n\n");
        sb.Append($$"""
                [RelayCommand]
                private async Task Load()
                {
                    if (_repository is null) return;
                    await Run("Loading", async () =>
                    {
            {{string.Concat(lookups.Select(f => $"            {f.Name}Lookup = await _repository.{f.Name}LookupAsync();\n"))}}                Items = new ObservableCollection<{{model}}>(await _repository.ListAsync(SearchText));
                        _unsaved.Clear();
                        Status = Items.Count == 1 ? "1 {{Esc(label)}}" : $"{Items.Count} {{Esc(Words.FromIdentifier(e.Plural).ToLowerInvariant())}}";
                    });
                }

                [RelayCommand]
                private Task Search() => Load();

                [RelayCommand]
                private void New()
                {
                    var item = new {{model}}();
                    _unsaved.Add(item);
                    Items.Insert(0, item);
                    Selected = item;
                    Status = "New {{Esc(label)}}: fill it in and Save.";
                }

                [RelayCommand]
                private async Task Save()
                {
                    if (Selected is not { } item) return;
                    var problems = Check(item);
                    if (problems.Count > 0)
                    {
                        Status = "Not saved: " + string.Join("; ", problems) + ".";
                        return;
                    }
                    if (_repository is null) { Status = "No database in the designer's preview."; return; }
                    await Run("Saving", async () =>
                    {
                        if (_unsaved.Contains(item))
                        {
                            await _repository.InsertAsync(item);
                            _unsaved.Remove(item);
                            Status = "Added.";
                        }
                        else
                        {
                            Status = await _repository.UpdateAsync(item) ? "Saved." : "Not saved: it has been deleted by someone else. Refresh the list.";
                        }
                    });
                }

                [RelayCommand]
                private async Task Delete()
                {
                    if (Selected is not { } item) return;
                    if (_unsaved.Remove(item))
                    {
                        Items.Remove(item);
                        Status = "New {{Esc(label)}} discarded.";
                        return;
                    }
                    // Deleting a record is not undone: the first click asks, the second deletes.
                    if (!ReferenceEquals(_deleteAsked, item))
                    {
                        _deleteAsked = item;
                        Status = "Click Delete again to delete this {{Esc(label)}} for good.";
                        return;
                    }
                    _deleteAsked = null;
                    if (_repository is null) return;
                    await Run("Deleting", async () =>
                    {
                        await _repository.DeleteAsync(item);
                        Items.Remove(item);
                        Selected = null;
                        Status = "Deleted.";
                    });
                }

                partial void OnSelectedChanged({{model}}? value) => _deleteAsked = null;

                /// <summary>What stops a {{FormXaml.Escape(label)}} being saved, in words for the status line.</summary>
                public static List<string> Check({{model}} item)
                {
                    var problems = new List<string>();
            {{checks}}        return problems;
                }

                private async Task Run(string doing, Func<Task> work)
                {
                    IsBusy = true;
                    Status = doing + "…";
                    try
                    {
                        await work();
                    }
                    catch (Exception e)
                    {
                        Status = doing + " failed: " + e.Message;
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            }

            """);
        _ = generatedKey;
        return sb.ToString();
    }

    /// <summary>A choice field's list for its drop-down: the enum's values for an enum in the person's class, else the texts.</summary>
    public static string OptionsMember(FieldModel f, bool existingModel) =>
        existingModel && f.EnumType is { } en
            ? $"    public IReadOnlyList<{en}> {f.Name}Options {{ get; }} = Enum.GetValues<{en}>();\n\n"
            : $"    public IReadOnlyList<string> {f.Name}Options {{ get; }} = [{string.Join(", ", f.Options!.Select(o => "\"" + o.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""))}];\n\n";

    private static string Esc(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
