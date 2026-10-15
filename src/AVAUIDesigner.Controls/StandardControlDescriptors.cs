using AVAUIDesigner.Controls.Common;
using AVAUIDesigner.Engine.Descriptors;

namespace AVAUIDesigner.Controls;

/// <summary>Registers every built-in Avalonia control descriptor.</summary>
public static class StandardControlDescriptors
{
    /// <summary>Creates a registry populated with all standard descriptors.</summary>
    public static ControlDescriptorRegistry CreateRegistry()
    {
        var registry = new ControlDescriptorRegistry();
        RegisterAll(registry);
        return registry;
    }

    public static void RegisterAll(ControlDescriptorRegistry registry)
    {
        registry.RegisterAssembly(typeof(StandardControlDescriptors).Assembly);
        foreach (var p in AttachedPropertySets.Global) registry.RegisterGlobalAttachedProperty(p);
    }
}
