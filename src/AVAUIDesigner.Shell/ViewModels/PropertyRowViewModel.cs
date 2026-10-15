using System.Reactive;
using Avalonia;
using Avalonia.Media;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Properties;
using ReactiveUI;

namespace AVAUIDesigner.Shell.ViewModels;

/// <summary>Which inline editor a property row uses.</summary>
public enum PropertyEditorKind
{
    String,
    Bool,
    Enum,
    Color,
    Thickness,
    CornerRadius,
    Numeric,
    FontFamily,
    GridLength,
    Collection,
    XamlFragment,
    Complex,
    Custom,
}

public enum PropertySortMode
{
    Alphabetical,
    Categorized,
}

/// <summary>Category header shown between rows in categorized mode.</summary>
public class PropertyCategoryHeaderViewModel(string name)
{
    public string Name { get; } = name;
}

/// <summary>One row of the property grid. <see cref="Value"/> is the XAML string (null = not set).</summary>
public class PropertyRowViewModel : ReactiveObject
{
    private readonly Action<PropertyRowViewModel, string?> _commit;
    private string? _value;
    private bool _isMixed;
    private bool _hasError;
    private string? _errorMessage;

    public PropertyRowViewModel(PropertyDescriptor descriptor, string fullName, Action<PropertyRowViewModel, string?> commit,
        Type? customEditorType = null)
    {
        Descriptor = descriptor;
        Name = fullName;
        _commit = commit;
        CustomEditorType = customEditorType ?? descriptor.CustomEditorType;
        EditorKind = CustomEditorType is not null ? PropertyEditorKind.Custom : ChooseEditor(descriptor);
        DefaultText = descriptor.DefaultValue is null ? null : XamlValueConverter.Serialize(descriptor.DefaultValue, descriptor.PropertyType);
        StandardValues = descriptor.StandardValues
                         ?? (UnderlyingType.IsEnum ? Enum.GetNames(UnderlyingType) : null);
        ResetCommand = ReactiveCommand.Create(() => { Value = null; });
    }

    public PropertyDescriptor Descriptor { get; }

    /// <summary>Full XAML name ("Width", "Grid.Row").</summary>
    public string Name { get; }

    public string DisplayName => Name;
    public string Category => Descriptor.Category;
    public string Description => string.IsNullOrEmpty(Descriptor.Description) ? Name : Descriptor.Description;
    public Type PropertyType => Descriptor.PropertyType;
    public Type UnderlyingType => Nullable.GetUnderlyingType(PropertyType) ?? PropertyType;
    public object? DefaultValue => Descriptor.DefaultValue;

    /// <summary>The default value in XAML form, shown as a watermark when the property is not set.</summary>
    public string? DefaultText { get; }

    public bool IsReadOnly => Descriptor.IsReadOnly;
    public PropertyEditorKind EditorKind { get; }
    public Type? CustomEditorType { get; }
    public IReadOnlyList<string>? StandardValues { get; }
    public bool IsInteger => UnderlyingType == typeof(int) || UnderlyingType == typeof(long);

    /// <summary>Set by the property grid for collection / XAML fragment / complex rows.</summary>
    public ReactiveCommand<Unit, Unit>? EditCommand { get; set; }

    public ReactiveCommand<Unit, Unit> ResetCommand { get; }

    /// <summary>Edits the raw attribute text (e.g. to type a {Binding}); set by the property grid.</summary>
    public ReactiveCommand<Unit, Unit>? EditTextCommand { get; set; }

    /// <summary>The XAML string; setting it commits the change (undoable). Null resets to default.</summary>
    public string? Value
    {
        get => _value;
        set
        {
            var normalized = string.IsNullOrEmpty(value) ? null : value;
            if (normalized == _value && !_isMixed) return;
            if (normalized is not null && !Validate(normalized)) return;
            _value = normalized;
            IsMixed = false;
            RaiseValueChanged();
            _commit(this, normalized);
        }
    }

    /// <summary>The value the control actually has: <see cref="Value"/> or the default.</summary>
    public string? EffectiveValue => _value ?? DefaultText;

    /// <summary>Multi-selection with differing values.</summary>
    public bool IsMixed
    {
        get => _isMixed;
        private set => this.RaiseAndSetIfChanged(ref _isMixed, value);
    }

    /// <summary>Rendered bold when the property differs from its default.</summary>
    public bool IsModified => _value is not null && !XamlValueConverter.AreEquivalent(_value, DefaultText, PropertyType);

