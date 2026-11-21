using System.Globalization;
using System.Security;
using System.Text;

namespace AVAUIDesigner.Data;

/// <summary>Which control edits a field.</summary>
public static class FormEditors
{
    public static readonly string[] Kinds =
        ["text", "multiline", "email", "phone", "password", "date", "time", "datetime", "number", "decimal", "money", "check", "choice", "lookup", "readonly"];

    public static string Kind(FieldModel f)
    {
        if (!string.IsNullOrWhiteSpace(f.Editor))
        {
            var e = f.Editor.Trim().ToLowerInvariant();
            if (!Kinds.Contains(e)) throw new ArgumentException($"{f.Name}: \"{f.Editor}\" isn't an editor. Use one of {string.Join(", ", Kinds)}.");
            return e;
        }
        if (f.IsKey && f.IsGenerated || f.IsComputed) return "readonly";
        if (f.Options is { Count: > 0 }) return "choice";
        if (f.References != null && f.ReferencesKey != null) return "lookup";
        var lower = f.Name.ToLowerInvariant();
        return f.Type switch
        {
            "bool" => "check",
            "DateOnly" => "date",
            "DateTime" or "DateTimeOffset" => lower.EndsWith("date", StringComparison.Ordinal) || lower.Contains("birth") ? "date" : "datetime",
            "TimeOnly" or "TimeSpan" => "time",
            "int" or "long" or "short" or "byte" => "number",
            "decimal" when lower.Contains("price") || lower.Contains("amount") || lower.Contains("cost") || lower.Contains("charge") || lower.Contains("balance") || lower.Contains("fee") => "money",
            "decimal" or "double" or "float" => "decimal",
            "string" when lower.Contains("email") => "email",
            "string" when lower.Contains("phone") || lower.Contains("fax") || lower.Contains("mobile") => "phone",
            "string" when lower.Contains("password") => "password",
            "string" when f.MaxLength is > 255 || f.MaxLength is null && (lower.Contains("note") || lower.Contains("comment") || lower.Contains("description") || lower.Contains("narrative") || lower.Contains("instruction")) => "multiline",
            _ => "text",
        };
    }
}

/// <summary>Options for a form's XAML.</summary>
public sealed class FormOptions
{
    /// <summary>Put before each binding path ("Selected." binds to the list's chosen row); empty binds to the view model itself.</summary>
    public string BindingPrefix { get; init; } = "";

    /// <summary>Bind the editors at all (a mock-up has no view model).</summary>
    public bool Bind { get; init; } = true;

    /// <summary>One or two label-and-editor pairs across. Wide editors (notes) take a whole row.</summary>
    public int Columns { get; init; } = 1;

    /// <summary>Starts every x:Name, so two forms on one screen don't clash ("patient").</summary>
    public string NamePrefix { get; init; } = "";

    /// <summary>The form's own x:Name.</summary>
    public string? Name { get; init; }

    /// <summary>Choice fields get their items from "{Field}Options" on the view model, rather than items written in.</summary>
    public bool OptionsFromViewModel { get; init; }

    /// <summary>
    /// The class holding the date and time converters ("vm:Converters"), for models whose dates are DateOnly and times
    /// TimeOnly (the pickers bind DateTime and TimeSpan). Null: the model already uses DateTime and TimeSpan.
    /// </summary>
    public string? ConvertersClass { get; init; }
}

/// <summary>Builds XAML for forms: a grid of labels and editors, one row per field (or two fields a row).</summary>
public static class FormXaml
{
    public static string Escape(string text) => SecurityElement.Escape(text) ?? "";

    /// <summary>A form's grid, as a XAML fragment in the default Avalonia namespace.</summary>
    public static string Form(IEnumerable<FieldModel> fields, FormOptions o)
    {
        var list = fields.ToList();
        if (list.Count == 0) throw new ArgumentException("A form needs at least one field.");
        var cols = Math.Clamp(o.Columns, 1, 2);
        var columnDefs = cols == 1 ? "Auto,*" : "Auto,*,24,Auto,*";
        var rows = new List<List<FieldModel>>();
        foreach (var f in list)
        {
            var wide = f.EditorKind == "multiline" || cols == 1;
            if (wide || rows.Count == 0 || rows[^1].Count >= cols || rows[^1].Any(x => x.EditorKind == "multiline"))
                rows.Add([f]);
            else
                rows[^1].Add(f);
            if (wide && cols > 1 && f.EditorKind == "multiline") rows.Add([]);
        }
        rows.RemoveAll(r => r.Count == 0);
        var sb = new StringBuilder();
        var name = o.Name is { Length: > 0 } n ? $" x:Name=\"{Identifiers.Safe(n)}\"" : "";
        sb.Append($"<Grid{name} ColumnDefinitions=\"{columnDefs}\" RowDefinitions=\"{string.Join(",", rows.Select(_ => "Auto"))}\">\n");
        for (var r = 0; r < rows.Count; r++)
            for (var i = 0; i < rows[r].Count; i++)
            {
                var f = rows[r][i];
                var labelCol = i * 3;
                var span = cols > 1 && f.EditorKind == "multiline" ? 4 : 1;
                if (f.EditorKind != "check")
                    sb.Append($"  <TextBlock Grid.Row=\"{r}\" Grid.Column=\"{labelCol}\" Text=\"{Escape(f.DisplayLabel + (f.Required ? " *" : ""))}\" VerticalAlignment=\"{(f.EditorKind == "multiline" ? "Top" : "Center")}\" Margin=\"0,6,12,6\" />\n");
                sb.Append("  ").Append(Editor(f, o, r, labelCol + 1, span, f.EditorKind == "check" ? labelCol : -1)).Append('\n');
            }
        sb.Append("</Grid>");
        return sb.ToString();
    }

