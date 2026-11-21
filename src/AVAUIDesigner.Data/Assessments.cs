using System.Globalization;
using System.Text;

namespace AVAUIDesigner.Data;

/// <summary>A rating scale's point: its value and what it means ("0", "Not at all").</summary>
public sealed record ScalePoint(int Value, string Label);

/// <summary>A score band: totals from Min to Max mean Label ("10–14: Moderate").</summary>
public sealed record ScoreBand(int Min, int Max, string Label);

public sealed class AssessmentSection
{
    public string Title { get; set; } = "";

    /// <summary>The items, in order.</summary>
    public List<string> Items { get; set; } = [];

    /// <summary>Strengths, not needs: a low rating is the good one (CANS strengths: 0 is a centrepiece strength).</summary>
    public bool Strengths { get; set; }

    /// <summary>This section's own scale, if not the assessment's.</summary>
    public List<ScalePoint>? Scale { get; set; }
}

/// <summary>
/// A rated assessment: sections of items each rated on a scale, a total or a count of items needing action, and
/// what the result means. The built-in instruments are public-domain screening tools and a CANS core set; any other
/// is described the same way.
/// </summary>
public sealed class AssessmentDefinition
{
    /// <summary>A short name, used for its controls and view-model members ("Phq9", "Cans").</summary>
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Instructions { get; set; }
    public List<ScalePoint> Scale { get; set; } = [];
    public List<AssessmentSection> Sections { get; set; } = [];

    /// <summary>"sum": a total, read against <see cref="Bands"/>. "action": a count of items rated at or above <see cref="ActionAt"/>.</summary>
    public string Scoring { get; set; } = "sum";
    public List<ScoreBand> Bands { get; set; } = [];
    public int ActionAt { get; set; } = 2;

    /// <summary>Items (numbered from 1 across the sections) that need follow-up whenever rated above 0, with what to say.</summary>
    public Dictionary<int, string> Alerts { get; set; } = [];

    /// <summary>Where it comes from and on what terms it may be used.</summary>
    public string? Source { get; set; }

    public IEnumerable<(int Number, AssessmentSection Section, string Text)> AllItems()
    {
        var n = 0;
        foreach (var s in Sections)
            foreach (var item in s.Items)
                yield return (++n, s, item);
    }

    public int ItemCount => Sections.Sum(s => s.Items.Count);

    public AssessmentDefinition Normalise()
    {
        Key = Identifiers.Pascal(string.IsNullOrWhiteSpace(Key) ? Title : Key);
        if (string.IsNullOrWhiteSpace(Title)) Title = Words.FromIdentifier(Key);
        if (Scale.Count < 2) throw new ArgumentException("An assessment's scale needs at least two points (value and label).");
        if (ItemCount == 0) throw new ArgumentException("An assessment needs at least one item.");
        if (ItemCount > 200) throw new ArgumentException("An assessment is up to 200 items.");
        if (Scoring is not ("sum" or "action")) throw new ArgumentException("scoring is \"sum\" (a total read against bands) or \"action\" (a count of items needing action).");
        return this;
    }
}

/// <summary>The built-in instruments, and the XAML and view-model code for any assessment.</summary>
public static class Assessments
{
    private static readonly List<ScalePoint> Frequency =
        [new(0, "Not at all"), new(1, "Several days"), new(2, "More than half the days"), new(3, "Nearly every day")];

    private static readonly List<ScalePoint> CansNeeds =
        [new(0, "No evidence of need"), new(1, "Watch or prevent"), new(2, "Action needed"), new(3, "Immediate or intensive action")];

    private static readonly List<ScalePoint> CansStrengths =
        [new(0, "Centrepiece strength"), new(1, "Useful strength"), new(2, "Identified strength"), new(3, "Not yet identified")];

    public static readonly string[] BuiltIn = ["phq9", "gad7", "cans"];

    public static AssessmentDefinition Named(string name) => name.Trim().ToLowerInvariant().Replace("-", "").Replace(" ", "").Replace("_", "") switch
    {
        "phq9" => Phq9(),
        "gad7" => Gad7(),
        "cans" => Cans(),
        _ => throw new ArgumentException($"\"{name}\" isn't a built-in assessment. The built-in ones: phq9, gad7, cans. For another, describe it: its scale, sections and items."),
    };

