using System.Collections.ObjectModel;
using Avalonia;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Properties;
using ReactiveUI;

namespace AVAUIDesigner.Engine.Document;

/// <summary>
/// One node of the in-memory design DOM. Mirrors a XAML element: its attributes, attached
/// properties, event handlers, property elements and child elements.
/// </summary>
public class DesignElement : ReactiveObject
{
    private string? _name;
    private bool _isLocked;
    private bool _isVisible = true;
    private bool _isSelected;

    public DesignElement(string elementType, ControlDescriptor? descriptor = null)
    {
        ElementType = elementType;
        Descriptor = descriptor;
        ClrType = descriptor?.FullTypeName;
    }

    /// <summary>XAML element name, e.g. "Button" (may carry a prefix: "local:MyControl").</summary>
    public string ElementType { get; set; }

    /// <summary>Fully qualified CLR type, if known.</summary>
    public string? ClrType { get; set; }

    /// <summary>The x:Name value.</summary>
    public string? Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <summary>Null when the element type is unknown to the registry.</summary>
    public ControlDescriptor? Descriptor { get; internal set; }

    /// <summary>Element type not found in the registry; rendered as a placeholder.</summary>
    public bool IsUnknown => Descriptor is null;

    public ObservableCollection<DesignProperty> Properties { get; } = [];
    public ObservableCollection<DesignAttachedProperty> AttachedProperties { get; } = [];
    public ObservableCollection<DesignEventHandler> EventHandlers { get; } = [];
    public ObservableCollection<DesignElement> Children { get; } = [];

    public DesignElement? Parent { get; internal set; }

    /// <summary>When the element lives inside a property element (e.g. a ContextMenu), that property.</summary>
    public DesignProperty? OwnerProperty { get; internal set; }

    /// <summary>Text content that could not be mapped to a content property.</summary>
    public string? InnerText { get; set; }

    // ---- Designer state (not serialized) ----

    public bool IsLocked
    {
        get => _isLocked;
        set => this.RaiseAndSetIfChanged(ref _isLocked, value);
    }

    /// <summary>Hidden on the canvas (still in XAML).</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    // ---- Layout convenience ----

    public double Width
    {
        get => GetDouble("Width");
        set => SetDouble("Width", value);
    }

    public double Height
    {
        get => GetDouble("Height");
        set => SetDouble("Height", value);
    }

    /// <summary>Canvas.Left.</summary>
    public double Left
    {
        get => GetDouble("Canvas.Left");
        set => SetDouble("Canvas.Left", value);
    }

    /// <summary>Canvas.Top.</summary>
    public double Top
    {
        get => GetDouble("Canvas.Top");
        set => SetDouble("Canvas.Top", value);
    }

    public Thickness Margin
    {
        get => GetValue("Margin") is { } s && XamlValueConverter.TryDeserialize(s, typeof(Thickness), out var t)
            ? (Thickness)t!
            : default;
        set => SetValue("Margin", value == default ? null : XamlValueConverter.Serialize(value, typeof(Thickness)));
    }

    /// <summary>x:Name if set, else the type name.</summary>
    public string DisplayName => Name ?? ElementType;

    public int Depth
    {
        get
        {
            var d = 0;
            for (var p = Parent; p is not null; p = p.Parent) d++;
            return d;
        }
    }

    // ---- Property access ----

    /// <summary>Gets a property's typed value (converted using the descriptor's property type).</summary>
    public object? GetProperty(string name)
    {
        var text = GetValue(name);
        if (text is null) return null;
        var type = ResolvePropertyType(name);
        if (type is null || type == typeof(string)) return text;
        return XamlValueConverter.TryDeserialize(text, type, out var value) ? value : text;
    }

    /// <summary>Sets a property from a typed value; null removes it.</summary>
    public void SetProperty(string name, object? value)
    {
        if (value is null)
        {
            RemoveProperty(name);
            return;
        }
        var text = value as string ?? XamlValueConverter.Serialize(value, ResolvePropertyType(name) ?? value.GetType());
        SetValue(name, text);
    }

    /// <summary>Resets the property to default (omits it from XAML).</summary>
    public void RemoveProperty(string name) => SetValue(name, null);

