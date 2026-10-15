using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Engine.Serialization;

/// <summary>Prefix ↔ namespace URI mapping used by the parser and serializer.</summary>
public class XamlNamespaceMap
{
    public const string Avalonia = "https://github.com/avaloniaui";
    public const string Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    public const string Blend = "http://schemas.microsoft.com/expression/blend/2008";
    public const string MarkupCompatibility = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    private readonly Dictionary<string, string> _prefixToUri = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _uriToPrefix = new(StringComparer.Ordinal);

    public XamlNamespaceMap(IEnumerable<XamlNamespaceDeclaration> declarations)
    {
        foreach (var d in declarations) Add(d.Prefix, d.Uri);
    }

    public void Add(string prefix, string uri)
    {
        _prefixToUri[prefix] = uri;
        _uriToPrefix.TryAdd(uri, prefix);
    }

    public string? GetUri(string prefix) => _prefixToUri.GetValueOrDefault(prefix);

    public string? GetPrefix(string uri) => _uriToPrefix.GetValueOrDefault(uri);

    public IEnumerable<KeyValuePair<string, string>> All => _prefixToUri;

    /// <summary>The standard declarations of a new Avalonia document.</summary>
    public static IEnumerable<XamlNamespaceDeclaration> DefaultDeclarations()
    {
        yield return new XamlNamespaceDeclaration("", Avalonia);
        yield return new XamlNamespaceDeclaration("x", Xaml);
        yield return new XamlNamespaceDeclaration("d", Blend);
        yield return new XamlNamespaceDeclaration("mc", MarkupCompatibility);
    }

    public static string ClrNamespaceUri(string clrNamespace, string? assembly = null) =>
        assembly is null ? $"clr-namespace:{clrNamespace}" : $"clr-namespace:{clrNamespace};assembly={assembly}";
}
