using System.Globalization;
using System.Text;

namespace AVAUIDesigner.Data;

/// <summary>
/// A data structure a screen is built for: a C# class or record, a table in a SQL script, a table in a database, or
/// one described by hand. Whatever it came from, the form, CRUD and code generators work from this.
/// </summary>
public sealed class EntityModel
{
    /// <summary>The C# class name, PascalCase and singular ("Patient").</summary>
    public string Name { get; set; } = "";

    /// <summary>The table it is kept in ("patients", "dbo.Patient"); defaults to <see cref="Name"/>.</summary>
    public string? Table { get; set; }

    /// <summary>The table's schema or owner ("dbo", "public", "CLINIC"), if any.</summary>
    public string? Schema { get; set; }

    /// <summary>The C# namespace of the class it was read from, when it came from code (screens then use that class).</summary>
    public string? Namespace { get; set; }

    /// <summary>Where it was read from ("Models/Patient.cs", "sqlserver:Clinic dbo.Patient", "described").</summary>
    public string Source { get; set; } = "described";

    /// <summary>A label for people ("Patient"); defaults to the name in words.</summary>
    public string? Label { get; set; }

    public List<FieldModel> Fields { get; set; } = [];

    public string TableName => string.IsNullOrWhiteSpace(Table) ? Name : Table!;

    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Words.FromIdentifier(Name) : Label!;

    /// <summary>The plural used for lists and the screen's class ("Patients").</summary>
    public string Plural => Words.Plural(Name);

    public IEnumerable<FieldModel> Keys => Fields.Where(f => f.IsKey);

    /// <summary>Fields a person edits: not generated keys, not navigation, not row versions.</summary>
    public IEnumerable<FieldModel> Editable => Fields.Where(f => !(f.IsKey && f.IsGenerated) && !f.IsComputed);

    /// <summary>Checks the model makes sense, and fills in what can be inferred (a key named Id, column names).</summary>
    public EntityModel Normalise()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("An entity needs a name.");
        Name = Identifiers.Pascal(Name);
        if (Fields.Count == 0) throw new ArgumentException($"{Name} has no fields.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Fields)
        {
            if (string.IsNullOrWhiteSpace(f.Name)) throw new ArgumentException($"A field of {Name} has no name.");
            f.Column ??= f.Name;
            f.Name = Identifiers.Pascal(f.Name);
            if (!seen.Add(f.Name)) throw new ArgumentException($"{Name} has two fields called {f.Name}.");
            f.Type = TypeMap.Normalise(f.Type);
        }
        if (!Fields.Any(f => f.IsKey))
        {
            var key = Fields.FirstOrDefault(f => f.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                      ?? Fields.FirstOrDefault(f => f.Name.Equals(Name + "Id", StringComparison.OrdinalIgnoreCase));
            if (key != null)
            {
                key.IsKey = true;
                if (TypeMap.IsInteger(key.Type)) key.IsGenerated = true;
            }
        }
        return this;
    }

    public override string ToString() => $"{Name} ({Fields.Count} fields)";
}

/// <summary>One field of an entity: its C# type, its column and what a form needs to know about it.</summary>
public sealed class FieldModel
{
    /// <summary>The C# property name ("FirstName").</summary>
    public string Name { get; set; } = "";

    /// <summary>The column it is stored in ("first_name"); defaults to <see cref="Name"/>.</summary>
    public string? Column { get; set; }

    /// <summary>The C# type without "?": string, int, long, short, decimal, double, bool, DateTime, DateOnly, TimeOnly, Guid, byte[]…</summary>
    public string Type { get; set; } = "string";

    /// <summary>May be empty (null in the database, "?" in C#).</summary>
    public bool Nullable { get; set; }

    /// <summary>Part of the primary key.</summary>
    public bool IsKey { get; set; }

    /// <summary>Filled in by the database (identity, sequence, autoincrement): never typed in.</summary>
    public bool IsGenerated { get; set; }

    /// <summary>Computed by the database or a row version: shown, never written.</summary>
    public bool IsComputed { get; set; }

    /// <summary>Must be given before saving.</summary>
    public bool Required { get; set; }

    /// <summary>Longest text allowed, if limited.</summary>
    public int? MaxLength { get; set; }

    public double? Min { get; set; }
    public double? Max { get; set; }

    /// <summary>The label people see ("Date of birth"); defaults to the name in words.</summary>
    public string? Label { get; set; }

    /// <summary>The C# enum the field is declared as in the person's own class ("Sex"), when it is one.</summary>
    public string? EnumType { get; set; }

    /// <summary>The choices, for a field with a fixed list (an enum, a CHECK list, or given by hand).</summary>
    public List<string>? Options { get; set; }

