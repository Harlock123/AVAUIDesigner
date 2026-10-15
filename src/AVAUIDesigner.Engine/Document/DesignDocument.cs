using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Serialization;
using ReactiveUI;

namespace AVAUIDesigner.Engine.Document;

/// <summary>The in-memory DOM of one .axaml file.</summary>
public class DesignDocument : ReactiveObject
{
    private string _filePath = string.Empty;
    private string? _xClass;
    private DesignElement _rootElement;
    private bool _isDirty;
    private CodeBehindStyle _codeStyle = CodeBehindStyle.CodeBehind;

    public DesignDocument(DesignElement rootElement)
    {
        _rootElement = rootElement;
        Namespaces = XamlNamespaceMap.DefaultDeclarations().ToList();
    }

    /// <summary>Full path to the .axaml file; empty for an unsaved document.</summary>
    public string FilePath
    {
        get => _filePath;
        set
        {
            this.RaiseAndSetIfChanged(ref _filePath, value);
            this.RaisePropertyChanged(nameof(FileName));
        }
    }

    public string FileName => string.IsNullOrEmpty(FilePath) ? "Untitled.axaml" : Path.GetFileName(FilePath);

    /// <summary>x:Class value, e.g. "MyApp.Views.MainWindow".</summary>
    public string? XClass
    {
        get => _xClass;
        set => this.RaiseAndSetIfChanged(ref _xClass, value);
    }

    /// <summary>"Window", "UserControl", etc.</summary>
    public string RootElementType
    {
        get => RootElement.ElementType;
        set => RootElement.ElementType = value;
    }

    public DesignElement RootElement
    {
        get => _rootElement;
        set
        {
            value.Parent = null;
            this.RaiseAndSetIfChanged(ref _rootElement, value);
            this.RaisePropertyChanged(nameof(RootElementType));
        }
    }

    /// <summary>Unsaved changes exist.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    /// <summary>Code-behind style used when saving (MVVM also produces a ViewModel).</summary>
    public CodeBehindStyle CodeStyle
    {
        get => _codeStyle;
        set => this.RaiseAndSetIfChanged(ref _codeStyle, value);
    }

    /// <summary>From project context.</summary>
    public string? AssemblyName { get; set; }

    public List<XamlNamespaceDeclaration> Namespaces { get; }

    /// <summary>Flat lookup by x:Name.</summary>
    public IReadOnlyDictionary<string, DesignElement> NamedElements
    {
        get
        {
            var map = new Dictionary<string, DesignElement>(StringComparer.Ordinal);
            foreach (var e in RootElement.SelfAndAllDescendants())
                if (e.Name is { } n) map.TryAdd(n, e);
            return map;
        }
    }

    /// <summary>All elements in tree order (including those inside property elements).</summary>
    public IEnumerable<DesignElement> AllElements => RootElement.SelfAndAllDescendants();

    /// <summary>Class name part of x:Class, or the file name.</summary>
    public string ClassName
    {
        get
        {
            if (!string.IsNullOrEmpty(XClass))
            {
                var dot = XClass.LastIndexOf('.');
                return dot >= 0 ? XClass[(dot + 1)..] : XClass;
            }
            var name = Path.GetFileNameWithoutExtension(FileName);
            return CodeIdentifiers.MakeIdentifier(name);
        }
    }

    /// <summary>Namespace part of x:Class.</summary>
    public string? ClassNamespace
    {
        get
        {
            if (string.IsNullOrEmpty(XClass)) return null;
            var dot = XClass.LastIndexOf('.');
            return dot > 0 ? XClass[..dot] : null;
        }
    }

    public string? GetNamespaceUri(string prefix) => Namespaces.FirstOrDefault(n => n.Prefix == prefix)?.Uri;

    public void SetNamespace(string prefix, string uri)
    {
        Namespaces.RemoveAll(n => n.Prefix == prefix);
        Namespaces.Add(new XamlNamespaceDeclaration(prefix, uri));
    }

    /// <summary>Generates a unique x:Name such as "button1".</summary>
    public string GenerateUniqueName(string elementType)
    {
        var bare = elementType.Contains(':') ? elementType[(elementType.IndexOf(':') + 1)..] : elementType;
        var stem = char.ToLowerInvariant(bare[0]) + bare[1..];
        var names = NamedElements;
        for (var i = 1; ; i++)
        {
            var candidate = $"{stem}{i}";
            if (!names.ContainsKey(candidate)) return candidate;
        }
    }

    /// <summary>Makes <paramref name="name"/> unique among existing names by appending a number.</summary>
    public string MakeUniqueName(string name, DesignElement? ignore = null)
    {
        var names = NamedElements;
        if (!names.TryGetValue(name, out var holder) || ReferenceEquals(holder, ignore)) return name;
        var stem = name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        if (stem.Length == 0) stem = name;
        for (var i = 1; ; i++)
        {
            var candidate = $"{stem}{i}";
            if (!names.ContainsKey(candidate)) return candidate;
        }
    }
}
