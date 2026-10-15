using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace AVAUIDesigner.Engine.CodeGeneration;

/// <summary>C# identifier helpers.</summary>
public static class CodeIdentifiers
{
    public static bool IsValidIdentifier(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (!SyntaxFacts.IsValidIdentifier(name)) return false;
        return SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None;
    }

    /// <summary>Turns arbitrary text into a valid identifier ("my-window" → "my_window").</summary>
    public static string MakeIdentifier(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text)
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        if (sb.Length == 0 || char.IsDigit(sb[0])) sb.Insert(0, '_');
        var result = sb.ToString();
        return SyntaxFacts.GetKeywordKind(result) == SyntaxKind.None ? result : "@" + result;
    }

    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var trimmed = name.TrimStart('_');
        if (trimmed.Length == 0) return name;
        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }

    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>Strips an XML prefix: "local:MyControl" → "MyControl".</summary>
    public static string BareTypeName(string elementType) =>
        elementType.Contains(':') ? elementType[(elementType.IndexOf(':') + 1)..] : elementType;

    /// <summary>Re-indents a block of code so its least-indented line starts at <paramref name="indent"/>.</summary>
    public static string Reindent(string code, string indent)
    {
        var lines = code.Replace("\r\n", "\n").Split('\n');
        var start = 0;
        var end = lines.Length;
        while (start < end && string.IsNullOrWhiteSpace(lines[start])) start++;
        while (end > start && string.IsNullOrWhiteSpace(lines[end - 1])) end--;
        if (start >= end) return string.Empty;
        var slice = lines[start..end];
        var min = slice.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join("\n", slice.Select(l => string.IsNullOrWhiteSpace(l) ? string.Empty : indent + l[Math.Min(min, l.Length)..].TrimEnd()));
    }
}