    /// <summary>
    /// Gets the raw XAML string of a property, attached property ("Grid.Row") or x:Name.
    /// Returns null when not set.
    /// </summary>
    public string? GetValue(string name)
    {
        name = NormalizeName(name);
        if (name is "x:Name" or "Name") return Name;
        if (IsAttachedName(name, out var owner, out var bare))
        {
            foreach (var ap in AttachedProperties)
                if (ap.OwnerType == owner && ap.Name == bare) return ap.StringValue;
            return null;
        }
        foreach (var p in Properties)
            if (p.Name == name && p.IsAttributeForm) return p.StringValue;
        return null;
    }

    /// <summary>Sets the raw XAML string of a property (null removes it).</summary>
    public void SetValue(string name, string? value)
    {
        name = NormalizeName(name);
        if (name is "x:Name" or "Name")
        {
            Name = value;
            return;
        }

        if (IsAttachedName(name, out var owner, out var bare))
        {
            var existing = AttachedProperties.FirstOrDefault(a => a.OwnerType == owner && a.Name == bare);
            if (value is null)
            {
                if (existing is not null) AttachedProperties.Remove(existing);
            }
            else if (existing is not null) existing.StringValue = value;
            else AttachedProperties.Add(new DesignAttachedProperty(owner, bare, value));
            return;
        }

        var prop = Properties.FirstOrDefault(p => p.Name == name);
        if (value is null)
        {
            if (prop is { IsAttributeForm: true }) Properties.Remove(prop);
            return;
        }
        if (prop is null)
        {
            Properties.Add(new DesignProperty(name, value));
        }
        else
        {
            // Setting an attribute value replaces an element-form value of the same property.
            prop.IsAttributeForm = true;
            prop.ElementValues.Clear();
            prop.ElementText = null;
            prop.StringValue = value;
            prop.TypedValue = null;
        }
    }

    public bool HasValue(string name) => GetValue(name) is not null || GetPropertyElement(name) is not null;

    // ---- Property elements ----

    public DesignProperty? GetPropertyElement(string name)
    {
        name = NormalizeName(name);
        return Properties.FirstOrDefault(p => p.Name == name && !p.IsAttributeForm);
    }

    public DesignProperty GetOrCreatePropertyElement(string name)
    {
        name = NormalizeName(name);
        var existing = Properties.FirstOrDefault(p => p.Name == name);
        if (existing is { IsAttributeForm: false }) return existing;
        if (existing is not null) Properties.Remove(existing);
        var prop = new DesignProperty(name, null) { IsAttributeForm = false };
        Properties.Add(prop);
        return prop;
    }

    public void AddToPropertyElement(DesignProperty property, DesignElement child, int index = -1)
    {
        child.Parent = this;
        child.OwnerProperty = property;
        if (index < 0 || index > property.ElementValues.Count) property.ElementValues.Add(child);
        else property.ElementValues.Insert(index, child);
    }

    public IEnumerable<DesignProperty> PropertyElements => Properties.Where(p => !p.IsAttributeForm);

    // ---- Events ----

    public string? GetEventHandler(string eventName) =>
        EventHandlers.FirstOrDefault(e => e.EventName == eventName)?.HandlerName;

    public void SetEventHandler(string eventName, string? handlerName)
    {
        var existing = EventHandlers.FirstOrDefault(e => e.EventName == eventName);
        if (string.IsNullOrWhiteSpace(handlerName))
        {
            if (existing is not null) EventHandlers.Remove(existing);
        }
        else if (existing is not null) existing.HandlerName = handlerName.Trim();
        else EventHandlers.Add(new DesignEventHandler(eventName, handlerName.Trim()));
    }

    // ---- Tree ----

    public void AddChild(DesignElement child, int index = -1)
    {
        if (child.Parent is not null) child.Detach();
        child.Parent = this;
        child.OwnerProperty = null;
        if (index < 0 || index > Children.Count) Children.Add(child);
        else Children.Insert(index, child);
    }

    public void RemoveChild(DesignElement child)
    {
        if (Children.Remove(child))
        {
            child.Parent = null;
        }
    }

    /// <summary>Removes this element from its parent (child list or property element).</summary>
    public void Detach()
    {
        if (Parent is null) return;
        if (OwnerProperty is not null)
        {
            OwnerProperty.ElementValues.Remove(this);
            OwnerProperty = null;
            Parent = null;
        }
        else
        {
            Parent.RemoveChild(this);
        }
    }

    /// <summary>Position of this element in its parent's child list (or property element).</summary>
    public int IndexInParent =>
        OwnerProperty is not null ? OwnerProperty.ElementValues.IndexOf(this) : Parent?.Children.IndexOf(this) ?? -1;

