using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AVAUIDesigner.Data;

/// <summary>Adds members to a class in existing C# source, keeping every member already there as it is.</summary>
public static class CodeMerge
{
    /// <summary>
    /// <paramref name="source"/> with those of <paramref name="members"/> (member declarations, as inside a class) that
    /// <paramref name="className"/> doesn't have yet, added at its end, and <paramref name="usings"/> it lacks.
    /// </summary>
    public static string AddMembers(string source, string className, string members, IEnumerable<string>? usings = null)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var cls = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.Text == className)
                  ?? throw new InvalidOperationException($"There's no class {className} in the view model's source.");
        var have = new HashSet<string>(cls.Members.SelectMany(Names), StringComparer.Ordinal);
        var wrapper = CSharpSyntaxTree.ParseText("class __Wrap {\n" + members + "\n}").GetCompilationUnitRoot()
            .DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var add = wrapper.Members.Where(m => !Names(m).Any(have.Contains)).Select(m => m.ToFullString().TrimEnd()).ToList();
        var text = source;
        if (add.Count > 0)
        {
            var close = cls.CloseBraceToken.SpanStart;
            var lineStart = text.LastIndexOf('\n', Math.Max(0, close - 1)) + 1;
            if (text[lineStart..close].Trim().Length > 0) lineStart = close;
            text = text[..lineStart].TrimEnd() + "\n\n" + string.Join("\n\n", add) + "\n" + text[lineStart..];
        }
        foreach (var u in usings ?? [])
            if (!root.Usings.Any(x => x.Name?.ToString() == u))
            {
                var at = root.Usings.Count > 0 ? root.Usings.Last().FullSpan.End : 0;
                text = text[..at] + $"using {u};\n" + text[at..];
                root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
            }
        return text;
    }

    /// <summary>The names a member declares, as the source generators see them: a field "_total" also claims "Total".</summary>
    private static IEnumerable<string> Names(MemberDeclarationSyntax m)
    {
        switch (m)
        {
            case FieldDeclarationSyntax f:
                foreach (var v in f.Declaration.Variables)
                {
                    var n = v.Identifier.Text;
                    yield return n;
                    var bare = n.TrimStart('_');
                    if (bare.Length > 0) yield return char.ToUpperInvariant(bare[0]) + bare[1..];
                }
                break;
            case PropertyDeclarationSyntax p: yield return p.Identifier.Text; break;
            case MethodDeclarationSyntax me: yield return me.Identifier.Text + "(" + me.ParameterList.Parameters.Count; break;
            case BaseTypeDeclarationSyntax t: yield return t.Identifier.Text; break;
        }
    }
}