    /// <summary>The table it refers to, for a foreign key ("Provider", "dbo.Provider").</summary>
    public string? References { get; set; }

    /// <summary>The referenced table's key column, when known (a lookup list needs it).</summary>
    public string? ReferencesKey { get; set; }

    /// <summary>The referenced table's column people recognise a row by ("name", "last_name"), when known.</summary>
    public string? ReferencesDisplay { get; set; }

    /// <summary>The referenced table's schema, when known.</summary>
    public string? ReferencesSchema { get; set; }

    /// <summary>How it is edited, if not the type's usual way: text, multiline, email, phone, password, date, time, datetime, number, money, check, choice, readonly.</summary>
    public string? Editor { get; set; }

    public string ColumnName => string.IsNullOrWhiteSpace(Column) ? Name : Column!;

    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Words.FromIdentifier(Name) : Label!;

    /// <summary>The C# type as declared, with "?" where it may be empty.</summary>
    public string CSharpType => TypeMap.Declared(Type, Nullable);

    /// <summary>How a form edits it.</summary>
    public string EditorKind => FormEditors.Kind(this);

    public override string ToString() => $"{Name}: {CSharpType}{(IsKey ? " key" : "")}";
}

/// <summary>Turning identifiers into words and words into identifiers.</summary>
public static class Words
{
    /// <summary>"DateOfBirth" / "date_of_birth" → "Date of birth"; "SSN" stays; "PatientId" → "Patient ID".</summary>
    public static string FromIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var parts = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c is '_' or '-' or ' ' or '.')
            {
                Flush();
                continue;
            }
            var boundary = current.Length > 0 && char.IsUpper(c) &&
                           (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1])));
            if (boundary) Flush();
            current.Append(c);
        }
        Flush();
        var words = parts.Select((w, i) =>
        {
            if (w.Equals("id", StringComparison.OrdinalIgnoreCase)) return "ID";
            if (w.Length > 1 && w.All(char.IsUpper)) return w;
            return i == 0 ? char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant() : w.ToLowerInvariant();
        });
        return string.Join(' ', words);

        void Flush()
        {
            if (current.Length > 0) parts.Add(current.ToString());
            current.Clear();
        }
    }

    /// <summary>A simple English plural: Patient → Patients, Diagnosis → Diagnoses, Allergy → Allergies.</summary>
    public static string Plural(string name)
    {
        if (name.EndsWith("sis", StringComparison.Ordinal)) return name[..^3] + "ses";
        if (name.EndsWith('y') && name.Length > 1 && !"aeiou".Contains(char.ToLowerInvariant(name[^2]))) return name[..^1] + "ies";
        if (name.EndsWith('s') || name.EndsWith('x') || name.EndsWith("ch", StringComparison.Ordinal) || name.EndsWith("sh", StringComparison.Ordinal)) return name + "es";
        if (name.EndsWith("Person", StringComparison.Ordinal)) return name[..^6] + "People";
        return name + "s";
    }

    /// <summary>A singular for a plural table name: Patients → Patient, Allergies → Allergy, Diagnoses → Diagnosis.</summary>
    public static string Singular(string name)
    {
        if (name.EndsWith("ses", StringComparison.Ordinal) && name.Length > 4 && name.EndsWith("oses", StringComparison.Ordinal)) return name[..^2] + "is";
        if (name.EndsWith("ies", StringComparison.Ordinal) && name.Length > 3) return name[..^3] + "y";
        if (name.EndsWith("sses", StringComparison.Ordinal) || name.EndsWith("xes", StringComparison.Ordinal) || name.EndsWith("ches", StringComparison.Ordinal)) return name[..^2];
        if (name.EndsWith("People", StringComparison.Ordinal)) return name[..^6] + "Person";
        if (name.EndsWith('s') && !name.EndsWith("ss", StringComparison.Ordinal) && !name.EndsWith("us", StringComparison.Ordinal) && name.Length > 2) return name[..^1];
        return name;
    }
}

