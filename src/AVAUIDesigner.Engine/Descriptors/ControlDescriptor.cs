using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Engine.Descriptors;

public enum ContainerLayoutMode
{
    None,
    Canvas,
    Stack,
    Grid,
    Dock,
    Wrap,
    /// <summary>Holds exactly one child (Border, ScrollViewer, ContentControl...).</summary>
    Single,
    /// <summary>Ordered list of items (TabControl, Menu, ComboBox...).</summary>
    Items,
}

/// <summary>
/// Everything the designer needs to know about a control type: the bridge between the
/// abstract design document and the concrete Avalonia type.
/// </summary>
public class ControlDescriptor
{
    private readonly Lazy<IImage?> _icon;

    public ControlDescriptor(string elementName, string fullTypeName, string category)
    {
        ElementName = elementName;
        FullTypeName = fullTypeName;
        Category = category;
        _icon = new Lazy<IImage?>(() => IconFactory?.Invoke(this));
        DefaultElementFactory = CreateDefaultElement;
    }

    public ControlDescriptor(string elementName, Type clrType, string category)
        : this(elementName, clrType.FullName ?? clrType.Name, category)
    {
        ClrType = clrType;
        ControlFactory = _ => (Control)Activator.CreateInstance(clrType)!;
    }

    /// <summary>XAML element name, e.g. "Button".</summary>
    public string ElementName { get; }

    /// <summary>CLR type, e.g. "Avalonia.Controls.Button".</summary>
    public string FullTypeName { get; }

    public Type? ClrType { get; init; }

    /// <summary>Toolbox category.</summary>
    public string Category { get; }

    public string Description { get; init; } = string.Empty;

    /// <summary>Toolbox / outline icon. Created lazily because geometry needs a running platform.</summary>
    public IImage? Icon => _icon.Value;

    /// <summary>Creates the icon on first access.</summary>
    public Func<ControlDescriptor, IImage?>? IconFactory { get; init; }

    /// <summary>Default size when dropped on the canvas. NaN components are not written.</summary>
    public Size DefaultSize { get; init; } = new(double.NaN, double.NaN);

    /// <summary>Minimum designable size enforced while resizing.</summary>
    public Size MinimumSize { get; init; } = new(4, 4);

    public IReadOnlyList<PropertyDescriptor> Properties { get; init; } = [];

    public IReadOnlyList<EventDescriptor> Events { get; init; } = [];

    /// <summary>Attached properties this container contributes to its children (Grid.Row, Canvas.Left...).</summary>
    public IReadOnlyList<PropertyDescriptor> ChildAttachedProperties { get; init; } = [];

    public bool IsContainer => LayoutMode != ContainerLayoutMode.None;

    public ContainerLayoutMode LayoutMode { get; init; } = ContainerLayoutMode.None;

    /// <summary>Which property holds children (e.g. "Children", "Items", "Content").</summary>
    public string? ContentProperty { get; init; }

    /// <summary>Element types accepted as children; null accepts any visual control.</summary>
    public IReadOnlyList<string>? AllowedChildTypes { get; init; }

    /// <summary>Maximum number of children (1 for single-content containers); null = unlimited.</summary>
    public int? MaxChildren { get; init; }

    /// <summary>Only valid as the document root (Window, UserControl).</summary>
    public bool IsRootOnly { get; init; }

    /// <summary>Can be dropped onto the design surface (false for TabItem-like items that need a specific parent).</summary>
    public bool IsToolboxVisible { get; init; } = true;

    /// <summary>
    /// When set, dropping this control onto another control assigns it to that control's
    /// property element instead of adding a child (e.g. ContextMenu → "ContextMenu", ToolTip → "ToolTip.Tip").
    /// </summary>
    public string? AttachToPropertyOnDrop { get; init; }

    /// <summary>Renders as an inert placeholder on the canvas (Popup, Flyout...).</summary>
    public bool RenderAsPlaceholder { get; init; }

    /// <summary>Name of the event a double-click on the canvas wires up (e.g. "Click").</summary>
    public string? DefaultEvent { get; init; }

    /// <summary>Property values written when a new element is created from the toolbox.</summary>
    public IReadOnlyDictionary<string, string> DefaultValues { get; init; } = new Dictionary<string, string>();

    /// <summary>Factory: creates a live Avalonia control instance for preview rendering (properties are applied by the renderer).</summary>
    public Func<DesignElement, Control>? ControlFactory { get; init; }

    /// <summary>Factory: creates a default DesignElement for this type.</summary>
    public Func<DesignElement> DefaultElementFactory { get; init; }

    /// <summary>Optional children created along with a new element (e.g. a TabControl starts with two TabItems).</summary>
    public Func<ControlDescriptorRegistry, IEnumerable<DesignElement>>? DefaultChildrenFactory { get; init; }

    public PropertyDescriptor? FindProperty(string name)
    {
        foreach (var p in Properties)
            if (p.Name == name) return p;
        return null;
    }

    public EventDescriptor? FindEvent(string name)
    {
        foreach (var e in Events)
            if (e.Name == name) return e;
        return null;
    }

    /// <summary>
    /// Whether this attach-on-drop control (ContextMenu, Flyout, ToolTip) can be attached to <paramref name="host"/>:
    /// attached properties ("ToolTip.Tip") fit any control; others need the host type to have the property.
    /// </summary>
    public bool CanAttachTo(DesignElement host)
    {
        if (AttachToPropertyOnDrop is not { } prop || host.Descriptor is not { RenderAsPlaceholder: false } hd) return false;
        if (hd.ClrType is not { } type || !typeof(Control).IsAssignableFrom(type)) return false;
        return prop.Contains('.') || type.GetProperty(prop) is not null;
    }

    public bool AcceptsChild(string elementType)
    {
        if (!IsContainer) return false;
        if (AllowedChildTypes is null) return true;
        var bare = elementType.Contains(':') ? elementType[(elementType.IndexOf(':') + 1)..] : elementType;
        return AllowedChildTypes.Contains(bare);
    }

    private DesignElement CreateDefaultElement()
    {
        var element = new DesignElement(ElementName, this);
        // Dropped controls start at the descriptor's default size (NaN components size to content).
        if (!double.IsNaN(DefaultSize.Width)) element.Width = DefaultSize.Width;
        if (!double.IsNaN(DefaultSize.Height)) element.Height = DefaultSize.Height;
        foreach (var (name, value) in DefaultValues)
            element.SetValue(name, value);
        return element;
    }

    public override string ToString() => ElementName;
}