    public static AssessmentDefinition Phq9() => new AssessmentDefinition
    {
        Key = "Phq9",
        Title = "Patient Health Questionnaire (PHQ-9)",
        Instructions = "Over the last 2 weeks, how often have you been bothered by any of the following problems?",
        Scale = Frequency,
        Sections =
        [
            new()
            {
                Items =
                [
                    "Little interest or pleasure in doing things",
                    "Feeling down, depressed, or hopeless",
                    "Trouble falling or staying asleep, or sleeping too much",
                    "Feeling tired or having little energy",
                    "Poor appetite or overeating",
                    "Feeling bad about yourself, or that you are a failure or have let yourself or your family down",
                    "Trouble concentrating on things, such as reading the newspaper or watching television",
                    "Moving or speaking so slowly that other people could have noticed, or the opposite: being so fidgety or restless that you have been moving around a lot more than usual",
                    "Thoughts that you would be better off dead, or of hurting yourself in some way",
                ],
            },
        ],
        Bands = [new(0, 4, "Minimal"), new(5, 9, "Mild"), new(10, 14, "Moderate"), new(15, 19, "Moderately severe"), new(20, 27, "Severe")],
        Alerts = { [9] = "Item 9 answered above 0: follow the clinic's suicide-risk protocol now." },
        Source = "PHQ-9: Kroenke, Spitzer and Williams, developed with a grant from Pfizer; free to reproduce and use, no permission required.",
    }.Normalise();

    public static AssessmentDefinition Gad7() => new AssessmentDefinition
    {
        Key = "Gad7",
        Title = "Generalized Anxiety Disorder scale (GAD-7)",
        Instructions = "Over the last 2 weeks, how often have you been bothered by the following problems?",
        Scale = Frequency,
        Sections =
        [
            new()
            {
                Items =
                [
                    "Feeling nervous, anxious, or on edge",
                    "Not being able to stop or control worrying",
                    "Worrying too much about different things",
                    "Trouble relaxing",
                    "Being so restless that it is hard to sit still",
                    "Becoming easily annoyed or irritable",
                    "Feeling afraid, as if something awful might happen",
                ],
            },
        ],
        Bands = [new(0, 4, "Minimal"), new(5, 9, "Mild"), new(10, 14, "Moderate"), new(15, 21, "Severe")],
        Source = "GAD-7: Spitzer, Kroenke, Williams and Löwe, developed with a grant from Pfizer; free to reproduce and use, no permission required.",
    }.Normalise();

    /// <summary>A CANS core set (the items most versions share). Programs use their own versions: describe yours to match it exactly.</summary>
    public static AssessmentDefinition Cans() => new AssessmentDefinition
    {
        Key = "Cans",
        Title = "Child and Adolescent Needs and Strengths (CANS): core items",
        Instructions = "Rate each item from everything known about the child and family over the last 30 days. Needs: 0 no evidence, 1 watch or prevent, 2 action needed, 3 immediate or intensive action. Strengths: 0 centrepiece, 1 useful, 2 identified, 3 not yet identified.",
        Scale = CansNeeds,
        Scoring = "action",
        ActionAt = 2,
        Sections =
        [
            new() { Title = "Life functioning", Items = ["Family functioning", "Living situation", "Social functioning", "Developmental / intellectual", "Decision making", "School behavior", "School achievement", "School attendance", "Medical / physical", "Sleep"] },
            new() { Title = "Behavioral / emotional needs", Items = ["Psychosis", "Impulsivity / hyperactivity", "Depression", "Anxiety", "Oppositional behavior", "Conduct", "Adjustment to trauma", "Anger control", "Substance use"] },
            new() { Title = "Risk behaviors", Items = ["Suicide risk", "Non-suicidal self-injurious behavior", "Other self-harm (recklessness)", "Danger to others", "Sexual aggression", "Runaway", "Delinquent behavior", "Intentional misbehavior"] },
            new() { Title = "Strengths", Strengths = true, Scale = CansStrengths, Items = ["Family strengths", "Interpersonal", "Educational setting", "Talents and interests", "Spiritual / religious", "Cultural identity", "Community life", "Natural supports", "Resiliency"] },
            new() { Title = "Caregiver resources and needs", Items = ["Supervision", "Involvement with care", "Knowledge", "Social resources", "Residential stability", "Caregiver medical / physical", "Caregiver mental health", "Caregiver substance use", "Safety"] },
        ],
        Source = "CANS: Praed Foundation, an open-domain tool free to use; training and certification are required to rate it. Check the item set against your program's version.",
    }.Normalise();

    // ───────────────────────────── XAML ─────────────────────────────

