using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Engine.Tests;

/// <summary>Deep structural comparison of design documents / element trees.</summary>
public static class DocumentAssert
{
    public static void Equal(DesignDocument expected, DesignDocument actual)
    {
        Assert.Equal(expected.XClass, actual.XClass);
        Assert.Equal(expected.Namespaces.OrderBy(n => n.Prefix), actual.Namespaces.OrderBy(n => n.Prefix));
        Equal(expected.RootElement, actual.RootElement, "/");
    }

    public static void Equal(DesignElement expected, DesignElement actual, string path = "/")
    {
        var here = $"{path}{expected.ElementType}";
        Assert.True(expected.ElementType == actual.ElementType, $"{here}: element type {actual.ElementType}");
        Assert.True(expected.Name == actual.Name, $"{here}: name '{expected.Name}' vs '{actual.Name}'");
        Assert.True(expected.IsUnknown == actual.IsUnknown, $"{here}: IsUnknown");
        Assert.True(expected.InnerText == actual.InnerText, $"{here}: inner text");

        Assert.True(Attrs(expected).SequenceEqual(Attrs(actual)),
            $"{here}: attributes [{string.Join(", ", Attrs(expected))}] vs [{string.Join(", ", Attrs(actual))}]");
        Assert.True(Attached(expected).SequenceEqual(Attached(actual)),
            $"{here}: attached [{string.Join(", ", Attached(expected))}] vs [{string.Join(", ", Attached(actual))}]");
        Assert.True(Events(expected).SequenceEqual(Events(actual)),
            $"{here}: events [{string.Join(", ", Events(expected))}] vs [{string.Join(", ", Events(actual))}]");

        var ePe = expected.PropertyElements.ToList();
        var aPe = actual.PropertyElements.ToList();
        Assert.True(ePe.Select(p => p.Name).SequenceEqual(aPe.Select(p => p.Name)), $"{here}: property elements");
        for (var i = 0; i < ePe.Count; i++)
        {
            Assert.Equal(ePe[i].ElementText, aPe[i].ElementText);
            Assert.True(ePe[i].ElementValues.Count == aPe[i].ElementValues.Count, $"{here}.{ePe[i].Name}: value count");
            for (var j = 0; j < ePe[i].ElementValues.Count; j++)
                Equal(ePe[i].ElementValues[j], aPe[i].ElementValues[j], $"{here}.{ePe[i].Name}/");
        }

        Assert.True(expected.Children.Count == actual.Children.Count, $"{here}: child count {expected.Children.Count} vs {actual.Children.Count}");
        for (var i = 0; i < expected.Children.Count; i++)
            Equal(expected.Children[i], actual.Children[i], here + "/");
    }

    private static IEnumerable<string> Attrs(DesignElement e) =>
        e.Properties.Where(p => p.IsAttributeForm).Select(p => $"{p.Name}={p.StringValue}").Order(StringComparer.Ordinal);

    private static IEnumerable<string> Attached(DesignElement e) =>
        e.AttachedProperties.Select(p => $"{p.FullName}={p.StringValue}").Order(StringComparer.Ordinal);

    private static IEnumerable<string> Events(DesignElement e) =>
        e.EventHandlers.Select(h => $"{h.EventName}={h.HandlerName}").Order(StringComparer.Ordinal);
}
