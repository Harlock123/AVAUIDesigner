using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Engine.UndoRedo.Commands;

/// <summary>
/// Replaces the whole document tree (used when the XAML source is edited by hand),
/// including document-level metadata.
/// </summary>
public class ReplaceRootCommand : IDesignCommand
{
    private readonly DesignDocument _source;
    private DesignElement? _oldRoot;
    private string? _oldXClass;
    private List<XamlNamespaceDeclaration>? _oldNamespaces;

    public ReplaceRootCommand(DesignDocument parsed, string description = "Edit XAML")
    {
        _source = parsed;
        Description = description;
    }

    public string Description { get; }
    public DesignElement NewRoot => _source.RootElement;

    public void Execute(DesignDocument document)
    {
        _oldRoot = document.RootElement;
        _oldXClass = document.XClass;
        _oldNamespaces = document.Namespaces.ToList();
        Apply(document, _source.RootElement, _source.XClass, _source.Namespaces.ToList());
    }

    public void Undo(DesignDocument document)
    {
        if (_oldRoot is null) return;
        Apply(document, _oldRoot, _oldXClass, _oldNamespaces!);
    }

    private static void Apply(DesignDocument document, DesignElement root, string? xClass, List<XamlNamespaceDeclaration> namespaces)
    {
        document.RootElement = root;
        document.XClass = xClass;
        document.Namespaces.Clear();
        document.Namespaces.AddRange(namespaces);
    }
}

/// <summary>Replaces a whole property (attribute or element form) with a new value; used by the grid definition and collection editors.</summary>
public class ReplacePropertyCommand : IDesignCommand
{
    private DesignProperty? _old;
    private int _oldIndex = -1;

    public ReplacePropertyCommand(DesignElement element, string propertyName, DesignProperty? newProperty, string? description = null)
    {
        Element = element;
        PropertyName = propertyName;
        NewProperty = newProperty;
        Description = description ?? $"Edit {element.DisplayName}.{propertyName}";
    }

    public DesignElement Element { get; }
    public string PropertyName { get; }
    public DesignProperty? NewProperty { get; }
    public string Description { get; }

    public void Execute(DesignDocument document)
    {
        _old = Element.Properties.FirstOrDefault(p => p.Name == PropertyName);
        _oldIndex = _old is null ? -1 : Element.Properties.IndexOf(_old);
        if (_old is not null) Element.Properties.Remove(_old);
        if (NewProperty is not null)
        {
            foreach (var e in NewProperty.ElementValues)
            {
                e.Parent = Element;
                e.OwnerProperty = NewProperty;
            }
            if (_oldIndex >= 0) Element.Properties.Insert(_oldIndex, NewProperty);
            else Element.Properties.Add(NewProperty);
        }
    }

    public void Undo(DesignDocument document)
    {
        if (NewProperty is not null) Element.Properties.Remove(NewProperty);
        if (_old is not null)
        {
            if (_oldIndex >= 0 && _oldIndex <= Element.Properties.Count) Element.Properties.Insert(_oldIndex, _old);
            else Element.Properties.Add(_old);
        }
    }
}
