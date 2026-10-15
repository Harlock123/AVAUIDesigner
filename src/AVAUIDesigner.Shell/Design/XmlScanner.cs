namespace AVAUIDesigner.Shell.Design;

/// <summary>
/// Lightweight, error-tolerant XML scanning used by the source editor (completion, auto-close and
/// tag matching). It works on possibly-invalid text, so it never throws.
/// </summary>
public static class XmlScanner
{
    public readonly record struct Tag(int Start, int End, string Name, bool IsClosing, bool IsSelfClosing)
    {
        /// <summary>Offset of the tag name ("&lt;" + 1, or "&lt;/" + 2).</summary>
        public int NameStart => Start + (IsClosing ? 2 : 1);
    }

    /// <summary>All tags in the text, skipping comments, CDATA and processing instructions.</summary>
    public static List<Tag> Tags(string text, int limit = int.MaxValue)
    {
        var tags = new List<Tag>();
        var i = 0;
        var end = Math.Min(limit, text.Length);
        while (i < end)
        {
            var lt = text.IndexOf('<', i);
            if (lt < 0 || lt >= end) break;
            if (Starts(text, lt, "<!--"))
            {
                var close = text.IndexOf("-->", lt + 4, StringComparison.Ordinal);
                i = close < 0 ? text.Length : close + 3;
                continue;
            }
            if (Starts(text, lt, "<![CDATA["))
            {
                var close = text.IndexOf("]]>", lt, StringComparison.Ordinal);
                i = close < 0 ? text.Length : close + 3;
                continue;
            }
            if (Starts(text, lt, "<?") || Starts(text, lt, "<!"))
            {
                var close = text.IndexOf('>', lt);
                i = close < 0 ? text.Length : close + 1;
                continue;
            }
            var closing = lt + 1 < text.Length && text[lt + 1] == '/';
            var nameStart = lt + (closing ? 2 : 1);
            var nameEnd = nameStart;
            while (nameEnd < text.Length && IsNameChar(text[nameEnd])) nameEnd++;
            if (nameEnd == nameStart)
            {
                i = lt + 1;
                continue;
            }
            var gt = FindTagEnd(text, nameEnd);
            var tagEnd = gt < 0 ? text.Length : gt + 1;
            var selfClosing = gt > 0 && text[gt - 1] == '/';
            tags.Add(new Tag(lt, tagEnd, text[nameStart..nameEnd], closing, selfClosing));
            i = tagEnd;
        }
        return tags;
    }

    /// <summary>The '&gt;' ending a tag, honoring quoted attribute values; -1 if unterminated (or a new '&lt;' starts first).</summary>
    private static int FindTagEnd(string text, int from)
    {
        char? quote = null;
        for (var i = from; i < text.Length; i++)
        {
            var c = text[i];
            if (quote is not null)
            {
                if (c == quote) quote = null;
                continue;
            }
            if (c is '"' or '\'') quote = c;
            else if (c == '>') return i;
            else if (c == '<') return -1;
        }
        return -1;
    }

    /// <summary>Names of the elements still open at <paramref name="offset"/>, innermost last.</summary>
    public static List<string> OpenElements(string text, int offset)
    {
        var stack = new List<string>();
        foreach (var tag in Tags(text, offset))
        {
            if (tag.End > offset) break;
            if (tag.IsSelfClosing) continue;
            if (!tag.IsClosing) stack.Add(tag.Name);
            else
            {
                var idx = stack.FindLastIndex(n => n == tag.Name);
                if (idx >= 0) stack.RemoveRange(idx, stack.Count - idx);
            }
        }
        return stack;
    }

    /// <summary>
    /// When <paramref name="offset"/> is inside a start tag (after its name), the element name; null otherwise.
    /// </summary>
    public static string? ElementAtCaretInTag(string text, int offset)
    {
        var lt = text.LastIndexOf('<', Math.Max(0, offset - 1));
        if (lt < 0) return null;
        var gt = text.LastIndexOf('>', Math.Max(0, offset - 1));
        if (gt > lt) return null;
        if (lt + 1 < text.Length && text[lt + 1] is '/' or '!' or '?') return null;
        // Ensure we are not inside an attribute value.
        var quotes = 0;
        for (var i = lt; i < offset && i < text.Length; i++) if (text[i] == '"') quotes++;
        if (quotes % 2 == 1) return null;
        var nameEnd = lt + 1;
        while (nameEnd < text.Length && IsNameChar(text[nameEnd])) nameEnd++;
        if (nameEnd >= offset) return null;
        return text[(lt + 1)..nameEnd];
    }

    /// <summary>For a tag whose name contains <paramref name="offset"/>, the matching tag's name range.</summary>
    public static (int A, int ALength, int B, int BLength)? MatchingTag(string text, int offset)
    {
        var tags = Tags(text);
        var index = tags.FindIndex(t => offset >= t.NameStart && offset <= t.NameStart + t.Name.Length);
        if (index < 0) return null;
        var tag = tags[index];
        if (tag.IsSelfClosing) return null;
        var depth = 0;
        if (!tag.IsClosing)
        {
            for (var i = index + 1; i < tags.Count; i++)
            {
                var t = tags[i];
                if (t.Name != tag.Name || t.IsSelfClosing) continue;
                if (!t.IsClosing) depth++;
                else if (depth-- == 0) return (tag.NameStart, tag.Name.Length, t.NameStart, t.Name.Length);
            }
        }
        else
        {
            for (var i = index - 1; i >= 0; i--)
            {
                var t = tags[i];
                if (t.Name != tag.Name || t.IsSelfClosing) continue;
                if (t.IsClosing) depth++;
                else if (depth-- == 0) return (t.NameStart, t.Name.Length, tag.NameStart, tag.Name.Length);
            }
        }
        return null;
    }

    /// <summary>The identifier-like word around <paramref name="offset"/>.</summary>
    public static string? WordAt(string text, int offset)
    {
        if (text.Length == 0) return null;
        var s = Math.Clamp(offset, 0, text.Length);
        var e = s;
        while (s > 0 && IsWordChar(text[s - 1])) s--;
        while (e < text.Length && IsWordChar(text[e])) e++;
        return e > s ? text[s..e] : null;
    }

    public static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or ':' or '.' or '-';

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static bool Starts(string text, int at, string s) => string.CompareOrdinal(text, at, s, 0, s.Length) == 0;
}
