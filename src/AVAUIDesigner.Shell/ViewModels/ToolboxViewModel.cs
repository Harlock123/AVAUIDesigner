using System.Collections.ObjectModel;
using System.Reactive;
using AVAUIDesigner.Engine.Descriptors;
using ReactiveUI;

namespace AVAUIDesigner.Shell.ViewModels;

/// <summary>Categorized, searchable list of controls that can be dragged onto the canvas.</summary>
public class ToolboxViewModel : ReactiveObject
{
    private readonly ControlDescriptorRegistry _registry;
    private string _searchText = string.Empty;
    private ToolboxItem? _selectedItem;

    public ToolboxViewModel(ControlDescriptorRegistry registry, Action<ToolboxItem>? addControl = null)
    {
        _registry = registry;
        AddControlCommand = ReactiveCommand.Create<ToolboxItem>(item => addControl?.Invoke(item));
        Rebuild();
        registry.Changed += (_, _) => Rebuild();
    }

    public ObservableCollection<ToolboxCategory> Categories { get; } = [];

    /// <summary>Extra categories supplied by plugins.</summary>
    public List<ToolboxCategory> PluginCategories { get; } = [];

    /// <summary>Filters items by name; while non-empty <see cref="FilteredItems"/> is shown instead of the categories.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            ApplyFilter();
        }
    }

    public ObservableCollection<ToolboxItem> FilteredItems { get; } = [];

    public bool IsFiltering => !string.IsNullOrWhiteSpace(SearchText);

    public ToolboxItem? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }

    /// <summary>Double-click adds the control at a default position on the canvas.</summary>
    public ReactiveCommand<ToolboxItem, Unit> AddControlCommand { get; }

    /// <summary>Category names that start expanded (from settings).</summary>
    public IReadOnlyCollection<string>? ExpandedCategories { get; set; }

    public void Rebuild()
    {
        var expanded = Categories.Where(c => c.IsExpanded).Select(c => c.Name).ToHashSet();
        var hadCategories = Categories.Count > 0;
        Categories.Clear();

        var visible = _registry.GetAll().Where(d => d.IsToolboxVisible && !d.IsRootOnly).ToList();
        var names = ToolboxCategories.Order.Concat(visible.Select(d => d.Category)).Distinct();
        foreach (var name in names)
        {
            var items = visible.Where(d => d.Category == name).Select(d => new ToolboxItem(d)).ToList();
            if (items.Count == 0) continue;
            var isExpanded = hadCategories ? expanded.Contains(name) : ExpandedCategories?.Contains(name) ?? true;
            Categories.Add(new ToolboxCategory(name, items) { IsExpanded = isExpanded });
        }
        foreach (var pc in PluginCategories)
        {
            if (Categories.Any(c => c.Name == pc.Name)) continue;
            Categories.Add(pc);
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredItems.Clear();
        if (IsFiltering)
        {
            var text = SearchText.Trim();
            foreach (var item in Categories.SelectMany(c => c.Items))
                if (item.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase) && FilteredItems.All(f => f.Descriptor != item.Descriptor))
                    FilteredItems.Add(item);
        }
        this.RaisePropertyChanged(nameof(IsFiltering));
    }
}
