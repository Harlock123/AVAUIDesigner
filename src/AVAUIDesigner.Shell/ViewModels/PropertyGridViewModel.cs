using System.Collections.ObjectModel;
using System.Reactive;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Properties;
using AVAUIDesigner.Engine.Serialization;
using AVAUIDesigner.Engine.UndoRedo;
using AVAUIDesigner.Engine.UndoRedo.Commands;
using AVAUIDesigner.Shell.Services;
using ReactiveUI;

namespace AVAUIDesigner.Shell.ViewModels;

/// <summary>What the property grid needs from its owner.</summary>
public interface IPropertyGridHost
{
    DesignDocument? Document { get; }
    ControlDescriptorRegistry Registry { get; }
    XamlParser Parser { get; }
    XamlSerializer Serializer { get; }
    IDialogService? Dialogs { get; }
    void Execute(IDesignCommand command);

    /// <summary>Double-click on an event row: generate the default handler name if empty and show the handler.</summary>
    void ActivateEventHandler(DesignElement element, string eventName);

    /// <summary>Maps property types to plugin editor control types.</summary>
    IReadOnlyDictionary<Type, Type> CustomEditors { get; }
}

/// <summary>The Properties/Events panel for the current selection.</summary>
public class PropertyGridViewModel : ReactiveObject
{
    private readonly IPropertyGridHost _host;
    private IReadOnlyList<DesignElement> _selected = [];
    private string _searchText = string.Empty;
    private PropertySortMode _sortMode = PropertySortMode.Categorized;
    private PropertyRowViewModel? _selectedRow;
    private string? _elementName;
    private string? _nameError;
    private GridDefinitionEditorViewModel? _gridEditor;
    private int _selectedTab;

    public PropertyGridViewModel(IPropertyGridHost host)
    {
        _host = host;
        ResetPropertyCommand = ReactiveCommand.Create<PropertyRowViewModel>(r => r.Value = null);
        SortAlphabeticallyCommand = ReactiveCommand.Create(() => { SortMode = PropertySortMode.Alphabetical; });
        SortByCategoryCommand = ReactiveCommand.Create(() => { SortMode = PropertySortMode.Categorized; });
    }

    public IReadOnlyList<DesignElement> SelectedElements => _selected;

    /// <summary>The single selected element (null for none or multi-selection).</summary>
    public DesignElement? SelectedElement => _selected.Count == 1 ? _selected[0] : null;

    public bool HasSelection => _selected.Count > 0;
    public bool IsMultiSelection => _selected.Count > 1;

    /// <summary>Header text: "Button" / "3 elements".</summary>
    public string SelectionTypeLabel => _selected.Count switch
    {
        0 => "No selection",
        1 => _selected[0].ElementType + (_selected[0].IsUnknown ? " (unknown type)" : string.Empty),
        _ => $"{_selected.Count} elements",
    };