/// <summary>Making C# identifiers.</summary>
public static class Identifiers
{
    private static readonly HashSet<string> Keywords =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
        "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile", "while",
    ];

    /// <summary>"first_name" / "first name" / "FIRST_NAME" → "FirstName"; a leading digit gets an underscore.</summary>
    public static string Pascal(string text)
    {
        var sb = new StringBuilder();
        var upper = true;
        var allCaps = text.Where(char.IsLetter).All(char.IsUpper);
        foreach (var c in text.Trim())
        {
            if (!char.IsLetterOrDigit(c))
            {
                upper = true;
                continue;
            }
            sb.Append(upper ? char.ToUpperInvariant(c) : allCaps ? char.ToLowerInvariant(c) : c);
            upper = false;
        }
        if (sb.Length == 0) return "Item";
        if (char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }

    /// <summary>"FirstName" → "firstName", kept clear of C# keywords.</summary>
    public static string Camel(string text)
    {
        var p = Pascal(text);
        var c = char.ToLowerInvariant(p[0]) + p[1..];
        return Keywords.Contains(c) ? "@" + c : c;
    }

    /// <summary>A name safe as an x:Name or a C# member: letters, digits and underscores, not starting with a digit.</summary>
    public static string Safe(string text)
    {
        var s = new string(text.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (s.Length == 0) return "item";
        return char.IsDigit(s[0]) ? "_" + s : s;
    }
}

/// <summary>C# types: their usual names, and SQL types mapped onto them.</summary>
public static class TypeMap
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["String"] = "string", ["System.String"] = "string", ["text"] = "string", ["Int32"] = "int", ["System.Int32"] = "int",
        ["integer"] = "int", ["Int64"] = "long", ["System.Int64"] = "long", ["Int16"] = "short", ["Byte"] = "byte",
        ["Boolean"] = "bool", ["boolean"] = "bool", ["Decimal"] = "decimal", ["Double"] = "double", ["Single"] = "float",
        ["System.DateTime"] = "DateTime", ["datetime"] = "DateTime", ["date"] = "DateOnly", ["System.DateOnly"] = "DateOnly",
        ["time"] = "TimeOnly", ["System.TimeOnly"] = "TimeOnly", ["System.TimeSpan"] = "TimeSpan", ["System.Guid"] = "Guid",
        ["guid"] = "Guid", ["uuid"] = "Guid", ["Byte[]"] = "byte[]", ["System.Byte[]"] = "byte[]", ["number"] = "decimal",
        ["money"] = "decimal", ["System.DateTimeOffset"] = "DateTimeOffset", ["char"] = "string",
    };

    private static readonly HashSet<string> ValueTypes =
    [
        "int", "long", "short", "byte", "bool", "decimal", "double", "float", "DateTime", "DateOnly", "TimeOnly", "TimeSpan",
        "Guid", "DateTimeOffset",
    ];

    public static string Normalise(string? type)
    {
        var t = (type ?? "string").Trim().TrimEnd('?');
        return Aliases.TryGetValue(t, out var a) ? a : t.Length == 0 ? "string" : t;
    }

    public static bool IsValueType(string type) => ValueTypes.Contains(type);

    public static bool IsInteger(string type) => type is "int" or "long" or "short" or "byte";

    public static bool IsNumber(string type) => IsInteger(type) || type is "decimal" or "double" or "float";

    public static string Declared(string type, bool nullable) => nullable && (IsValueType(type) || type is "string" or "byte[]") ? type + "?" : type;

    /// <summary>A SQL column type (any engine's spelling) as a C# type.</summary>
    public static string FromSql(string sqlType, int? precision = null, int? scale = null)
    {
        var t = sqlType.Trim().ToLowerInvariant();
        var paren = t.IndexOf('(');
        if (paren > 0) t = t[..paren].Trim();
        t = t.Replace(" unsigned", "").Replace("unsigned ", "");
        return t switch
        {
            "int" or "integer" or "int4" or "mediumint" or "serial" => "int",
            "bigint" or "int8" or "bigserial" => "long",
            "smallint" or "int2" or "smallserial" => "short",
            "tinyint" => precision == 1 ? "bool" : "byte",
            "bit" or "bool" or "boolean" => "bool",
            "decimal" or "numeric" or "money" or "smallmoney" or "decfloat" => "decimal",
            "number" => scale is null or 0 ? precision switch { null => "decimal", <= 4 => "short", <= 9 => "int", <= 18 => "long", _ => "decimal" } : "decimal",
            "float" or "double" or "double precision" or "float8" or "binary_double" => "double",
            "real" or "float4" or "binary_float" => "float",
            "date" => "DateOnly",
            "time" or "time without time zone" => "TimeOnly",
            "datetime" or "datetime2" or "smalldatetime" or "timestamp" or "timestamp without time zone" => "DateTime",
            "datetimeoffset" or "timestamp with time zone" or "timestamptz" or "timestamp with local time zone" => "DateTimeOffset",
            "uniqueidentifier" or "uuid" => "Guid",
            "binary" or "varbinary" or "image" or "blob" or "bytea" or "longblob" or "mediumblob" or "raw" or "long raw" or "varbinary(max)" => "byte[]",
            "interval" => "TimeSpan",
            _ => "string",
        };
    }

    /// <summary>Text as a number, for limits read from schemas and attributes.</summary>
    public static double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
}
