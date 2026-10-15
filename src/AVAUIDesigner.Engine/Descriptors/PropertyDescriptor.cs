namespace AVAUIDesigner.Engine.Descriptors;

/// <summary>
/// Describes one designable property shown in the property grid.
/// </summary>
public class PropertyDescriptor
{
    public PropertyDescriptor(string name, Type propertyType, string category = PropertyCategories.Misc,
        object? defaultValue = null, string? description = null)
    {
        Name = name;
        PropertyType = propertyType;
        Category = category;
        DefaultValue = defaultValue;
        Description = description ?? string.Empty;
    }

    /// <summary>Property name, e.g. "Content". For attached properties this is the bare name ("Row").</summary>
    public string Name { get; }

    public string Category { get; init; }
    public string Description { get; init; }
    public Type PropertyType { get; }
    public object? DefaultValue { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsBrowsable { get; init; } = true;

    /// <summary>Design-time only (d: prefix), never part of runtime XAML.</summary>
    public bool IsDesignTimeOnly { get; init; }

    /// <summary>Overrides the default editor chosen from <see cref="PropertyType"/>.</summary>
    public Type? CustomEditorType { get; init; }

    /// <summary>Owner type of an attached property, e.g. "Grid" for Grid.Row.</summary>
    public string? AttachedPropertyOwner { get; init; }

    /// <summary>Allowed string values for free-form properties (shown as an editable dropdown).</summary>
    public IReadOnlyList<string>? StandardValues { get; init; }

    /// <summary>
    /// For collection properties edited through the collection editor: the element type of each item
    /// (e.g. "ComboBoxItem"), and the item properties edited per line (e.g. ["Content"]).
    /// </summary>
    public string? CollectionItemElement { get; init; }
    public IReadOnlyList<string>? CollectionItemProperties { get; init; }

    /// <summary>When true the collection lives in a property element (e.g. DataGrid.Columns); otherwise items are children.</summary>
    public bool CollectionIsPropertyElement { get; init; }

    /// <summary>Property whose value is a XAML fragment (e.g. ItemTemplate) edited in a snippet dialog.</summary>
    public bool IsXamlFragment { get; init; }

    /// <summary>Full XAML name: "Grid.Row" for attached, "Width" otherwise.</summary>
    public string FullName => AttachedPropertyOwner is null ? Name : $"{AttachedPropertyOwner}.{Name}";

    public bool IsAttached => AttachedPropertyOwner is not null;

    public override string ToString() => FullName;
}

public static class PropertyCategories
{
    public const string Common = "Common";
    public const string Layout = "Layout";
    public const string Appearance = "Appearance";
    public const string Brush = "Brush";
    public const string Text = "Text";
    public const string Behavior = "Behavior";
    public const string Data = "Data";
    public const string Window = "Window";
    public const string Attached = "Attached";
    public const string Misc = "Miscellaneous";

    /// <summary>Display order of categories in categorized mode.</summary>
    public static readonly IReadOnlyList<string> Order =
        [Common, Layout, Appearance, Brush, Text, Behavior, Data, Window, Attached, Misc];

    public static int IndexOf(string category)
    {
        for (var i = 0; i < Order.Count; i++)
            if (Order[i] == category) return i;
        return Order.Count;
    }
}