    /// <summary>Editable x:Name of the single selected element.</summary>
    public string? ElementName
    {
        get => _elementName;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (SelectedElement is not { } e || v == e.Name) return;
            if (ValidateName(e, v) is { } error)
            {
                NameError = error;
                this.RaisePropertyChanged();
                return;
            }
            NameError = null;
            _elementName = v;
            _host.Execute(new SetNameCommand(e, v));
        }
    }

    public string? NameError
    {
        get => _nameError;
        private set => this.RaiseAndSetIfChanged(ref _nameError, value);
    }

    public ObservableCollection<PropertyRowViewModel> AllProperties { get; } = [];

    /// <summary>Filtered/sorted rows, interleaved with category headers in categorized mode.</summary>
    public ObservableCollection<object> VisibleProperties { get; } = [];

    public ObservableCollection<EventRowViewModel> Events { get; } = [];

    public bool HasEvents => Events.Count > 0;

    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            ApplyView();
        }
    }

    public PropertySortMode SortMode
    {
        get => _sortMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _sortMode, value);
            this.RaisePropertyChanged(nameof(IsAlphabetical));
            this.RaisePropertyChanged(nameof(IsCategorized));
            ApplyView();
        }
    }

    public bool IsAlphabetical
    {
        get => SortMode == PropertySortMode.Alphabetical;
        set { if (value) SortMode = PropertySortMode.Alphabetical; }
    }

    public bool IsCategorized
    {
        get => SortMode == PropertySortMode.Categorized;
        set { if (value) SortMode = PropertySortMode.Categorized; }
    }

    /// <summary>0 = Properties, 1 = Events.</summary>
    public int SelectedTab
    {
        get => _selectedTab;
        set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
    }

    public PropertyRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedRow, value);
            this.RaisePropertyChanged(nameof(DescriptionText));
        }
    }

    /// <summary>Status bar text for the selected property.</summary>
    public string DescriptionText => SelectedRow is { } r
        ? $"{r.Name} ({TypeName(r.PropertyType)}): {r.Description}"
        : SelectedElement?.Descriptor?.Description ?? string.Empty;

    /// <summary>Row/column editor, shown when a single Grid is selected.</summary>
    public GridDefinitionEditorViewModel? GridEditor
    {
        get => _gridEditor;
        private set => this.RaiseAndSetIfChanged(ref _gridEditor, value);
    }

    public ReactiveCommand<PropertyRowViewModel, Unit> ResetPropertyCommand { get; }
    public ReactiveCommand<Unit, Unit> SortAlphabeticallyCommand { get; }
    public ReactiveCommand<Unit, Unit> SortByCategoryCommand { get; }

    // ------------------------------------------------------------------ selection

    /// <summary>Shows the properties of <paramref name="elements"/> (rebuilding the rows).</summary>
    public void Select(IReadOnlyList<DesignElement> elements)
    {
        var same = elements.Count == _selected.Count && elements.Zip(_selected).All(p => ReferenceEquals(p.First, p.Second));
        _selected = elements.ToList();
        if (same)
        {
            Refresh();
            return;
        }
        Rebuild();
    }

    private void Rebuild()
    {
        AllProperties.Clear();
        Events.Clear();
        SelectedRow = null;
        GridEditor = null;

        if (_selected.Count > 0)
        {
            var sets = _selected.Select(PropertiesFor).ToList();
            var common = sets[0].Where(p => sets.Skip(1).All(s => s.Any(o => o.FullName == p.FullName)));
            foreach (var pd in common)
            {
                var row = new PropertyRowViewModel(pd, pd.FullName, OnRowCommitted, CustomEditorFor(pd));
                if (row.EditorKind is PropertyEditorKind.Collection or PropertyEditorKind.XamlFragment or PropertyEditorKind.Complex)
                    row.EditCommand = ReactiveCommand.CreateFromTask(() => EditComplexAsync(row));
                if (row.EditorKind is not (PropertyEditorKind.Collection or PropertyEditorKind.XamlFragment))
                    row.EditTextCommand = ReactiveCommand.CreateFromTask(() => EditAsTextAsync(row));
                AllProperties.Add(row);
            }

            if (SelectedElement is { } single)
            {
                foreach (var ed in single.Descriptor?.Events ?? [])
                {
                    var row = new EventRowViewModel(ed, OnEventCommitted);
                    row.ActivateCommand = ReactiveCommand.Create(() =>
                    {
                        if (SelectedElement is { } el) _host.ActivateEventHandler(el, ed.Name);
                    });
                    Events.Add(row);
                }
                if (single.Descriptor?.LayoutMode == ContainerLayoutMode.Grid)
                    GridEditor = new GridDefinitionEditorViewModel(single, _host.Execute);
            }
        }

        RaiseSelectionProperties();
        Refresh();
        ApplyView();
    }

    /// <summary>Re-reads all values from the model (after any document change) without rebuilding rows.</summary>
    public void Refresh()
    {
        _elementName = SelectedElement?.Name;
        NameError = null;
        this.RaisePropertyChanged(nameof(ElementName));

        foreach (var row in AllProperties)
        {
            var values = _selected.Select(e => ReadValue(e, row)).ToList();
            var first = values.FirstOrDefault();
            var mixed = values.Any(v => v != first);
            row.UpdateFromModel(mixed ? null : first, mixed);
        }
        if (SelectedElement is { } e)
            foreach (var ev in Events) ev.UpdateFromModel(e.GetEventHandler(ev.Name));
        GridEditor?.Reload();
        this.RaisePropertyChanged(nameof(SelectionTypeLabel));
    }

    private void RaiseSelectionProperties()
    {
        this.RaisePropertyChanged(nameof(SelectedElements));
        this.RaisePropertyChanged(nameof(SelectedElement));
        this.RaisePropertyChanged(nameof(HasSelection));
        this.RaisePropertyChanged(nameof(IsMultiSelection));
        this.RaisePropertyChanged(nameof(SelectionTypeLabel));
        this.RaisePropertyChanged(nameof(HasEvents));
        this.RaisePropertyChanged(nameof(DescriptionText));
    }

    /// <summary>Every property shown for <paramref name="e"/>: descriptor properties, container-attached ones and any other set attributes.</summary>
    private List<PropertyDescriptor> PropertiesFor(DesignElement e)
    {
        var list = new List<PropertyDescriptor>();
        var isGrid = e.Descriptor?.LayoutMode == ContainerLayoutMode.Grid;
        foreach (var p in e.Descriptor?.Properties ?? [])
        {
            if (!p.IsBrowsable) continue;
            if (isGrid && p.Name is "RowDefinitions" or "ColumnDefinitions") continue; // the Grid editor handles these
            list.Add(p);
        }
        list.AddRange(_host.Registry.GetAttachedPropertiesFor(e));

        // Attributes the descriptor does not know about still need to be editable.
        foreach (var p in e.Properties.Where(p => p.IsAttributeForm && !p.Name.StartsWith("x:", StringComparison.Ordinal)))
            if (list.All(d => d.FullName != p.Name))
                list.Add(new PropertyDescriptor(p.Name, typeof(string), PropertyCategories.Misc, null, "Attribute not described by the control catalog."));
        foreach (var a in e.AttachedProperties)
            if (list.All(d => d.FullName != a.FullName))
                list.Add(new PropertyDescriptor(a.Name, _host.Registry.FindAttachedProperty(a.FullName)?.PropertyType ?? typeof(string),
                    PropertyCategories.Attached) { AttachedPropertyOwner = a.OwnerType });
        return list;
    }

    private Type? CustomEditorFor(PropertyDescriptor pd) =>
        pd.CustomEditorType ?? _host.CustomEditors.GetValueOrDefault(Nullable.GetUnderlyingType(pd.PropertyType) ?? pd.PropertyType);

    private static string? ReadValue(DesignElement e, PropertyRowViewModel row)
    {
        var d = row.Descriptor;
        if (d.CollectionItemElement is { } itemType)
        {
            var items = d.CollectionIsPropertyElement
                ? e.GetPropertyElement(d.Name)?.ElementValues ?? []
                : e.Children.Where(c => c.ElementType == itemType).ToList();
            return items.Count == 0 ? null : $"({items.Count} item{(items.Count == 1 ? "" : "s")})";
        }
        if (d.IsXamlFragment || row.EditorKind == PropertyEditorKind.Complex)
        {
            if (e.GetValue(row.Name) is { } attr) return attr;
            return e.GetPropertyElement(row.Name) is { } pe ? $"<{string.Join(", ", pe.ElementValues.Select(v => v.ElementType))}>" : null;
        }
        return e.GetValue(row.Name);
    }

    // ------------------------------------------------------------------ editing

    /// <summary>Called when the user commits a change in any editor.</summary>
    public void SetProperty(string propertyName, object? newValue)
    {
        var row = AllProperties.FirstOrDefault(r => r.Name == propertyName);
        var text = newValue switch
        {
            null => null,
            string s => s,
            _ => XamlValueConverter.Serialize(newValue, row?.PropertyType ?? newValue.GetType()),
        };
        if (row is not null)
        {
            row.Value = text;
            return;
        }
        ApplyValue(propertyName, text);
    }

    private void OnRowCommitted(PropertyRowViewModel row, string? value)
    {
        if (row.EditorKind is PropertyEditorKind.Collection) return; // edited through the dialog
        ApplyValue(row.Name, value);
    }

    private void ApplyValue(string name, string? value)
    {
        var commands = _selected.Where(e => e.GetValue(name) != value)
            .Select(e => (IDesignCommand)new SetPropertyCommand(e, name, value)).ToList();
        if (commands.Count == 0) return;
        _host.Execute(commands.Count == 1 ? commands[0] : new CompositeCommand($"Set {name} on {commands.Count} elements", commands));
    }

    private void OnEventCommitted(EventRowViewModel row, string? handler)
    {
        if (SelectedElement is not { } e || e.GetEventHandler(row.Name) == handler) return;
        _host.Execute(new SetEventHandlerCommand(e, row.Name, handler));
    }

    private static string? ValidateName(DesignElement e, string? name)
    {
        if (name is null) return null;
        if (!CodeIdentifiers.IsValidIdentifier(name)) return $"'{name}' is not a valid C# identifier.";
        var root = e.Root;
        if (root.SelfAndAllDescendants().Any(x => !ReferenceEquals(x, e) && x.Name == name)) return $"The name '{name}' is already used.";
        return null;
    }

    /// <summary>Validates an x:Name for the rename dialog.</summary>
    public static string? ValidateNewName(DesignElement e, string? name) => ValidateName(e, string.IsNullOrWhiteSpace(name) ? null : name.Trim());

    private async Task EditComplexAsync(PropertyRowViewModel row)
    {
        if (SelectedElement is not { } e || _host.Dialogs is not { } dialogs || _host.Document is not { } doc) return;
        var d = row.Descriptor;

        if (d.CollectionItemElement is { } itemType)
        {
            var props = d.CollectionItemProperties ?? ["Content"];
            var items = d.CollectionIsPropertyElement
                ? e.GetPropertyElement(d.Name)?.ElementValues.ToList() ?? []
                : e.Children.Where(c => c.ElementType == itemType).ToList();
            var text = string.Join("\n", items.Select(i => string.Join(" | ", props.Select(p => Display(i.GetValue(p))))));
            var help = props.Count > 1
                ? $"One {itemType} per line. Separate {string.Join(", ", props)} with \" | \"."
                : $"One {itemType} per line ({props[0]}).";
            var edited = await dialogs.EditTextAsync($"{e.DisplayName}.{d.Name}", help, text, isXaml: false);
            if (edited is null) return;
            ApplyCollection(e, d, itemType, props, items, edited);
            return;
        }

        if (d.IsXamlFragment)
        {
            var current = e.GetPropertyElement(d.Name) is { } pe ? _host.Serializer.SerializePropertyElementContent(pe) : string.Empty;
            var edited = await dialogs.EditTextAsync($"{e.DisplayName}.{d.Name}", "XAML content of the property (e.g. a <DataTemplate>).", current, isXaml: true);
            if (edited is null) return;
            DesignProperty? newProp = null;
            if (!string.IsNullOrWhiteSpace(edited))
            {
                try
                {
                    var elements = _host.Parser.ParseFragment(edited, doc, _host.Registry);
                    newProp = new DesignProperty(d.Name, null) { IsAttributeForm = false };
                    newProp.ElementValues.AddRange(elements);
                }
                catch (XamlParseException ex)
                {
                    await dialogs.ShowMessageAsync("Invalid XAML", ex.Error.ToString());
                    return;
                }
            }
            _host.Execute(new ReplacePropertyCommand(e, d.Name, newProp));
            return;
        }

        await EditAsTextAsync(row);
    }

    private async Task EditAsTextAsync(PropertyRowViewModel row)
    {
        if (_host.Dialogs is not { } dialogs || _selected.Count == 0) return;
        var value = await dialogs.PromptAsync($"Edit {row.Name}", "Attribute value (markup extensions such as {Binding Name} are allowed):",
            row.Value ?? string.Empty);
        if (value is not null) row.Value = value.Trim();
    }

    private static string Display(string? value) =>
        MvvmBindingHeuristics.GetBindingPath(value) is { } path && value!.Count(c => c == ',') == 0 ? path : value ?? string.Empty;

    private void ApplyCollection(DesignElement e, PropertyDescriptor d, string itemType, IReadOnlyList<string> props,
        List<DesignElement> oldItems, string text)
    {
        var descriptor = _host.Registry.GetByElementName(itemType);
        var newItems = new List<DesignElement>();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split('|').Select(p => p.Trim()).ToArray();
            var item = new DesignElement(itemType, descriptor);
            for (var i = 0; i < props.Count && i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                var v = parts[i];
                if (props[i] == "Binding" && !XamlValueConverter.IsMarkupExtension(v)) v = $"{{Binding {v}}}";
                item.SetValue(props[i], v);
            }
            newItems.Add(item);
        }

        if (d.CollectionIsPropertyElement)
        {
            DesignProperty? prop = null;
            if (newItems.Count > 0)
            {
                prop = new DesignProperty(d.Name, null) { IsAttributeForm = false };
                prop.ElementValues.AddRange(newItems);
            }
            _host.Execute(new ReplacePropertyCommand(e, d.Name, prop, $"Edit {e.DisplayName}.{d.Name}"));
            return;
        }

        var commands = new List<IDesignCommand>();
        commands.AddRange(oldItems.Select(i => new RemoveElementCommand(i)));
        commands.AddRange(newItems.Select(i => new AddElementCommand(e, i)));
        if (commands.Count > 0) _host.Execute(new CompositeCommand($"Edit {e.DisplayName}.{d.Name}", commands));
    }

    // ------------------------------------------------------------------ view

    private void ApplyView()
    {
        VisibleProperties.Clear();
        var rows = AllProperties.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText.Trim();
            rows = rows.Where(r => r.Name.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        if (SortMode == PropertySortMode.Alphabetical)
        {
            foreach (var r in rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)) VisibleProperties.Add(r);
            return;
        }
        foreach (var group in rows.GroupBy(r => r.Category).OrderBy(g => PropertyCategories.IndexOf(g.Key)).ThenBy(g => g.Key))
        {
            VisibleProperties.Add(new PropertyCategoryHeaderViewModel(group.Key));
            foreach (var r in group) VisibleProperties.Add(r);
        }
    }

    private static string TypeName(Type t)
    {
        var u = Nullable.GetUnderlyingType(t);
        return u is null ? t.Name : u.Name + "?";
    }
}
