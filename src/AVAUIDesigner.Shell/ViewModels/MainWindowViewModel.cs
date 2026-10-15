using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Plugins;
using AVAUIDesigner.Engine.Project;
using AVAUIDesigner.Engine.Selection;
using AVAUIDesigner.Engine.Serialization;
using AVAUIDesigner.Engine.Settings;
using AVAUIDesigner.Engine.UndoRedo;
using AVAUIDesigner.Engine.UndoRedo.Commands;
using AVAUIDesigner.Shell.Controls;
using AVAUIDesigner.Shell.Design;
using AVAUIDesigner.Shell.Services;
using ReactiveUI;

namespace AVAUIDesigner.Shell.ViewModels;

/// <summary>A file of the open project, for the Project panel.</summary>
public record ProjectFileItem(string FullPath, string RelativePath);

/// <summary>Notification bar content (external modification, crash recovery...).</summary>
public class NotificationViewModel(string message, string primaryText, Action primary, string secondaryText, Action secondary)
{
    public string Message { get; } = message;
    public string PrimaryText { get; } = primaryText;
    public string SecondaryText { get; } = secondaryText;
    public ReactiveCommand<Unit, Unit> PrimaryCommand { get; } = ReactiveCommand.Create(primary);
    public ReactiveCommand<Unit, Unit> SecondaryCommand { get; } = ReactiveCommand.Create(secondary);
}

public enum AlignKind
{
    Left,
    Center,
    Right,
    Top,
    Middle,
    Bottom,
}

/// <summary>Root view model: owns the document, undo stack, selection and all panels.</summary>
public partial class MainWindowViewModel : ReactiveObject, IPropertyGridHost, IDisposable
{
    public static readonly IReadOnlyList<string> ZoomOptions = ["25%", "50%", "75%", "100%", "150%", "200%", "Fit"];

    private readonly CodeBehindGenerator _codeBehindGenerator;
    private readonly ViewModelGenerator _viewModelGenerator;
    private readonly IPluginLoader? _pluginLoader;
    private readonly FileWatcherService _watcher = new();
    private readonly Dictionary<Type, Type> _customEditors = new();
    private DesignDocument? _currentDocument;
    private ProjectContext? _projectContext;
    private string? _existingCodeBehind;
    private string? _existingViewModel;
    private bool _applyingSourceEdit;
    private string _codeBehindText = string.Empty;
    private string _viewModelText = string.Empty;
    private int _bottomTab;
    private double _zoomFactor = 1;
    private string _cursorText = string.Empty;
    private string _statusMessage = "Ready";
    private NotificationViewModel? _notification;
    private string? _clipboardFallback;
    private bool _showToolbox = true;
    private bool _showProperties = true;
    private bool _showOutline = true;
    private int _leftTab;

    public MainWindowViewModel(ControlDescriptorRegistry registry, XamlParser parser, XamlSerializer serializer,
        CodeBehindGenerator codeBehindGenerator, ViewModelGenerator viewModelGenerator, DesignerSettings settings,
        IPluginLoader? pluginLoader = null)
    {
        Registry = registry;
        Parser = parser;
        Serializer = serializer;
        _codeBehindGenerator = codeBehindGenerator;
        _viewModelGenerator = viewModelGenerator;
        Settings = settings;
        _pluginLoader = pluginLoader;

        SelectionManager = new SelectionManager();
        UndoRedo = new UndoRedoStack();
        Toolbox = new ToolboxViewModel(registry, AddControl) { ExpandedCategories = settings.Ui.ToolboxExpanded };
        Toolbox.Rebuild();
        PropertyGrid = new PropertyGridViewModel(this);
        DocumentOutline = new DocumentOutlineViewModel(SelectionManager, ReparentFromOutline);
        XamlSource = new XamlSourceViewModel(parser, registry) { ApplyParsed = ApplySourceEdit };
        XamlSource.IsSplitViewVisible = settings.Ui.PanelLayout.ShowSource;
        _showToolbox = settings.Ui.PanelLayout.ShowToolbox;
        _showProperties = settings.Ui.PanelLayout.ShowProperties;
        _showOutline = settings.Ui.PanelLayout.ShowOutline;
        _zoomFactor = settings.Design.DefaultZoom;
        RecentFiles = new ObservableCollection<string>(settings.Ui.RecentFiles);

        UndoRedo.CommandApplied += (_, _) => OnDocumentChanged();
        SelectionManager.SelectionChanged += (_, _) => OnSelectionChanged();
        XamlSource.WhenAnyValue(x => x.HasParseError, x => x.Warnings).Subscribe(_ => RaiseStatus());
        _watcher.ExternallyModified += (_, path) => OnExternallyModified(path);

        CreateCommands();
    }