    /// <summary>The assessment as a XAML fragment: a card with the instructions, a rating grid per section and the result.</summary>
    public static string Xaml(AssessmentDefinition a, bool bind, string convertersClass = "vm:Converters")
    {
        var key = a.Key;
        var low = Identifiers.Camel(key).TrimStart('@');
        var sb = new StringBuilder();
        sb.Append($"<Border x:Name=\"{low}Assessment\" BorderBrush=\"#40808080\" BorderThickness=\"1\" CornerRadius=\"6\" Padding=\"16\" Margin=\"0,0,0,12\">\n");
        sb.Append("  <StackPanel Spacing=\"8\">\n");
        sb.Append($"    <TextBlock Text=\"{FormXaml.Escape(a.Title)}\" FontSize=\"18\" FontWeight=\"SemiBold\" TextWrapping=\"Wrap\" />\n");
        if (!string.IsNullOrWhiteSpace(a.Instructions))
            sb.Append($"    <TextBlock Text=\"{FormXaml.Escape(a.Instructions)}\" TextWrapping=\"Wrap\" Opacity=\"0.85\" />\n");
        foreach (var (section, index) in a.Sections.Select((s, i) => (s, i)))
        {
            var scale = section.Scale ?? a.Scale;
            var hasTitle = !string.IsNullOrWhiteSpace(section.Title);
            var pad = hasTitle ? "        " : "    ";
            if (hasTitle)
                sb.Append($"    <Expander x:Name=\"{low}Section{index + 1}\" Header=\"{FormXaml.Escape(section.Title)}\" IsExpanded=\"True\" HorizontalAlignment=\"Stretch\">\n      <StackPanel>\n");
            var cols = "*," + string.Join(",", scale.Select(_ => "96"));
            var rows = string.Join(",", Enumerable.Repeat("Auto", section.Items.Count + 1));
            sb.Append($"{pad}<Grid x:Name=\"{low}Items{index + 1}\" ColumnDefinitions=\"{cols}\" RowDefinitions=\"{rows}\">\n");
            for (var c = 0; c < scale.Count; c++)
                sb.Append($"{pad}  <TextBlock Grid.Row=\"0\" Grid.Column=\"{c + 1}\" Text=\"{scale[c].Value} {FormXaml.Escape(scale[c].Label)}\" TextWrapping=\"Wrap\" TextAlignment=\"Center\" FontSize=\"11\" Opacity=\"0.8\" Margin=\"4,0,4,6\" />\n");
            var first = a.AllItems().First(x => x.Section == section).Number;
            for (var i = 0; i < section.Items.Count; i++)
            {
                var number = first + i;
                var item = section.Items[i];
                var row = i + 1;
                sb.Append($"{pad}  <TextBlock Grid.Row=\"{row}\" Grid.Column=\"0\" Text=\"{number}. {FormXaml.Escape(item)}\" TextWrapping=\"Wrap\" VerticalAlignment=\"Center\" Margin=\"0,4,8,4\" />\n");
                for (var c = 0; c < scale.Count; c++)
                {
                    var binding = bind ? $" IsChecked=\"{{Binding {key}Q{number}, Converter={{x:Static {convertersClass}.Score}}, ConverterParameter={scale[c].Value}}}\"" : "";
                    sb.Append($"{pad}  <RadioButton x:Name=\"{low}Q{number}_{scale[c].Value}\" Grid.Row=\"{row}\" Grid.Column=\"{c + 1}\" GroupName=\"{low}Q{number}\" HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\"{binding} />\n");
                }
            }
            sb.Append($"{pad}</Grid>\n");
            if (hasTitle) sb.Append("      </StackPanel>\n    </Expander>\n");
        }
        sb.Append($"    <StackPanel x:Name=\"{low}Result\" Orientation=\"Horizontal\" Spacing=\"12\" Margin=\"0,8,0,0\">\n");
        if (a.Scoring == "sum")
        {
            sb.Append("      <TextBlock Text=\"Total\" FontWeight=\"SemiBold\" VerticalAlignment=\"Center\" />\n");
            sb.Append($"      <TextBlock x:Name=\"{low}TotalText\" Text=\"{(bind ? $"{{Binding {key}Total, FallbackValue=0}}" : "0")}\" FontSize=\"18\" FontWeight=\"Bold\" VerticalAlignment=\"Center\" />\n");
        }
        else
        {
            sb.Append($"      <TextBlock Text=\"Items needing action ({a.ActionAt} or 3)\" FontWeight=\"SemiBold\" VerticalAlignment=\"Center\" />\n");
            sb.Append($"      <TextBlock x:Name=\"{low}ActionText\" Text=\"{(bind ? $"{{Binding {key}ActionCount, FallbackValue=0}}" : "0")}\" FontSize=\"18\" FontWeight=\"Bold\" VerticalAlignment=\"Center\" />\n");
        }
        sb.Append($"      <TextBlock x:Name=\"{low}ResultText\" Text=\"{(bind ? $"{{Binding {key}Result, FallbackValue='Not yet scored'}}" : "Not yet scored")}\" VerticalAlignment=\"Center\" TextWrapping=\"Wrap\" />\n");
        sb.Append("    </StackPanel>\n");
        if (a.Alerts.Count > 0)
            sb.Append($"    <TextBlock x:Name=\"{low}AlertText\" Text=\"{(bind ? $"{{Binding {key}Alert}}" : "")}\" Foreground=\"#C62828\" FontWeight=\"SemiBold\" TextWrapping=\"Wrap\" />\n");
        if (!string.IsNullOrWhiteSpace(a.Source))
            sb.Append($"    <TextBlock Text=\"{FormXaml.Escape(a.Source)}\" FontSize=\"10\" Opacity=\"0.6\" TextWrapping=\"Wrap\" />\n");
        sb.Append("  </StackPanel>\n</Border>");
        return sb.ToString();
    }

