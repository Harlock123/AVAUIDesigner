using AVAUIDesigner.Engine.Descriptors;

namespace AVAUIDesigner.Engine.Plugins;

/// <summary>Supplies control descriptors (and optionally toolbox categories) from a plugin assembly.</summary>
public interface IControlDescriptorPlugin
{
    string PluginName { get; }
    string PluginVersion { get; }
    IEnumerable<ControlDescriptor> GetDescriptors();
    IEnumerable<ToolboxCategory> GetToolboxCategories();
}

/// <summary>
/// Registers a custom property editor control for a property type. <see cref="EditorControlType"/> must be
/// an Avalonia Control subclass exposing a <c>Value</c> StyledProperty&lt;object?&gt; holding the XAML string.
/// </summary>
public interface IPropertyEditorPlugin
{
    Type PropertyType { get; }
    Type EditorControlType { get; }
}

public interface IPluginLoader
{
    string PluginDirectory { get; }
    IReadOnlyList<IControlDescriptorPlugin> DescriptorPlugins { get; }
    IReadOnlyList<IPropertyEditorPlugin> PropertyEditorPlugins { get; }
    IReadOnlyList<string> Errors { get; }

    /// <summary>Loads every plugin assembly found and registers its descriptors.</summary>
    void LoadAll(ControlDescriptorRegistry registry);
}
