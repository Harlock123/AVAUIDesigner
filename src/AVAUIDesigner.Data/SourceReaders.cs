using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AVAUIDesigner.Data;

/// <summary>
/// Reads entities from source files: C# classes and records (with their data-annotation attributes), and the tables
/// of a SQL script's CREATE TABLE statements.
/// </summary>
public static class SourceReaders
{
    /// <summary>The entities in a file, by its extension: .cs or .sql.</summary>
    public static List<EntityModel> Read(string path, string text)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var list = ext switch
        {
            ".cs" => CSharp(text),
            ".sql" or ".ddl" => Sql(text),
            _ => throw new NotSupportedException($"Entities are read from C# (.cs) and SQL (.sql, .ddl) files, not {ext}. For another language, read the file and describe the entity's fields instead."),
        };
        foreach (var e in list) e.Source = path;
        return list;
    }

    // ───────────────────────────── C# ─────────────────────────────

    private static readonly HashSet<string> Scalars =
    [
        "string", "int", "long", "short", "byte", "bool", "decimal", "double", "float", "DateTime", "DateOnly", "TimeOnly",
        "TimeSpan", "Guid", "DateTimeOffset", "byte[]", "char",
    ];

    /// <summary>The classes and records in C# source that have properties a form could edit.</summary>
    public static List<EntityModel> CSharp(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var enums = root.DescendantNodes().OfType<EnumDeclarationSyntax>()
            .ToDictionary(e => e.Identifier.Text, e => e.Members.Select(m => m.Identifier.Text).ToList());
        var result = new List<EntityModel>();
        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (type is not (ClassDeclarationSyntax or RecordDeclarationSyntax)) continue;
            if (type.Modifiers.Any(SyntaxKind.StaticKeyword)) continue;
            var ns = type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Select(n => n.Name.ToString()).Reverse().ToList();
            var entity = new EntityModel { Name = type.Identifier.Text, Namespace = ns.Count > 0 ? string.Join('.', ns) : null };
            foreach (var a in Attributes(type.AttributeLists))
                if (a.Name == "Table")
                {
                    entity.Table = a.First;
                    entity.Schema = a.Named("Schema");
                }

            // A positional record's parameters are its properties.
            if (type is RecordDeclarationSyntax { ParameterList: { } parameters })
                foreach (var p in parameters.Parameters)
                    if (p.Type is { } t && Field(p.Identifier.Text, t, Attributes(p.AttributeLists), enums, required: false) is { } f)
                        entity.Fields.Add(f);

            foreach (var prop in type.Members.OfType<PropertyDeclarationSyntax>())
            {
                if (!prop.Modifiers.Any(SyntaxKind.PublicKeyword) || prop.Modifiers.Any(SyntaxKind.StaticKeyword)) continue;
                var attrs = Attributes(prop.AttributeLists);
                if (attrs.Any(a => a.Name is "NotMapped" or "JsonIgnore")) continue;
                if (Field(prop.Identifier.Text, prop.Type, attrs, enums, prop.Modifiers.Any(SyntaxKind.RequiredKeyword)) is { } f)
                    entity.Fields.Add(f);
            }
            if (entity.Fields.Count > 0) result.Add(entity.Normalise());
        }
        return result;
    }

    private static FieldModel? Field(string name, TypeSyntax typeSyntax, List<Attr> attrs, Dictionary<string, List<string>> enums, bool required)
    {
        var nullable = typeSyntax is NullableTypeSyntax;
        var inner = typeSyntax is NullableTypeSyntax n ? n.ElementType : typeSyntax;
        var typeName = inner.ToString();
        if (typeName.StartsWith("System.", StringComparison.Ordinal)) typeName = typeName[7..];
        var normal = TypeMap.Normalise(typeName);
        var f = new FieldModel { Name = name, Nullable = nullable, Required = required };
        if (enums.TryGetValue(normal, out var members))
        {
            f.Type = "string";
            f.EnumType = normal;
            f.Options = members;
            f.Editor = "choice";
        }
        else if (Scalars.Contains(normal))
        {
            f.Type = normal;
        }
        else
        {
            return null; // a navigation property or a collection: not a column a form edits
        }
        foreach (var a in attrs)
        {
            switch (a.Name)
            {
                case "Key": f.IsKey = true; break;
                case "Required": f.Required = true; break;
                case "MaxLength" or "StringLength": f.MaxLength = (int?)TypeMap.Number(a.First); break;
                case "Range":
                    f.Min = TypeMap.Number(a.Args.ElementAtOrDefault(0));
                    f.Max = TypeMap.Number(a.Args.ElementAtOrDefault(1));
                    break;
                case "Display": f.Label = a.Named("Name") ?? f.Label; break;
                case "DisplayName": f.Label = a.First; break;
                case "Column": f.Column = a.First ?? f.Column; break;
                case "EmailAddress": f.Editor = "email"; break;
                case "Phone": f.Editor = "phone"; break;
                case "DataType" when a.First?.Contains("MultilineText") == true: f.Editor = "multiline"; break;
                case "DataType" when a.First?.Contains("Password") == true: f.Editor = "password"; break;
                case "DatabaseGenerated" when a.First?.Contains("Identity") == true: f.IsGenerated = true; break;
                case "DatabaseGenerated" when a.First?.Contains("Computed") == true: f.IsComputed = true; break;
                case "Timestamp": f.IsComputed = true; break;
                case "ForeignKey": f.References = a.First; break;
            }
        }
        if (normal == "char") f.MaxLength = 1;
        return f;
    }

    private sealed record Attr(string Name, List<string> Args, Dictionary<string, string> NamedArgs)
    {
        public string? First => Args.Count > 0 ? Args[0] : null;
        public string? Named(string key) => NamedArgs.GetValueOrDefault(key);
    }

    private static List<Attr> Attributes(SyntaxList<AttributeListSyntax> lists) =>
        lists.SelectMany(l => l.Attributes).Select(a =>
        {
            var name = a.Name.ToString();
            var dot = name.LastIndexOf('.');
            if (dot >= 0) name = name[(dot + 1)..];
            if (name.EndsWith("Attribute", StringComparison.Ordinal)) name = name[..^9];
            var args = new List<string>();
            var named = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var arg in a.ArgumentList?.Arguments ?? default)
            {
                var value = arg.Expression is LiteralExpressionSyntax lit ? lit.Token.ValueText : arg.Expression.ToString();
                if (arg.NameEquals is { } ne) named[ne.Name.Identifier.Text] = value;
                else if (arg.NameColon is { } nc) named[nc.Name.Identifier.Text] = value;
                else args.Add(value);
            }
            return new Attr(name, args, named);
        }).ToList();

    // ───────────────────────────── SQL ─────────────────────────────

    /// <summary>The tables of a SQL script's CREATE TABLE statements (any of the common dialects).</summary>
    public static List<EntityModel> Sql(string script)
    {
        var text = Regex.Replace(script, @"--[^\n]*", " ");
        text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        var result = new List<EntityModel>();
        foreach (Match m in Regex.Matches(text, @"CREATE\s+(?:GLOBAL\s+TEMPORARY\s+|TEMPORARY\s+|TEMP\s+)?TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<name>[^\s(]+)\s*\(", RegexOptions.IgnoreCase))
        {
            var body = Balanced(text, m.Index + m.Length - 1);
            if (body is null) continue;
            var (schema, table) = SplitName(m.Groups["name"].Value);
            var entity = new EntityModel { Name = Identifiers.Pascal(Words.Singular(table)), Table = table, Schema = schema };
            var keys = new List<string>();
            foreach (var part in SplitTopLevel(body))
            {
                var p = part.Trim();
                if (p.Length == 0) continue;
                var head = p.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0].ToUpperInvariant();
                if (head is "CONSTRAINT" or "PRIMARY" or "FOREIGN" or "UNIQUE" or "CHECK" or "INDEX" or "KEY")
                {
                    if (Regex.Match(p, @"PRIMARY\s+KEY\s*\((?<cols>[^)]*)\)", RegexOptions.IgnoreCase) is { Success: true } pk)
                        keys.AddRange(pk.Groups["cols"].Value.Split(',').Select(c => Unquote(c.Trim().Split(' ')[0])));
                    if (Regex.Match(p, @"FOREIGN\s+KEY\s*\((?<col>[^)]*)\)\s*REFERENCES\s+(?<ref>[^\s(]+)", RegexOptions.IgnoreCase) is { Success: true } fk)
                        foreach (var c in fk.Groups["col"].Value.Split(','))
                            if (entity.Fields.FirstOrDefault(f => f.Column!.Equals(Unquote(c.Trim()), StringComparison.OrdinalIgnoreCase)) is { } field)
                                field.References = SplitName(fk.Groups["ref"].Value).Name;
                    continue;
                }
                var col = Regex.Match(p, @"^(?<name>""[^""]+""|`[^`]+`|\[[^\]]+\]|\S+)\s+(?<type>[A-Za-z][A-Za-z0-9_ ]*?(?:\s*\([^)]*\))?)(?=\s|$|,)(?<rest>.*)$", RegexOptions.Singleline);
                if (!col.Success) continue;
                var column = Unquote(col.Groups["name"].Value);
                var typeText = col.Groups["type"].Value.Trim();
                var rest = col.Groups["rest"].Value;
                var upperRest = rest.ToUpperInvariant();
                var (precision, scale, length) = Sizes(typeText);
                var f = new FieldModel
                {
                    Name = Identifiers.Pascal(column),
                    Column = column,
                    Type = TypeMap.FromSql(typeText, precision, scale),
                    Nullable = !upperRest.Contains("NOT NULL") && !upperRest.Contains("PRIMARY KEY"),
                    IsKey = upperRest.Contains("PRIMARY KEY"),
                    IsGenerated = upperRest.Contains("IDENTITY") || upperRest.Contains("AUTO_INCREMENT") || upperRest.Contains("AUTOINCREMENT")
                                  || upperRest.Contains("GENERATED") && upperRest.Contains("AS IDENTITY")
                                  || typeText.StartsWith("serial", StringComparison.OrdinalIgnoreCase) || typeText.StartsWith("bigserial", StringComparison.OrdinalIgnoreCase),
                    IsComputed = upperRest.Contains(" AS (") || upperRest.TrimStart().StartsWith("AS (", StringComparison.Ordinal) || typeText.Equals("rowversion", StringComparison.OrdinalIgnoreCase),
                    MaxLength = TypeMap.FromSql(typeText) == "string" ? length : null,
                };
                f.Required = !f.Nullable && !f.IsGenerated;
                if (Regex.Match(rest, @"REFERENCES\s+(?<ref>[^\s(]+)", RegexOptions.IgnoreCase) is { Success: true } inlineRef)
                    f.References = SplitName(inlineRef.Groups["ref"].Value).Name;
                if (Regex.Match(rest, @"CHECK\s*\(\s*\S+\s+IN\s*\((?<list>[^)]*)\)", RegexOptions.IgnoreCase) is { Success: true } check)
                {
                    f.Options = Regex.Matches(check.Groups["list"].Value, @"'((?:[^']|'')*)'").Select(x => x.Groups[1].Value.Replace("''", "'")).ToList();
                    if (f.Options.Count > 0) f.Editor = "choice";
                }
                if (Regex.IsMatch(typeText, @"^enum\s*\(", RegexOptions.IgnoreCase))
                {
                    f.Options = Regex.Matches(typeText, @"'((?:[^']|'')*)'").Select(x => x.Groups[1].Value).ToList();
                    f.Editor = "choice";
                }
                entity.Fields.Add(f);
            }
            foreach (var k in keys)
                if (entity.Fields.FirstOrDefault(f => f.Column!.Equals(k, StringComparison.OrdinalIgnoreCase)) is { } key)
                {
                    key.IsKey = true;
                    key.Nullable = false;
                }
            if (entity.Fields.Count > 0) result.Add(entity.Normalise());
        }
        return result;
    }

    private static (int? Precision, int? Scale, int? Length) Sizes(string type)
    {
        var m = Regex.Match(type, @"\(\s*(?<a>\d+|max)\s*(?:,\s*(?<b>\d+))?\s*\)", RegexOptions.IgnoreCase);
        if (!m.Success) return (null, null, null);
        int? a = int.TryParse(m.Groups["a"].Value, out var x) ? x : null;
        int? b = m.Groups["b"].Success && int.TryParse(m.Groups["b"].Value, out var y) ? y : null;
        return (a, b, a);
    }

    private static string? Balanced(string text, int open)
    {
        var depth = 0;
        var inQuote = false;
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\'') inQuote = !inQuote;
            if (inQuote) continue;
            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return text[(open + 1)..i];
        }
        return null;
    }

    private static List<string> SplitTopLevel(string body)
    {
        var parts = new List<string>();
        var depth = 0;
        var inQuote = false;
        var start = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (c == '\'') inQuote = !inQuote;
            if (inQuote) continue;
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0)
            {
                parts.Add(body[start..i]);
                start = i + 1;
            }
        }
        parts.Add(body[start..]);
        return parts;
    }

    internal static (string? Schema, string Name) SplitName(string qualified)
    {
        var parts = Regex.Matches(qualified, @"""[^""]+""|`[^`]+`|\[[^\]]+\]|[^.]+").Select(x => Unquote(x.Value)).ToList();
        return parts.Count >= 2 ? (parts[^2], parts[^1]) : (null, parts.FirstOrDefault() ?? qualified);
    }

    internal static string Unquote(string name)
    {
        name = name.Trim();
        if (name.Length >= 2 && (name[0] == '"' && name[^1] == '"' || name[0] == '`' && name[^1] == '`' || name[0] == '[' && name[^1] == ']'))
            return name[1..^1];
        return name;
    }
}
