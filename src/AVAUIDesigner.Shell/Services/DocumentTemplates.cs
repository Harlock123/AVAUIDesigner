using AVAUIDesigner.Engine.Serialization;

namespace AVAUIDesigner.Shell.Services;

/// <summary>Boilerplate XAML for File › New.</summary>
public static class DocumentTemplates
{
    public static string Create(NewDocumentKind kind, string xClass, string title, string? viewModelNamespace = null)
    {
        var vmClass = xClass[(xClass.LastIndexOf('.') + 1)..] + "ViewModel";
        var mvvm = viewModelNamespace is not null;
        var root = kind == NewDocumentKind.Window ? "Window" : "UserControl";
        var vmNs = mvvm ? $"\n{Pad(root)}xmlns:vm=\"clr-namespace:{viewModelNamespace}\"" : "";
        var dataType = mvvm ? $"\n{Pad(root)}x:DataType=\"vm:{vmClass}\"" : "";
        var size = kind == NewDocumentKind.Window
            ? $"\n{Pad(root)}Title=\"{title}\"\n{Pad(root)}Width=\"800\"\n{Pad(root)}Height=\"450\""
            : $"\n{Pad(root)}d:DesignWidth=\"400\"\n{Pad(root)}d:DesignHeight=\"300\"";
        var dataContext = mvvm ? $"\n    <{root}.DataContext>\n        <vm:{vmClass} />\n    </{root}.DataContext>" : "";
        return $"""
                <{root} xmlns="{XamlNamespaceMap.Avalonia}"
                {Pad(root)}xmlns:x="{XamlNamespaceMap.Xaml}"
                {Pad(root)}xmlns:d="{XamlNamespaceMap.Blend}"
                {Pad(root)}xmlns:mc="{XamlNamespaceMap.MarkupCompatibility}"{vmNs}
                {Pad(root)}mc:Ignorable="d"
                {Pad(root)}x:Class="{xClass}"{dataType}{size}>{dataContext}
                    <Canvas />
                </{root}>

                """;
    }

    private static string Pad(string root) => new(' ', root.Length + 2);
}
