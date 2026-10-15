using System.Reflection;
using AVAUIDesigner.Engine.Descriptors;

namespace AVAUIDesigner.Engine.Plugins;

/// <summary>Loads plugin assemblies from {AppData}/AVAUIDesigner/Plugins.</summary>
public class FileSystemPluginLoader : IPluginLoader
{
    private readonly List<IControlDescriptorPlugin> _descriptorPlugins = [];
    private readonly List<IPropertyEditorPlugin> _editorPlugins = [];
    private readonly List<string> _errors = [];

    public FileSystemPluginLoader() : this(Path.Combine(Settings.DesignerSettings.DefaultDirectory, "Plugins"))
    {
    }

    public FileSystemPluginLoader(string pluginDirectory)
    {
        PluginDirectory = pluginDirectory;
    }

    public string PluginDirectory { get; }
    public IReadOnlyList<IControlDescriptorPlugin> DescriptorPlugins => _descriptorPlugins;
    public IReadOnlyList<IPropertyEditorPlugin> PropertyEditorPlugins => _editorPlugins;
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>Toolbox categories contributed by the loaded plugins.</summary>
    public IEnumerable<ToolboxCategory> ToolboxCategories => _descriptorPlugins.SelectMany(SafeCategories);

    public void LoadAll(ControlDescriptorRegistry registry)
    {
        if (!Directory.Exists(PluginDirectory)) return;
        foreach (var dll in Directory.EnumerateFiles(PluginDirectory, "*.dll"))
        {
            try
            {
                LoadAssembly(Assembly.LoadFrom(dll), registry);
            }
            catch (Exception ex)
            {
                _errors.Add($"{Path.GetFileName(dll)}: {ex.Message}");
            }
        }
    }

    public void LoadAssembly(Assembly assembly, ControlDescriptorRegistry registry)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }

        foreach (var type in types)
        {
            if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) is null) continue;
            try
            {
                if (typeof(IControlDescriptorPlugin).IsAssignableFrom(type))
                {
                    var plugin = (IControlDescriptorPlugin)Activator.CreateInstance(type)!;
                    foreach (var d in plugin.GetDescriptors()) registry.Register(d);
                    _descriptorPlugins.Add(plugin);
                }
                if (typeof(IPropertyEditorPlugin).IsAssignableFrom(type))
                    _editorPlugins.Add((IPropertyEditorPlugin)Activator.CreateInstance(type)!);
            }
            catch (Exception ex)
            {
                _errors.Add($"{type.FullName}: {ex.Message}");
            }
        }
    }

    private IEnumerable<ToolboxCategory> SafeCategories(IControlDescriptorPlugin plugin)
    {
        try
        {
            return plugin.GetToolboxCategories().ToList();
        }
        catch (Exception ex)
        {
            _errors.Add($"{plugin.PluginName}: {ex.Message}");
            return [];
        }
    }
}
