using System.Collections.ObjectModel;
using Avalonia.Media;
using ReactiveUI;

namespace AVAUIDesigner.Engine.Descriptors;

/// <summary>A toolbox category (an expander in the toolbox panel).</summary>
public class ToolboxCategory : ReactiveObject
{
    private bool _isExpanded = true;

    public ToolboxCategory(string name, IEnumerable<ToolboxItem>? items = null)
    {
        Name = name;
        Items = new ObservableCollection<ToolboxItem>(items ?? []);
    }

    public string Name { get; set; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public ObservableCollection<ToolboxItem> Items { get; }
}

/// <summary>A draggable toolbox entry.</summary>
public class ToolboxItem
{
    public ToolboxItem(ControlDescriptor descriptor, string? displayName = null)
    {
        Descriptor = descriptor;
        DisplayName = displayName ?? descriptor.ElementName;
    }

    public ControlDescriptor Descriptor { get; set; }
    public string DisplayName { get; set; }
    public IImage? Icon => Descriptor.Icon;
    public string ToolTip => string.IsNullOrEmpty(Descriptor.Description)
        ? Descriptor.FullTypeName
        : $"{Descriptor.FullTypeName}\n{Descriptor.Description}";

    public override string ToString() => DisplayName;
}

/// <summary>Standard toolbox category names, in display order.</summary>
public static class ToolboxCategories
{
    public const string Common = "Common Controls";
    public const string Containers = "Containers";
    public const string Data = "Data";
    public const string Menus = "Menus & Toolbars";
    public const string Dialogs = "Dialogs & Overlays";
    public const string Input = "Input";
    public const string Layout = "Layout";
    public const string Media = "Media & Display";
    public const string Root = "Root";

    public static readonly IReadOnlyList<string> Order =
        [Common, Containers, Data, Menus, Dialogs, Input, Layout, Media];
}