    // ───────────────────────────── view model ─────────────────────────────

    /// <summary>
    /// The view-model members an assessment binds to: one rating per item, the total or the count needing action, what
    /// the result means, and any alert. They go into the view's view model (CodeMerge adds them to an existing one).
    /// </summary>
    public static string Members(AssessmentDefinition a)
    {
        var key = a.Key;
        var items = a.AllItems().ToList();
        var dependents = a.Scoring == "sum"
            ? new[] { $"{key}Total", $"{key}Answered", $"{key}Result" }
            : new[] { $"{key}ActionCount", $"{key}Answered", $"{key}Result" };
        if (a.Alerts.Count > 0) dependents = [.. dependents, $"{key}Alert"];
        var sb = new StringBuilder();
        sb.Append($"    // ── {a.Title} ──\n\n");
        foreach (var (number, section, text) in items)
        {
            sb.Append($"    /// <summary>{number}. {FormXaml.Escape(text)}{(section.Strengths ? " (a strength)" : "")}</summary>\n");
            sb.Append("    [ObservableProperty]\n");
            foreach (var d in dependents) sb.Append($"    [NotifyPropertyChangedFor(nameof({d}))]\n");
            sb.Append($"    private int? _{Identifiers.Camel(key).TrimStart('@')}Q{number};\n\n");
        }
        var all = string.Join(", ", items.Select(i => $"{key}Q{i.Number}"));
        sb.Append($"    /// <summary>The ratings in item order (null: not answered).</summary>\n    public int?[] {key}Ratings => [{all}];\n\n");
        sb.Append($"    public int {key}Answered => {key}Ratings.Count(r => r is not null);\n\n");
        if (a.Scoring == "sum")
        {
            sb.Append($"    public int {key}Total => {key}Ratings.Sum(r => r ?? 0);\n\n");
            sb.Append($"    /// <summary>What the total means, once every item is answered.</summary>\n    public string {key}Result => {key}Answered < {items.Count} ? $\"{{{items.Count} - {key}Answered}} of {items.Count} not answered yet\" : {key}Total switch\n    {{\n");
            foreach (var b in a.Bands)
                sb.Append($"        >= {b.Min} and <= {b.Max} => \"{b.Label.Replace("\"", "\\\"")} ({b.Min}–{b.Max})\",\n");
            sb.Append("        _ => \"\",\n    };\n\n");
        }
        else
        {
            var needs = items.Where(i => !i.Section.Strengths).Select(i => i.Number).ToList();
            var strengths = items.Where(i => i.Section.Strengths).Select(i => i.Number).ToList();
            sb.Append($"    private static readonly string[] {key}ItemNames = [{string.Join(", ", items.Select(i => "\"" + i.Text.Replace("\"", "\\\"") + "\""))}];\n\n");
            sb.Append($"    /// <summary>Needs rated {a.ActionAt} or 3: each one belongs in the plan.</summary>\n");
            sb.Append($"    public int {key}ActionCount => new[] {{ {string.Join(", ", needs.Select(n => $"{key}Q{n}"))} }}.Count(r => r >= {a.ActionAt.ToString(CultureInfo.InvariantCulture)});\n\n");
            sb.Append($"    public string {key}Result\n    {{\n        get\n        {{\n");
            sb.Append($"            var needs = new[] {{ {string.Join(", ", needs.Select(n => n.ToString(CultureInfo.InvariantCulture)))} }}.Where(n => {key}Ratings[n - 1] >= {a.ActionAt}).Select(n => {key}ItemNames[n - 1]).ToList();\n");
            if (strengths.Count > 0)
                sb.Append($"            var useful = new[] {{ {string.Join(", ", strengths.Select(n => n.ToString(CultureInfo.InvariantCulture)))} }}.Where(n => {key}Ratings[n - 1] <= 1).Select(n => {key}ItemNames[n - 1]).ToList();\n");
            else
                sb.Append("            var useful = new List<string>();\n");
            sb.Append($"            var left = {items.Count} - {key}Answered;\n");
            sb.Append("            var parts = new List<string>();\n");
            sb.Append("            if (left > 0) parts.Add($\"{left} not rated yet\");\n");
            sb.Append("            parts.Add(needs.Count == 0 ? \"no needs rated for action\" : \"action: \" + string.Join(\", \", needs));\n");
            sb.Append("            if (useful.Count > 0) parts.Add(\"strengths to build on: \" + string.Join(\", \", useful));\n");
            sb.Append("            return string.Join(\"; \", parts) + \".\";\n        }\n    }\n\n");
        }
        if (a.Alerts.Count > 0)
        {
            sb.Append($"    /// <summary>Follow-up an answer calls for at once, or empty.</summary>\n    public string {key}Alert => string.Join(\" \", new (int? Rating, string Say)[] {{ ");
            sb.Append(string.Join(", ", a.Alerts.Select(kv => $"({key}Q{kv.Key}, \"{kv.Value.Replace("\"", "\\\"")}\")")));
            sb.Append(" }.Where(x => x.Rating > 0).Select(x => x.Say));\n\n");
        }
        return sb.ToString();
    }

    /// <summary>A whole view-model class holding an assessment's members (when the view has none yet).</summary>
    public static string ViewModelClass(string ns, string className, string members) => $$"""
        // Generated by AVAUIDesigner. Yours to change: saving the view again only adds what is missing.
        using System.Collections.ObjectModel;
        using CommunityToolkit.Mvvm.ComponentModel;
        using CommunityToolkit.Mvvm.Input;

        namespace {{ns}};

        public partial class {{className}} : ObservableObject
        {
        {{members.TrimEnd()}}
        }

        """;
}