    public bool IsAncestorOf(DesignElement other)
    {
        for (var p = other.Parent; p is not null; p = p.Parent)
            if (ReferenceEquals(p, this)) return true;
        return false;
    }

    /// <summary>This element and all visual descendants (children only), in tree order.</summary>
    public IEnumerable<DesignElement> SelfAndDescendants()
    {
        yield return this;
        foreach (var child in Children)
        foreach (var d in child.SelfAndDescendants())
            yield return d;
    }

    /// <summary>This element and every descendant, including elements inside property elements.</summary>
    public IEnumerable<DesignElement> SelfAndAllDescendants()
    {
        yield return this;
        foreach (var prop in Properties)
        foreach (var pe in prop.ElementValues)
        foreach (var d in pe.SelfAndAllDescendants())
            yield return d;
        foreach (var child in Children)
        foreach (var d in child.SelfAndAllDescendants())
            yield return d;
    }

    public DesignElement Root
    {
        get
        {
            var e = this;
            while (e.Parent is not null) e = e.Parent;
            return e;
        }
    }

    /// <summary>Deep copy, detached from any parent. Designer state is reset.</summary>
    public DesignElement DeepClone()
    {
        var copy = new DesignElement(ElementType, Descriptor)
        {
            ClrType = ClrType,
            Name = Name,
            InnerText = InnerText,
        };
        foreach (var p in Properties) copy.Properties.Add(p.Clone(copy));
        foreach (var a in AttachedProperties) copy.AttachedProperties.Add(a.Clone());
        foreach (var e in EventHandlers) copy.EventHandlers.Add(e.Clone());
        foreach (var c in Children) copy.AddChild(c.DeepClone());
        return copy;
    }

    /// <summary>Re-resolves descriptors for this subtree (after parsing or plugin loading).</summary>
    public void AttachDescriptors(ControlDescriptorRegistry registry)
    {
        foreach (var e in SelfAndAllDescendants())
        {
            e.Descriptor = registry.GetByElementName(e.ElementType);
            e.ClrType ??= e.Descriptor?.FullTypeName;
        }
    }

    /// <summary>Type of a property from the descriptor/registry, or null if unknown.</summary>
    public Type? ResolvePropertyType(string name, ControlDescriptorRegistry? registry = null)
    {
        name = NormalizeName(name);
        if (IsAttachedName(name, out _, out _))
        {
            var attached = Parent?.Descriptor?.ChildAttachedProperties.FirstOrDefault(p => p.FullName == name)
                           ?? registry?.FindAttachedProperty(name);
            if (attached is not null) return attached.PropertyType;
            return name.EndsWith(".Row") || name.EndsWith(".Column") || name.EndsWith("Span") ? typeof(int)
                : name is "Canvas.Left" or "Canvas.Top" or "Canvas.Right" or "Canvas.Bottom" ? typeof(double) : null;
        }
        return Descriptor?.FindProperty(name)?.PropertyType ?? name switch
        {
            "Width" or "Height" or "MinWidth" or "MinHeight" or "MaxWidth" or "MaxHeight" => typeof(double),
            "Margin" or "Padding" => typeof(Thickness),
            _ => null,
        };
    }

    /// <summary>"Button.Content" on a Button → "Content".</summary>
    private string NormalizeName(string name)
    {
        var bare = ElementType.Contains(':') ? ElementType[(ElementType.IndexOf(':') + 1)..] : ElementType;
        return name.StartsWith(bare + ".", StringComparison.Ordinal) ? name[(bare.Length + 1)..] : name;
    }

    /// <summary>True for "Owner.Name" forms that are attached properties (not x:/d: prefixed).</summary>
    public static bool IsAttachedName(string name, out string owner, out string bare)
    {
        var dot = name.IndexOf('.');
        if (dot > 0 && !name.Contains(':') && dot < name.Length - 1)
        {
            owner = name[..dot];
            bare = name[(dot + 1)..];
            return true;
        }
        owner = bare = string.Empty;
        return false;
    }

    private double GetDouble(string name) =>
        GetValue(name) is { } s && XamlValueConverter.TryDeserialize(s, typeof(double), out var v) ? (double)v! : double.NaN;

    private void SetDouble(string name, double value) =>
        SetValue(name, double.IsNaN(value) ? null : XamlValueConverter.Serialize(value, typeof(double)));

    public override string ToString() => Name is null ? ElementType : $"{ElementType} \"{Name}\"";
}
