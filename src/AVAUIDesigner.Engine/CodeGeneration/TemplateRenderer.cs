using System.Collections.Concurrent;
using Scriban;
using Scriban.Runtime;

namespace AVAUIDesigner.Engine.CodeGeneration;

/// <summary>Loads Scriban templates embedded in this assembly and renders them.</summary>
internal static class TemplateRenderer
{
    private static readonly ConcurrentDictionary<string, Template> Cache = new();

    public static string Render(string templateName, ScriptObject model)
    {
        var template = Cache.GetOrAdd(templateName, Load);
        var context = new TemplateContext { StrictVariables = true, NewLine = "\n" };
        context.PushGlobal(model);
        var text = template.Render(context);
        return text.Replace("\r\n", "\n").TrimEnd() + "\n";
    }

    private static Template Load(string name)
    {
        var assembly = typeof(TemplateRenderer).Assembly;
        var resource = assembly.GetManifestResourceNames().FirstOrDefault(r => r.EndsWith("." + name, StringComparison.Ordinal))
                       ?? throw new InvalidOperationException($"Template '{name}' is not embedded.");
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var template = Template.Parse(reader.ReadToEnd(), name);
        if (template.HasErrors)
            throw new InvalidOperationException($"Template '{name}' has errors: {string.Join("; ", template.Messages)}");
        return template;
    }
}
