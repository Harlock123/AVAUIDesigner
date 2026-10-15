using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Engine.CodeGeneration;

/// <summary>Suggests event handler method names.</summary>
public static class HandlerNaming
{
    /// <summary>
    /// "{ElementName}_{EventName}" (PascalCased, e.g. "BtnOk_Click"); for unnamed elements
    /// "{TypeName}{AutoIndex}_{EventName}" (e.g. "Button1_Click"). The result is unique among the
    /// document's existing handler names unless it is the handler already wired to this element.
    /// </summary>
    public static string Suggest(DesignElement element, string eventName, DesignDocument? document)
    {
        string stem;
        if (element.Name is { } name)
        {
            stem = CodeIdentifiers.ToPascalCase(name);
        }
        else
        {
            var type = CodeIdentifiers.BareTypeName(element.ElementType);
            var index = 1;
            if (document is not null)
            {
                var sameType = document.AllElements.Where(e => e.ElementType == element.ElementType).ToList();
                index = Math.Max(1, sameType.IndexOf(element) + 1);
            }
            stem = $"{type}{index}";
        }

        var candidate = CodeIdentifiers.MakeIdentifier($"{stem}_{eventName}");
        if (document is null) return candidate;

        var used = document.AllElements.Where(e => !ReferenceEquals(e, element))
            .SelectMany(e => e.EventHandlers).Select(h => h.HandlerName).ToHashSet();
        if (!used.Contains(candidate)) return candidate;
        for (var i = 2; ; i++)
            if (!used.Contains($"{candidate}{i}")) return $"{candidate}{i}";
    }
}
