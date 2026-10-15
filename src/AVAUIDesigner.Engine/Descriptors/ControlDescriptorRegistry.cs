using System.Reflection;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Plugins;

namespace AVAUIDesigner.Engine.Descriptors;

/// <summary>
/// Marks a <see cref="ControlDescriptor"/> subclass (with a parameterless constructor) or an
/// <see cref="IControlDescriptorPlugin"/> implementation for discovery by
/// <see cref="ControlDescriptorRegistry.RegisterAssembly"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ControlDescriptorAttribute : Attribute;

public class ControlDescriptorRegistry
{
    private readonly Dictionary<string, ControlDescriptor> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, ControlDescriptor> _byType = new();
    private readonly List<ControlDescriptor> _ordered = [];
    private readonly List<PropertyDescriptor> _globalAttached = [];

    /// <summary>Raised whenever descriptors are added (e.g. by plugins) so the toolbox can refresh.</summary>
    public event EventHandler? Changed;

    /// <summary>Attached properties available on every visual control (ToolTip.Tip, ...).</summary>
    public IReadOnlyList<PropertyDescriptor> GlobalAttachedProperties => _globalAttached;

    public void Register(ControlDescriptor descriptor)
    {
        if (_byName.TryGetValue(descriptor.ElementName, out var existing))
            _ordered.Remove(existing);
        _byName[descriptor.ElementName] = descriptor;
        if (descriptor.ClrType is not null) _byType[descriptor.ClrType] = descriptor;
        _ordered.Add(descriptor);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RegisterGlobalAttachedProperty(PropertyDescriptor property)
    {
        _globalAttached.RemoveAll(p => p.FullName == property.FullName);
        _globalAttached.Add(property);
    }

    /// <summary>Scans an assembly for types marked with <see cref="ControlDescriptorAttribute"/>.</summary>
    public int RegisterAssembly(Assembly assembly)
    {
        var count = 0;
        foreach (var type in SafeGetTypes(assembly))
        {
            if (type.IsAbstract || type.GetCustomAttribute<ControlDescriptorAttribute>() is null) continue;
            if (type.GetConstructor(Type.EmptyTypes) is null) continue;

            if (typeof(ControlDescriptor).IsAssignableFrom(type))
            {
                Register((ControlDescriptor)Activator.CreateInstance(type)!);
                count++;
            }
            else if (typeof(IControlDescriptorPlugin).IsAssignableFrom(type))
            {
                var plugin = (IControlDescriptorPlugin)Activator.CreateInstance(type)!;
                foreach (var d in plugin.GetDescriptors())
                {
                    Register(d);
                    count++;
                }
            }
        }
        return count;
    }

    public ControlDescriptor? GetByElementName(string name)
    {
        if (_byName.TryGetValue(name, out var d)) return d;
        // Prefixed names ("controls:Foo") fall back to the bare name.
        var colon = name.IndexOf(':');
        return colon >= 0 && _byName.TryGetValue(name[(colon + 1)..], out d) ? d : null;
    }

    public ControlDescriptor? GetByType(Type type) => _byType.GetValueOrDefault(type);

    public IReadOnlyList<ControlDescriptor> GetAll() => _ordered;

    public IReadOnlyList<ControlDescriptor> GetByCategory(string category) =>
        _ordered.Where(d => d.Category == category).ToList();

    /// <summary>
    /// All attached properties that apply to <paramref name="element"/>: those contributed by its
    /// parent container plus the global ones.
    /// </summary>
    public IReadOnlyList<PropertyDescriptor> GetAttachedPropertiesFor(DesignElement element)
    {
        var result = new List<PropertyDescriptor>();
        if (element.Parent?.Descriptor is { } parent && element.OwnerProperty is null)
            result.AddRange(parent.ChildAttachedProperties);
        if (element.Descriptor is { RenderAsPlaceholder: false })
            result.AddRange(_globalAttached);
        return result;
    }

    /// <summary>Finds an attached property descriptor anywhere in the catalog by full name ("Grid.Row").</summary>
    public PropertyDescriptor? FindAttachedProperty(string fullName)
    {
        foreach (var p in _globalAttached)
            if (p.FullName == fullName) return p;
        foreach (var d in _ordered)
        foreach (var p in d.ChildAttachedProperties)
            if (p.FullName == fullName) return p;
        return null;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