    public bool HasError
    {
        get => _hasError;
        private set => this.RaiseAndSetIfChanged(ref _hasError, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool IsMarkupExtension => XamlValueConverter.IsMarkupExtension(_value);

    /// <summary>Summary shown by collection/fragment/complex editors.</summary>
    public string Summary => _value ?? string.Empty;

    /// <summary>Updates the displayed value from the model without committing.</summary>
    public void UpdateFromModel(string? value, bool mixed)
    {
        _value = value;
        IsMixed = mixed;
        HasError = false;
        ErrorMessage = null;
        RaiseValueChanged();
    }

    private bool Validate(string text)
    {
        if (EditorKind is PropertyEditorKind.Collection or PropertyEditorKind.XamlFragment or PropertyEditorKind.Complex
            || UnderlyingType == typeof(string) || UnderlyingType == typeof(object) || XamlValueConverter.IsValid(text, UnderlyingType))
        {
            HasError = false;
            ErrorMessage = null;
            return true;
        }
        HasError = true;
        ErrorMessage = $"'{text}' is not a valid {UnderlyingType.Name}.";
        return false;
    }

    private void RaiseValueChanged()
    {
        this.RaisePropertyChanged(nameof(Value));
        this.RaisePropertyChanged(nameof(EffectiveValue));
        this.RaisePropertyChanged(nameof(IsModified));
        this.RaisePropertyChanged(nameof(IsMarkupExtension));
        this.RaisePropertyChanged(nameof(Summary));
    }

    private static PropertyEditorKind ChooseEditor(PropertyDescriptor d)
    {
        if (d.CollectionItemElement is not null) return PropertyEditorKind.Collection;
        if (d.IsXamlFragment) return PropertyEditorKind.XamlFragment;
        var t = Nullable.GetUnderlyingType(d.PropertyType) ?? d.PropertyType;
        if (t == typeof(bool)) return PropertyEditorKind.Bool;
        if (t.IsEnum) return PropertyEditorKind.Enum;
        if (t == typeof(Color) || typeof(IBrush).IsAssignableFrom(t)) return PropertyEditorKind.Color;
        if (t == typeof(Thickness)) return PropertyEditorKind.Thickness;
        if (t == typeof(CornerRadius)) return PropertyEditorKind.CornerRadius;
        if (t == typeof(double) || t == typeof(float) || t == typeof(decimal) || t == typeof(int) || t == typeof(long))
            return PropertyEditorKind.Numeric;
        if (t == typeof(FontFamily)) return PropertyEditorKind.FontFamily;
        if (t == typeof(Avalonia.Controls.GridLength)) return PropertyEditorKind.GridLength;
        if (t == typeof(string) || d.StandardValues is not null) return PropertyEditorKind.String;
        // Types with a string form (Point, Size, Rect, TimeSpan, DateTime...) edit as validated text.
        if (t.IsPrimitive || t.IsValueType || t.GetMethod("Parse", [typeof(string)]) is not null) return PropertyEditorKind.String;
        return PropertyEditorKind.Complex;
    }

    public override string ToString() => $"{Name} = {Value}";
}

/// <summary>One row on the Events tab.</summary>
public class EventRowViewModel : ReactiveObject
{
    private readonly Action<EventRowViewModel, string?> _commit;
    private string? _handlerName;
    private bool _hasError;

    public EventRowViewModel(EventDescriptor descriptor, Action<EventRowViewModel, string?> commit)
    {
        Descriptor = descriptor;
        _commit = commit;
    }

    public EventDescriptor Descriptor { get; }
    public string Name => Descriptor.Name;
    public string Description => Descriptor.Description;
    public string ArgsType => Descriptor.EventArgsTypeName;

    /// <summary>Raised by the view on double-click (generate or navigate).</summary>
    public ReactiveCommand<Unit, Unit>? ActivateCommand { get; set; }

    public string? HandlerName
    {
        get => _handlerName;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (v == _handlerName) return;
            if (v is not null && !Engine.CodeGeneration.CodeIdentifiers.IsValidIdentifier(v))
            {
                HasError = true;
                return;
            }
            HasError = false;
            _handlerName = v;
            this.RaisePropertyChanged();
            _commit(this, v);
        }
    }

    /// <summary>Error icon shown when the name is not a valid C# identifier.</summary>
    public bool HasError
    {
        get => _hasError;
        set => this.RaiseAndSetIfChanged(ref _hasError, value);
    }

    public void UpdateFromModel(string? handler)
    {
        _handlerName = handler;
        HasError = false;
        this.RaisePropertyChanged(nameof(HandlerName));
    }
}
