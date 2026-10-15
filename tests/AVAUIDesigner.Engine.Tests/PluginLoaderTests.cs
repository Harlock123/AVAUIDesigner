using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Plugins;
using Xunit;

namespace AVAUIDesigner.Engine.Tests;

public class SamplePlugin : IControlDescriptorPlugin
{
    public string PluginName => "Sample";
    public string PluginVersion => "1.0";

    public IEnumerable<ControlDescriptor> GetDescriptors() =>
        [new ControlDescriptor("Gauge", "Sample.Controls.Gauge", "Sample Controls") { Description = "A gauge." }];

    public IEnumerable<ToolboxCategory> GetToolboxCategories() => [new ToolboxCategory("Sample Controls")];
}

public class SampleEditorPlugin : IPropertyEditorPlugin
{
    public Type PropertyType => typeof(TimeSpan);
    public Type EditorControlType => typeof(object);
}

public class PluginLoaderTests
{
    [Fact]
    public void LoadAssembly_RegistersDescriptorsCategoriesAndEditors()
    {
        var registry = new ControlDescriptorRegistry();
        var changed = 0;
        registry.Changed += (_, _) => changed++;
        var loader = new FileSystemPluginLoader(Path.Combine(Path.GetTempPath(), "no-such-plugin-dir"));
        loader.LoadAssembly(typeof(PluginLoaderTests).Assembly, registry);

        Assert.NotNull(registry.GetByElementName("Gauge"));
        Assert.True(changed > 0);
        Assert.Contains(loader.DescriptorPlugins, p => p.PluginName == "Sample");
        Assert.Contains(loader.ToolboxCategories, c => c.Name == "Sample Controls");
        Assert.Contains(loader.PropertyEditorPlugins, p => p.PropertyType == typeof(TimeSpan));
        Assert.Empty(loader.Errors);
    }

    [Fact]
    public void LoadAll_MissingDirectory_IsNotAnError()
    {
        var loader = new FileSystemPluginLoader(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        loader.LoadAll(new ControlDescriptorRegistry());
        Assert.Empty(loader.Errors);
    }
}