/// <summary>The value converters generated screens bind through: a rating's radio buttons, and DateOnly or TimeOnly to the pickers.</summary>
public static class ConvertersCode
{
    public const string FileName = "Converters.cs";

    public static string Source(string ns) => $$"""
        // Generated by AVAUIDesigner: the converters its screens bind through.
        using System.Globalization;
        using Avalonia.Data;
        using Avalonia.Data.Converters;

        namespace {{ns}};

        public static class Converters
        {
            /// <summary>A rating as a radio button: checked when the rating is the button's value (ConverterParameter); checking it sets the rating.</summary>
            public static readonly IValueConverter Score = new ScoreConverter();

            /// <summary>A DateOnly as the DateTime a date picker shows.</summary>
            public static readonly IValueConverter DateOnly = new DateOnlyConverter();

            /// <summary>A TimeOnly as the TimeSpan a time picker shows.</summary>
            public static readonly IValueConverter TimeOnly = new TimeOnlyConverter();

            private sealed class DateOnlyConverter : IValueConverter
            {
                public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
                    value is System.DateOnly d ? d.ToDateTime(System.TimeOnly.MinValue) : null;

                public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
                    value is DateTime t ? System.DateOnly.FromDateTime(t) : null;
            }

            private sealed class TimeOnlyConverter : IValueConverter
            {
                public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
                    value is System.TimeOnly t ? t.ToTimeSpan() : null;

                public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
                    value is TimeSpan s ? System.TimeOnly.FromTimeSpan(s) : null;
            }

            private sealed class ScoreConverter : IValueConverter
            {
                public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
                    value is int rating && int.TryParse(parameter?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mine) && rating == mine;

                public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
                    value is true && int.TryParse(parameter?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mine) ? mine : BindingOperations.DoNothing;
            }
        }

        """;
}