    /// <summary>One field's editor element.</summary>
    public static string Editor(FieldModel f, FormOptions o, int row, int column, int span = 1, int checkColumn = -1)
    {
        var kind = f.EditorKind;
        var path = o.BindingPrefix + f.Name;
        var name = Identifiers.Camel(o.NamePrefix.Length > 0 ? o.NamePrefix + f.Name : f.Name).TrimStart('@') + Suffix(kind);
        var place = $"x:Name=\"{name}\" Grid.Row=\"{row}\" Grid.Column=\"{(checkColumn >= 0 ? checkColumn : column)}\"" +
                    (checkColumn >= 0 ? " Grid.ColumnSpan=\"2\"" : span > 1 ? $" Grid.ColumnSpan=\"{span}\"" : "");
        var converter = o.ConvertersClass is { } cc && f.Type is "DateOnly" or "TimeOnly" ? $", Converter={{x:Static {cc}.{f.Type}}}" : "";
        string B(string property, bool twoWay = true) => o.Bind ? $" {property}=\"{{Binding {path}{(twoWay ? ", Mode=TwoWay" : "")}{converter}}}\"" : "";
        var margin = " Margin=\"0,4\"";
        var tip = f.MaxLength is { } max ? $" MaxLength=\"{max}\"" : "";
        return kind switch
        {
            "multiline" => $"<TextBox {place}{B("Text")} AcceptsReturn=\"True\" TextWrapping=\"Wrap\" Height=\"88\"{tip}{margin} />",
            "email" => $"<TextBox {place}{B("Text")} Watermark=\"name@example.org\"{tip}{margin} />",
            "phone" => $"<TextBox {place}{B("Text")} Watermark=\"(555) 555-0100\"{tip}{margin} />",
            "password" => $"<TextBox {place}{B("Text")} PasswordChar=\"•\"{tip}{margin} />",
            "date" or "datetime" => $"<CalendarDatePicker {place}{B("SelectedDate")} Watermark=\"mm/dd/yyyy\" HorizontalAlignment=\"Left\" Width=\"180\"{margin} />",
            "time" => $"<TimePicker {place}{B("SelectedTime")}{margin} />",
            "number" => $"<NumericUpDown {place}{B("Value")} FormatString=\"0\" Increment=\"1\"{Limits(f)} HorizontalAlignment=\"Left\" Width=\"180\"{margin} />",
            "decimal" => $"<NumericUpDown {place}{B("Value")} FormatString=\"0.##\"{Limits(f)} HorizontalAlignment=\"Left\" Width=\"180\"{margin} />",
            "money" => $"<NumericUpDown {place}{B("Value")} FormatString=\"0.00\" Increment=\"1\"{Limits(f, 0)} HorizontalAlignment=\"Left\" Width=\"180\"{margin} />",
            "check" => $"<CheckBox {place}{B("IsChecked")} Content=\"{Escape(f.DisplayLabel)}\"{margin} />",
            "choice" when o.Bind && o.OptionsFromViewModel =>
                $"<ComboBox {place} ItemsSource=\"{{Binding {f.Name}Options}}\"{B("SelectedItem")} PlaceholderText=\"Choose…\" MinWidth=\"180\"{margin} />",
            "choice" => $"<ComboBox {place}{B("SelectedItem")} PlaceholderText=\"Choose…\" MinWidth=\"180\"{margin}>\n" +
                        string.Concat((f.Options ?? []).Select(x => $"    <ComboBoxItem Content=\"{Escape(x)}\" />\n")) + "  </ComboBox>",
            "lookup" when o.Bind =>
                $"<ComboBox {place} ItemsSource=\"{{Binding {f.Name}Lookup}}\" SelectedValueBinding=\"{{Binding Id}}\" DisplayMemberBinding=\"{{Binding Name}}\" SelectedValue=\"{{Binding {path}, Mode=TwoWay}}\" PlaceholderText=\"Choose…\" MinWidth=\"180\"{margin} />",
            "lookup" => $"<ComboBox {place} PlaceholderText=\"Choose…\" MinWidth=\"180\"{margin} />",
            "readonly" => $"<TextBlock {place}{(o.Bind ? $" Text=\"{{Binding {path}}}\"" : " Text=\"(assigned when saved)\"")} VerticalAlignment=\"Center\" Opacity=\"0.8\"{margin} />",
            _ => $"<TextBox {place}{B("Text")}{tip}{margin} />",
        };
    }

    private static string Suffix(string kind) => kind switch
    {
        "date" or "datetime" => "Picker",
        "time" => "Picker",
        "number" or "decimal" or "money" => "Box",
        "check" => "Check",
        "choice" or "lookup" => "Combo",
        "readonly" => "Text",
        _ => "Box",
    };

    private static string Limits(FieldModel f, double? defaultMin = null)
    {
        var s = "";
        var min = f.Min ?? defaultMin;
        if (min is { } a) s += $" Minimum=\"{a.ToString(CultureInfo.InvariantCulture)}\"";
        if (f.Max is { } b) s += $" Maximum=\"{b.ToString(CultureInfo.InvariantCulture)}\"";
        return s;
    }
}