    // ------------------------------------------------------------------ services & state

    public ControlDescriptorRegistry Registry { get; }
    public XamlParser Parser { get; }
    public XamlSerializer Serializer { get; }
    public DesignerSettings Settings { get; private set; }

    /// <summary>Set by the view.</summary>
    public IDialogService? Dialogs { get; set; }

    /// <summary>Set by the view; falls back to an in-process clipboard.</summary>
    public IClipboardService? Clipboard { get; set; }

    /// <summary>Set by the view: layout information from the design canvas.</summary>
    public IDesignSurface? Surface { get; set; }

    public IReadOnlyDictionary<Type, Type> CustomEditors => _customEditors;

    public ToolboxViewModel Toolbox { get; }
    public PropertyGridViewModel PropertyGrid { get; }
    public DocumentOutlineViewModel DocumentOutline { get; }
    public XamlSourceViewModel XamlSource { get; }
    public SelectionManager SelectionManager { get; }
    public UndoRedoStack UndoRedo { get; }

    public DesignDocument? CurrentDocument
    {
        get => _currentDocument;
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentDocument, value);
            this.RaisePropertyChanged(nameof(HasDocument));
            this.RaisePropertyChanged(nameof(Title));
            this.RaisePropertyChanged(nameof(IsMvvm));
            this.RaisePropertyChanged(nameof(BaseDirectory));
        }
    }

    DesignDocument? IPropertyGridHost.Document => CurrentDocument;

    public bool HasDocument => CurrentDocument is not null;

    public ProjectContext? ProjectContext
    {
        get => _projectContext;
        private set
        {
            this.RaiseAndSetIfChanged(ref _projectContext, value);
            this.RaisePropertyChanged(nameof(HasProject));
            this.RaisePropertyChanged(nameof(ProjectName));
        }
    }

    public bool HasProject => ProjectContext is { IsStandalone: false };
    public string ProjectName => ProjectContext?.ProjectFilePath is { } p ? Path.GetFileName(p) : "No project";

    public ObservableCollection<ProjectFileItem> ProjectFiles { get; } = [];
    public ObservableCollection<string> RecentFiles { get; }

    public string Title => CurrentDocument is { } d
        ? $"{d.FileName}{(d.IsDirty ? " *" : string.Empty)} — AVAUIDesigner"
        : "AVAUIDesigner";

    public bool IsMvvm => CurrentDocument?.CodeStyle == CodeBehindStyle.MVVM;

    /// <summary>Edit › Undo header, e.g. "Undo Move button1".</summary>
    public string UndoHeader => UndoRedo.CanUndo ? $"_Undo {UndoRedo.UndoDescription}" : "_Undo";

    public string RedoHeader => UndoRedo.CanRedo ? $"_Redo {UndoRedo.RedoDescription}" : "_Redo";

    /// <summary>Directory of the open file (resolves relative image paths on the canvas).</summary>
    public string? BaseDirectory => string.IsNullOrEmpty(CurrentDocument?.FilePath) ? null : Path.GetDirectoryName(CurrentDocument.FilePath);

    public string CodeBehindText
    {
        get => _codeBehindText;
        private set => this.RaiseAndSetIfChanged(ref _codeBehindText, value);
    }

    public string ViewModelText
    {
        get => _viewModelText;
        private set => this.RaiseAndSetIfChanged(ref _viewModelText, value);
    }

    /// <summary>Bottom pane tab: 0 = XAML, 1 = code-behind, 2 = ViewModel.</summary>
    public int BottomTab
    {
        get => _bottomTab;
        set => this.RaiseAndSetIfChanged(ref _bottomTab, value);
    }

    /// <summary>Left pane tab: 0 = Toolbox, 1 = Project.</summary>
    public int LeftTab
    {
        get => _leftTab;
        set => this.RaiseAndSetIfChanged(ref _leftTab, value);
    }

    public NotificationViewModel? Notification
    {
        get => _notification;
        private set => this.RaiseAndSetIfChanged(ref _notification, value);
    }

    // ---- view options (bound to the canvas, persisted in settings)

    public double ZoomFactor
    {
        get => _zoomFactor;
        set
        {
            this.RaiseAndSetIfChanged(ref _zoomFactor, value);
            this.RaisePropertyChanged(nameof(ZoomText));
        }
    }

    public string ZoomText => $"{Math.Round(ZoomFactor * 100)}%";

    /// <summary>Zoom selector in the toolbar.</summary>
    public string? SelectedZoomOption
    {
        get => ZoomOptions.FirstOrDefault(o => o == ZoomText);
        set
        {
            if (value is null) return;
            if (value == "Fit") Surface?.ZoomToFit();
            else if (double.TryParse(value.TrimEnd('%'), out var pct)) ZoomFactor = pct / 100;
            this.RaisePropertyChanged();
        }
    }

    public bool SnapToGrid
    {
        get => Settings.Design.SnapToGrid;
        set
        {
            Settings.Design.SnapToGrid = value;
            this.RaisePropertyChanged();
        }
    }

    public bool ShowGrid
    {
        get => Settings.Design.ShowGrid;
        set
        {
            Settings.Design.ShowGrid = value;
            this.RaisePropertyChanged();
        }
    }

    public bool SmartSnap => Settings.Design.SmartSnapLines;
    public double GridSize => Settings.Design.GridSize;
    public double SnapThreshold => Settings.Design.SnapThreshold;

    public bool ShowToolbox
    {
        get => _showToolbox;
        set => this.RaiseAndSetIfChanged(ref _showToolbox, value);
    }

    public bool ShowProperties
    {
        get => _showProperties;
        set => this.RaiseAndSetIfChanged(ref _showProperties, value);
    }

    public bool ShowOutline
    {
        get => _showOutline;
        set => this.RaiseAndSetIfChanged(ref _showOutline, value);
    }

    // ---- status bar

    public string ElementCountText => CurrentDocument is { } d ? $"{d.AllElements.Count()} elements" : "No document";

    public string CursorText
    {
        get => _cursorText;
        private set => this.RaiseAndSetIfChanged(ref _cursorText, value);
    }

    public string ErrorsText => XamlSource.HasParseError ? "XAML error"
        : XamlSource.Warnings.Count > 0 ? $"{XamlSource.Warnings.Count} warning(s)" : "No errors";

    public bool HasErrors => XamlSource.HasParseError || XamlSource.Warnings.Count > 0;

    public string SelectionText => SelectionManager.SelectedElements.Count switch
    {
        0 => string.Empty,
        1 => SelectionManager.PrimarySelection!.ToString(),
        var n => $"{n} selected",
    };

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    /// <summary>Raised when the canvas must be rebuilt from the document.</summary>
    public event EventHandler? DesignInvalidated;

    /// <summary>Raised to show text in the code-behind (or ViewModel) tab: the text to search for.</summary>
    public event EventHandler<string>? CodeNavigateRequested;

    /// <summary>Raised to move keyboard focus to the property grid.</summary>
    public event EventHandler? FocusPropertiesRequested;

    /// <summary>Raised when the application should exit (File › Exit).</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Raised after the settings changed (theme, snapping...).</summary>
    public event EventHandler? SettingsChanged;

    // ------------------------------------------------------------------ startup

    /// <summary>Loads plugins and opens the initial document (or a file passed on the command line).</summary>
    public void Initialize(string? fileToOpen = null)
    {
        if (_pluginLoader is not null)
        {
            try
            {
                _pluginLoader.LoadAll(Registry);
                if (_pluginLoader is FileSystemPluginLoader fs) Toolbox.PluginCategories.AddRange(fs.ToolboxCategories);
                foreach (var ep in _pluginLoader.PropertyEditorPlugins) _customEditors[ep.PropertyType] = ep.EditorControlType;
                Toolbox.Rebuild();
                if (_pluginLoader.Errors.Count > 0) StatusMessage = $"Plugin errors: {string.Join("; ", _pluginLoader.Errors)}";
                else if (_pluginLoader.DescriptorPlugins.Count > 0) StatusMessage = $"Loaded {_pluginLoader.DescriptorPlugins.Count} plugin(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Plugin loading failed: {ex.Message}";
            }
        }

        if (fileToOpen is not null && File.Exists(fileToOpen))
        {
            if (fileToOpen.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) _ = OpenProjectAsync(fileToOpen);
            else _ = LoadFileAsync(fileToOpen);
        }
        else
        {
            NewDocument(NewDocumentKind.Window, Settings.CodeGen.DefaultStyleValue == CodeBehindStyle.MVVM, prompt: false);
        }
    }

    /// <summary>Offers to restore a crash-recovery copy.</summary>
    public void OfferRecovery(RecoveryService recovery)
    {
        if (recovery.TryGetRecovery() is not { } info) return;
        var name = info.OriginalPath is null ? "an untitled document" : Path.GetFileName(info.OriginalPath);
        Notification = new NotificationViewModel(
            $"Unsaved changes to {name} from {info.SavedAt:g} were recovered after an unexpected exit.",
            "Restore", () =>
            {
                Notification = null;
                LoadText(info.Text, info.OriginalPath ?? string.Empty, markDirty: true);
                recovery.Clear();
            },
            "Discard", () =>
            {
                Notification = null;
                recovery.Clear();
            });
    }

    /// <summary>Snapshot used by the recovery service.</summary>
    public (string? Path, string Text, bool Dirty)? GetRecoverySnapshot()
    {
        if (CurrentDocument is not { } d) return null;
        var text = XamlSource.HasParseError ? XamlSource.XamlText : Serializer.Serialize(d);
        return (string.IsNullOrEmpty(d.FilePath) ? null : d.FilePath, text, d.IsDirty);
    }

    // ------------------------------------------------------------------ IPropertyGridHost

    /// <summary>Executes a command on the undo stack (all document changes go through here).</summary>
    public void Execute(IDesignCommand command)
    {
        if (CurrentDocument is null) return;
        try
        {
            UndoRedo.Execute(command);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            StatusMessage = ex.Message;
            OnDocumentChanged();
        }
    }

    public void ActivateEventHandler(DesignElement element, string eventName)
    {
        if (element.GetEventHandler(eventName) is { } existing)
        {
            // Existing handler: show the attribute in the XAML view.
            XamlSource.IsSplitViewVisible = true;
            BottomTab = 0;
            if (!XamlSource.NavigateToAttribute(element, eventName)) CodeNavigateRequested?.Invoke(this, $"void {existing}(");
            return;
        }
        CreateEventHandler(element, eventName);
    }

    /// <summary>Wires <paramref name="eventName"/> to a suggested handler and shows it in the code-behind.</summary>
    public void CreateEventHandler(DesignElement element, string eventName)
    {
        var handler = element.GetEventHandler(eventName);
        if (handler is null)
        {
            handler = HandlerNaming.Suggest(element, eventName, CurrentDocument);
            Execute(new SetEventHandlerCommand(element, eventName, handler));
        }
        XamlSource.IsSplitViewVisible = true;
        BottomTab = 1;
        CodeNavigateRequested?.Invoke(this, $"void {handler}(");
    }

    // ------------------------------------------------------------------ canvas integration

    /// <summary>Called by the DesignCanvas for every user action.</summary>
    public void HandleDesignAction(DesignActionEventArgs args)
    {
        switch (args.Kind)
        {
            case DesignActionKind.ExecuteCommand when args.Command is not null:
                Execute(args.Command);
                if (args.SelectAfter is { Count: > 0 } select) SelectionManager.SetSelection(select);
                break;
            case DesignActionKind.CreateDefaultEventHandler when args.Element is { } e:
                if (e.Descriptor?.DefaultEvent is { } ev) CreateEventHandler(e, ev);
                else FocusPropertiesRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    public void OnCanvasPointerMoved(Point? p) => CursorText = p is { } v ? $"{v.X}, {v.Y}" : string.Empty;

    /// <summary>Creates a new element for a toolbox drop / double-click.</summary>
    public DesignElement CreateElement(ControlDescriptor descriptor)
    {
        var element = descriptor.DefaultElementFactory();
        if (descriptor.DefaultChildrenFactory is { } children)
            foreach (var c in children(Registry)) element.AddChild(c);

        if (Settings.Design.AutoNameNewControls && CurrentDocument is { } doc && descriptor.IsToolboxVisible
            && !descriptor.RenderAsPlaceholder && descriptor.ElementName != "Separator")
        {
            element.Name = doc.GenerateUniqueName(descriptor.ElementName);
        }
        if (IsMvvm && MvvmBindingHeuristics.GetDefaultBinding(element) is { } binding)
            element.SetValue(binding.Property, binding.Binding);
        return element;
    }

    // ------------------------------------------------------------------ document lifecycle

    private void SetDocument(DesignDocument? document, string? rawText = null, XamlParseResult? parsed = null, XamlParseError? error = null)
    {
        CurrentDocument = document;
        UndoRedo.Document = document;
        SelectionManager.Document = document;
        if (document is not null) document.WhenAnyValue(d => d.IsDirty).Subscribe(_ => this.RaisePropertyChanged(nameof(Title)));

        if (document is null)
        {
            XamlSource.SetTextFromDesigner(string.Empty, new(ReferenceEqualityComparer.Instance));
        }
        else if (rawText is not null)
        {
            // Keep the file's own formatting in the source view; designer edits reformat it.
            XamlSource.LoadText(rawText, parsed, error);
        }
        else
        {
            var text = Serializer.SerializeWithMap(document, out var spans);
            XamlSource.SetTextFromDesigner(text, spans);
        }

        DocumentOutline.Rebuild(document);
        PropertyGrid.Select([]);
        RegenerateCode();
        RaiseStatus();
        DesignInvalidated?.Invoke(this, EventArgs.Empty);
        if (document is not null && !string.IsNullOrEmpty(document.FilePath) && File.Exists(document.FilePath))
            _watcher.Watch(document.FilePath, SafeRead(document.FilePath));
        else
            _watcher.Stop();
    }

    private void OnDocumentChanged()
    {
        var doc = CurrentDocument;
        SelectionManager.Prune();
        DocumentOutline.Rebuild(doc);
        PropertyGrid.Select(SelectionManager.SelectedElements);
        if (doc is not null && !_applyingSourceEdit)
        {
            var text = Serializer.SerializeWithMap(doc, out var spans);
            XamlSource.SetTextFromDesigner(text, spans);
        }
        RegenerateCode();
        RaiseStatus();
        this.RaisePropertyChanged(nameof(IsMvvm));
        DesignInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionChanged()
    {
        PropertyGrid.Select(SelectionManager.SelectedElements);
        this.RaisePropertyChanged(nameof(SelectionText));
    }

    private void RaiseStatus()
    {
        this.RaisePropertyChanged(nameof(UndoHeader));
        this.RaisePropertyChanged(nameof(RedoHeader));
        this.RaisePropertyChanged(nameof(ElementCountText));
        this.RaisePropertyChanged(nameof(ErrorsText));
        this.RaisePropertyChanged(nameof(HasErrors));
        this.RaisePropertyChanged(nameof(Title));
        this.RaisePropertyChanged(nameof(SelectionText));
    }

    /// <summary>A successful parse of hand-edited XAML replaces the document tree (undoable).</summary>
    private void ApplySourceEdit(XamlParseResult result)
    {
        if (CurrentDocument is not { } doc) return;
        // Ignore parses that would not change anything (e.g. right after the designer wrote the text).
        if (Serializer.Serialize(result.Document) == Serializer.Serialize(doc)) return;

        var selectionPaths = SelectionManager.SelectedElements.Select(PathOf).ToList();
        _applyingSourceEdit = true;
        try
        {
            if (result.Document.CodeStyle == CodeBehindStyle.MVVM) doc.CodeStyle = CodeBehindStyle.MVVM;
            UndoRedo.Execute(new ReplaceRootCommand(result.Document));
        }
        finally
        {
            _applyingSourceEdit = false;
        }
        var restored = selectionPaths.Select(p => FromPath(doc.RootElement, p)).Where(e => e is not null).Cast<DesignElement>().ToList();
        SelectionManager.SetSelection(restored);
    }

    private static List<int> PathOf(DesignElement e)
    {
        var path = new List<int>();
        for (var x = e; x.Parent is not null; x = x.Parent) path.Insert(0, x.OwnerProperty is null ? x.IndexInParent : -1);
        return path;
    }

    private static DesignElement? FromPath(DesignElement root, List<int> path)
    {
        var e = root;
        foreach (var i in path)
        {
            if (i < 0 || i >= e.Children.Count) return null;
            e = e.Children[i];
        }
        return e;
    }

    /// <summary>Builds code generation options for the current document.</summary>
    public CodeGenOptions BuildCodeGenOptions(DesignDocument doc)
    {
        var project = ProjectContext;
        var ns = doc.ClassNamespace
                 ?? (project is { IsStandalone: false } && !string.IsNullOrEmpty(doc.FilePath) ? project.GetNamespaceForFile(doc.FilePath) : null)
                 ?? (string.IsNullOrWhiteSpace(Settings.CodeGen.DefaultNamespace) ? "MyApp.Views" : Settings.CodeGen.DefaultNamespace);
        string? vmNs = null;
        if (doc.GetNamespaceUri("vm") is { } uri && uri.StartsWith("clr-namespace:", StringComparison.Ordinal))
            vmNs = uri["clr-namespace:".Length..].Split(';')[0];
        return new CodeGenOptions
        {
            Namespace = ns,
            ClassName = doc.ClassName,
            BaseClass = CodeIdentifiers.BareTypeName(doc.RootElementType),
            GenerateNullableAnnotations = Settings.CodeGen.GenerateNullableAnnotations,
            GenerateFieldDeclarations = project is { IsStandalone: false, NameGeneratorEnabled: false } || Settings.CodeGen.GenerateFieldDeclarations,
            WireEventsInCode = Settings.CodeGen.WireEventsInCode,
            Style = doc.CodeStyle,
            ViewModelNamespace = vmNs,
        };
    }

    private void RegenerateCode()
    {
        var doc = CurrentDocument;
        Serializer.EmitEventAttributesForNamedElements = !Settings.CodeGen.WireEventsInCode;
        if (doc is null)
        {
            CodeBehindText = ViewModelText = string.Empty;
            return;
        }
        var options = BuildCodeGenOptions(doc);
        try
        {
            CodeBehindText = _codeBehindGenerator.Generate(doc, _existingCodeBehind, options);
        }
        catch (Exception ex)
        {
            CodeBehindText = $"// Code-behind generation failed: {ex.Message}";
        }
        try
        {
            ViewModelText = doc.CodeStyle == CodeBehindStyle.MVVM ? _viewModelGenerator.Generate(doc, options, _existingViewModel) : string.Empty;
        }
        catch (Exception ex)
        {
            ViewModelText = $"// ViewModel generation failed: {ex.Message}";
        }
    }

    private static string? SafeRead(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Loads XAML text as a document (also used for crash recovery).</summary>
    public void LoadText(string text, string filePath, bool markDirty = false)
    {
        var ok = Parser.TryParseDetailed(text, Registry, out var result, out var error);
        DesignDocument doc;
        if (ok && result is not null)
        {
            doc = result.Document;
        }
        else
        {
            // Unparseable file: keep the text editable; the designer shows a blank surface until it parses.
            doc = new DesignDocument(Registry.GetByElementName("Window")?.DefaultElementFactory() ?? new DesignElement("Window"));
            StatusMessage = $"XAML could not be parsed: {error}";
        }
        doc.FilePath = filePath;
        if (ProjectContext is { IsStandalone: false } p && string.IsNullOrEmpty(doc.XClass) && !string.IsNullOrEmpty(filePath))
            doc.XClass = p.GetDefaultXClass(filePath);
        doc.AssemblyName = ProjectContext?.AssemblyName;

        _existingCodeBehind = string.IsNullOrEmpty(filePath) ? null : SafeRead(filePath + ".cs");
        _existingViewModel = null;
        if (doc.CodeStyle == CodeBehindStyle.MVVM && !string.IsNullOrEmpty(filePath))
            _existingViewModel = SafeRead(ViewModelPathFor(doc));

        SetDocument(doc, text, ok ? result : null, error);
        doc.IsDirty = markDirty;
        this.RaisePropertyChanged(nameof(Title));
    }

    public async Task LoadFileAsync(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
            var text = await File.ReadAllTextAsync(path);
            if (ProjectContext is not { IsStandalone: false } && ProjectContext.FindProjectFile(path) is { } csproj)
            {
                try
                {
                    ProjectContext = await ProjectContext.LoadFromCsprojAsync(csproj);
                    RefreshProjectFiles();
                }
                catch (Exception)
                {
                    // Not fatal: continue without project context.
                }
            }
            LoadText(text, path);
            AddRecent(path);
            if (!XamlSource.HasParseError) StatusMessage = $"Opened {Path.GetFileName(path)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await ShowMessage("Open failed", ex.Message);
        }
    }

    private void AddRecent(string path)
    {
        Settings.AddRecentFile(path);
        RecentFiles.Clear();
        foreach (var r in Settings.Ui.RecentFiles) RecentFiles.Add(r);
    }

    /// <summary>Creates a new document; with <paramref name="prompt"/> the New wizard is shown.</summary>
    public async void NewDocument(NewDocumentKind kind, bool mvvm, bool prompt = true)
    {
        try
        {
            await NewDocumentAsync(kind, mvvm, prompt);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    public async Task NewDocumentAsync(NewDocumentKind kind, bool mvvm, bool prompt = true)
    {
        if (prompt && !await ConfirmDiscardAsync()) return;
        var defaultClass = kind == NewDocumentKind.Window ? "NewWindow" : "NewControl";
        var defaultDir = ProjectContext?.ProjectDirectory is { } pd
            ? (Directory.Exists(Path.Combine(pd, "Views")) ? Path.Combine(pd, "Views") : pd)
            : null;
        var defaultNs = defaultDir is not null && ProjectContext is { } pc ? pc.GetNamespaceForFile(Path.Combine(defaultDir, "x.axaml"))
            : string.IsNullOrWhiteSpace(Settings.CodeGen.DefaultNamespace) ? "MyApp.Views" : Settings.CodeGen.DefaultNamespace;

        var request = new NewDocumentRequest(defaultClass, defaultNs, kind, mvvm, defaultDir);
        if (prompt && Dialogs is not null)
        {
            var r = await Dialogs.ShowNewDocumentAsync(kind, mvvm, defaultNs, defaultDir);
            if (r is null) return;
            request = r;
        }

        var xClass = $"{request.Namespace}.{request.ClassName}";
        var vmNs = request.Mvvm ? CodeGenOptions.DefaultViewModelNamespace(request.Namespace) : null;
        var text = DocumentTemplates.Create(request.Kind, xClass, request.ClassName, vmNs);
        var doc = Parser.Parse(text, Registry);
        if (request.Mvvm) doc.CodeStyle = CodeBehindStyle.MVVM;
        _existingCodeBehind = null;
        _existingViewModel = null;
        var path = request.Directory is null ? string.Empty : Path.Combine(request.Directory, request.ClassName + ".axaml");
        doc.FilePath = path;
        SetDocument(doc);
        doc.IsDirty = true;

        if (path.Length > 0 && prompt)
        {
            if (File.Exists(path) && Dialogs is not null &&
                !await Dialogs.ConfirmAsync("File exists", $"{Path.GetFileName(path)} already exists. Overwrite it?"))
            {
                doc.FilePath = string.Empty;
                return;
            }
            await SaveToAsync(path);
            RefreshProjectFiles();
        }
        StatusMessage = $"Created {doc.ClassName}";
    }

    /// <summary>Asks to save a dirty document. Returns false when the user cancels.</summary>
    public async Task<bool> ConfirmDiscardAsync()
    {
        if (CurrentDocument is not { IsDirty: true } doc || Dialogs is null) return true;
        switch (await Dialogs.AskSaveChangesAsync(doc.FileName))
        {
            case SaveChangesResult.Save:
                return await SaveAsync();
            case SaveChangesResult.Discard:
                return true;
            default:
                return false;
        }
    }

    public async Task<bool> SaveAsync()
    {
        if (CurrentDocument is not { } doc) return false;
        if (string.IsNullOrEmpty(doc.FilePath)) return await SaveAsAsync();
        return await SaveToAsync(doc.FilePath);
    }

    public async Task<bool> SaveAsAsync()
    {
        if (CurrentDocument is not { } doc || Dialogs is null) return false;
        var path = await Dialogs.SaveFileAsync("Save As", doc.FileName, [FileFilter.Axaml]);
        if (path is null) return false;
        if (!path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)) path += ".axaml";

        // Keep x:Class in step with a renamed file.
        var oldStem = string.IsNullOrEmpty(doc.FilePath) ? null : Path.GetFileNameWithoutExtension(doc.FilePath);
        var newStem = Path.GetFileNameWithoutExtension(path);
        if (doc.XClass is { } xc && (oldStem is null || doc.ClassName == oldStem) && doc.ClassName != newStem)
        {
            var ns = doc.ClassNamespace;
            doc.XClass = ns is null ? CodeIdentifiers.MakeIdentifier(newStem) : $"{ns}.{CodeIdentifiers.MakeIdentifier(newStem)}";
            _existingCodeBehind = SafeRead(path + ".cs");
            OnDocumentChanged();
        }
        doc.FilePath = path;
        return await SaveToAsync(path);
    }

    /// <summary>Writes .axaml, merges .axaml.cs and (MVVM) the ViewModel.</summary>
    public async Task<bool> SaveToAsync(string path)
    {
        if (CurrentDocument is not { } doc) return false;
        try
        {
            XamlSource.FlushPendingEdits();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            string xaml;
            var writeCode = true;
            if (XamlSource.HasParseError)
            {
                if (Dialogs is not null && !await Dialogs.ConfirmAsync("XAML has errors",
                        "The XAML source cannot be parsed. Save the text as it is (code-behind will not be updated)?"))
                    return false;
                xaml = XamlSource.XamlText;
                writeCode = false;
            }
            else
            {
                xaml = Serializer.Serialize(doc);
            }

            _watcher.SetKnownText(xaml);
            await File.WriteAllTextAsync(path, xaml);

            if (writeCode)
            {
                var options = BuildCodeGenOptions(doc);
                var csPath = path + ".cs";
                var existing = SafeRead(csPath);
                var code = _codeBehindGenerator.Generate(doc, existing, options);
                await File.WriteAllTextAsync(csPath, code);
                _existingCodeBehind = code;

                if (doc.CodeStyle == CodeBehindStyle.MVVM)
                {
                    var vmPath = ViewModelPathFor(doc, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(vmPath)!);
                    var vm = _viewModelGenerator.Generate(doc, options, SafeRead(vmPath));
                    await File.WriteAllTextAsync(vmPath, vm);
                    _existingViewModel = vm;
                }
            }

            doc.FilePath = Path.GetFullPath(path);
            doc.IsDirty = false;
            AddRecent(doc.FilePath);
            _watcher.Watch(doc.FilePath, xaml);
            RegenerateCode();
            StatusMessage = $"Saved {Path.GetFileName(path)}";
            this.RaisePropertyChanged(nameof(Title));
            this.RaisePropertyChanged(nameof(BaseDirectory));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await ShowMessage("Save failed", ex.Message);
            return false;
        }
    }

    private string ViewModelPathFor(DesignDocument doc, string? axamlPath = null)
    {
        var className = $"{doc.ClassName}ViewModel";
        var ctx = ProjectContext ?? ProjectContext.Standalone();
        return ctx.GetViewModelPath(axamlPath ?? doc.FilePath, className);
    }

    public async Task CloseAsync()
    {
        if (!await ConfirmDiscardAsync()) return;
        _existingCodeBehind = _existingViewModel = null;
        SetDocument(null);
    }

    public async Task OpenProjectAsync(string csproj)
    {
        try
        {
            ProjectContext = await ProjectContext.LoadFromCsprojAsync(csproj);
            RefreshProjectFiles();
            LeftTab = 1;
            Settings.Ui.RecentProjects.Remove(csproj);
            Settings.Ui.RecentProjects.Insert(0, csproj);
            StatusMessage = $"Opened project {Path.GetFileName(csproj)} ({ProjectFiles.Count} .axaml files)";
            if (CurrentDocument is { } doc) RegenerateCode();
        }
        catch (Exception ex)
        {
            await ShowMessage("Open project failed", ex.Message);
        }
    }

    private void RefreshProjectFiles()
    {
        ProjectFiles.Clear();
        if (ProjectContext?.ProjectDirectory is not { } dir) return;
        ProjectContext.ScanFiles();
        foreach (var f in ProjectContext.AxamlFiles) ProjectFiles.Add(new ProjectFileItem(f, Path.GetRelativePath(dir, f)));
    }

    private void OnExternallyModified(string path)
    {
        if (CurrentDocument?.FilePath is not { } current || !string.Equals(current, path, StringComparison.Ordinal)) return;
        Notification = new NotificationViewModel($"{Path.GetFileName(path)} has been modified externally.",
            "Reload", () =>
            {
                Notification = null;
                _ = LoadFileAsync(path);
            },
            "Ignore", () => Notification = null);
    }

    public async Task<bool> ConfirmExitAsync()
    {
        if (!await ConfirmDiscardAsync()) return false;
        SaveSettings();
        return true;
    }

    public void SaveSettings()
    {
        var layout = Settings.Ui.PanelLayout;
        layout.ShowToolbox = ShowToolbox;
        layout.ShowProperties = ShowProperties;
        layout.ShowOutline = ShowOutline;
        layout.ShowSource = XamlSource.IsSplitViewVisible;
        Settings.Ui.ToolboxExpanded = Toolbox.Categories.Where(c => c.IsExpanded).Select(c => c.Name).ToList();
        try
        {
            Settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Could not save settings: {ex.Message}";
        }
    }

    public void ApplySettings(DesignerSettings settings)
    {
        Settings = settings;
        this.RaisePropertyChanged(nameof(SnapToGrid));
        this.RaisePropertyChanged(nameof(ShowGrid));
        this.RaisePropertyChanged(nameof(SmartSnap));
        this.RaisePropertyChanged(nameof(GridSize));
        this.RaisePropertyChanged(nameof(SnapThreshold));
        RegenerateCode();
        if (CurrentDocument is { } doc && !XamlSource.HasParseError)
            XamlSource.SetTextFromDesigner(Serializer.SerializeWithMap(doc, out var spans), spans);
        SaveSettings();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task ShowMessage(string title, string message)
    {
        if (Dialogs is not null) await Dialogs.ShowMessageAsync(title, message);
        else StatusMessage = $"{title}: {message}";
    }

    private void ReparentFromOutline(DesignElementNodeViewModel source, DesignElementNodeViewModel target, int index)
    {
        if (!DocumentOutlineViewModel.CanReparent(source, target)) return;
        var values = PlacementService.CleanupForNewParent(source.Element, target.Element);
        Execute(new ReparentCommand(source.Element, target.Element, index, values));
        SelectionManager.SetSelection(source.Element);
    }

    public void Dispose()
    {
        _watcher.Dispose();
        XamlSource.Dispose();
    }
}
