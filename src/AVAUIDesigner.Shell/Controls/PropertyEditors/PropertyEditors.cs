using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AVAUIDesigner.Engine.Properties;
using AVAUIDesigner.Shell.ViewModels;

namespace AVAUIDesigner.Shell.Controls.PropertyEditors;

/// <summary>
/// Base class of inline property editors. <see cref="Value"/> holds the XAML string and is bound two-way
/// to <see cref="PropertyRowViewModel.Value"/>; setting it commits an undoable change.
/// </summary>
public abstract class PropertyEditorBase : UserControl
{
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<PropertyEditorBase, object?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    private bool _updating;

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected PropertyRowViewModel? Row => DataContext as PropertyRowViewModel;

    protected string? Text => Value as string;

    /// <summary>The value to display: the set value or the property default.</summary>
    protected string? Effective => Text ?? Row?.DefaultText;

    /// <summary>True while the editor is updating its controls from <see cref="Value"/>.</summary>
    protected bool IsUpdating => _updating;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == DataContextProperty)
        {
            _updating = true;
            try
            {
                UpdateFromValue();
            }
            finally
            {
                _updating = false;
            }
        }
    }

    /// <summary>Refresh child controls from <see cref="Value"/>.</summary>
    protected abstract void UpdateFromValue();

    /// <summary>Commits a new XAML string (null = reset to default).</summary>
    protected void Commit(string? value)
    {
        if (_updating) return;
        SetCurrentValue(ValueProperty, string.IsNullOrEmpty(value) ? null : value);
    }

    protected static string Watermark(string? defaultText) => defaultText switch
    {
        null => string.Empty,
        "NaN" => "Auto",
        "Infinity" => "∞",
        _ => defaultText,
    };
}

/// <summary>TextBox (or suggestion box when the property has standard values).</summary>
public class StringPropertyEditor : PropertyEditorBase
{
    private readonly CommitTextBox _text = new() { MinHeight = 24, Padding = new Thickness(4, 2) };
    private readonly AutoCompleteBox? _suggest;

    public StringPropertyEditor(IReadOnlyList<string>? suggestions = null)
    {
        if (suggestions is { Count: > 0 })
        {
            _suggest = new AutoCompleteBox
            {
                ItemsSource = suggestions,
                FilterMode = AutoCompleteFilterMode.StartsWith,
                MinimumPrefixLength = 0,
                MinHeight = 24,
            };
            _suggest.LostFocus += (_, _) => Commit(_suggest.Text);
            _suggest.KeyDown += (_, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Enter) Commit(_suggest.Text);
            };
            _suggest.SelectionChanged += (_, _) =>
            {
                if (_suggest.SelectedItem is string s) Commit(s);
            };
            Content = _suggest;
        }
        else
        {
            _text.Committed += (_, v) => Commit(v);
            Content = _text;
        }
    }

    protected override void UpdateFromValue()
    {
        if (_suggest is not null)
        {
            _suggest.Text = Text;
            _suggest.Watermark = Watermark(Row?.DefaultText);
        }
        else
        {
            _text.Value = Text;
            _text.Watermark = Watermark(Row?.DefaultText);
        }
    }
}

/// <summary>CheckBox.</summary>
public class BoolPropertyEditor : PropertyEditorBase
{
    private readonly CheckBox _check = new() { VerticalAlignment = VerticalAlignment.Center, MinHeight = 24 };

    public BoolPropertyEditor()
    {
        _check.IsCheckedChanged += (_, _) =>
        {
            if (IsUpdating) return;
            Commit(_check.IsChecked == true ? "True" : "False");
        };
        Content = _check;
    }

    protected override void UpdateFromValue()
    {
        _check.IsChecked = string.Equals(Effective, "True", StringComparison.OrdinalIgnoreCase);
        _check.Content = Text is null ? $"({Effective ?? "default"})" : null;
        _check.Opacity = Text is null ? 0.75 : 1;
    }
}

/// <summary>ComboBox of enum member names (with glyphs for alignments).</summary>
public class EnumPropertyEditor : PropertyEditorBase
{
    private static readonly Dictionary<string, string> Glyphs = new()
    {
        ["Left"] = "⇤", ["Right"] = "⇥", ["Center"] = "↔", ["Stretch"] = "⇹",
        ["Top"] = "⤒", ["Bottom"] = "⤓", ["Horizontal"] = "↔", ["Vertical"] = "↕",
    };

    private readonly ComboBox _combo = new() { HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 24 };

    public EnumPropertyEditor(IReadOnlyList<string> values, bool showGlyphs)
    {
        _combo.ItemsSource = values;
        if (showGlyphs)
        {
            _combo.ItemTemplate = new FuncDataTemplate<string>((v, _) => new TextBlock
            {
                Text = v is not null && Glyphs.TryGetValue(v, out var g) ? $"{g}  {v}" : v,
            });
        }
        _combo.SelectionChanged += (_, _) =>
        {
            if (IsUpdating || _combo.SelectedItem is not string s) return;
            Commit(s);
        };
        Content = _combo;
    }

    protected override void UpdateFromValue()
    {
        _combo.SelectedItem = Effective;
        _combo.Opacity = Text is null ? 0.75 : 1;
    }
}

/// <summary>NumericUpDown; empty = default (e.g. Auto for Width).</summary>
public class NumericPropertyEditor : PropertyEditorBase
{
    private readonly NumericUpDown _number = new()
    {
        MinHeight = 24,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        FormatString = "0.####",
        Increment = 1,
        AllowSpin = true,
        ClipValueToMinMax = false,
    };

    public NumericPropertyEditor(bool integer)
    {
        if (integer) _number.FormatString = "0";
        _number.ValueChanged += (_, e) =>
        {
            if (IsUpdating) return;
            Commit(e.NewValue is { } v ? v.ToString(integer ? "0" : "0.####", CultureInfo.InvariantCulture) : null);
        };
        Content = _number;
    }

    protected override void UpdateFromValue()
    {
        _number.Watermark = Watermark(Row?.DefaultText);
        _number.Value = Text is not null && XamlValueConverter.TryParseDouble(Text, out var d) && double.IsFinite(d) ? (decimal)d : null;
    }
}

/// <summary>Four linked boxes for Thickness (L,T,R,B) or CornerRadius (TL,TR,BR,BL).</summary>
public class ThicknessPropertyEditor : PropertyEditorBase
{
    private readonly bool _cornerRadius;
    private readonly NumericUpDown[] _parts = new NumericUpDown[4];
    private readonly ToggleButton _link = new() { Content = "⛓", Padding = new Thickness(4, 0), MinHeight = 24, IsChecked = false };

    public ThicknessPropertyEditor(bool cornerRadius)
    {
        _cornerRadius = cornerRadius;
        var labels = cornerRadius ? new[] { "Top-left", "Top-right", "Bottom-right", "Bottom-left" } : new[] { "Left", "Top", "Right", "Bottom" };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,Auto"), ColumnSpacing = 2 };
        for (var i = 0; i < 4; i++)
        {
            var nud = new NumericUpDown
            {
                ShowButtonSpinner = false,
                FormatString = "0.##",
                MinHeight = 24,
                MinWidth = 0,
                Padding = new Thickness(2, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            ToolTip.SetTip(nud, labels[i]);
            var index = i;
            nud.ValueChanged += (_, e) => OnPartChanged(index, e.NewValue);
            Grid.SetColumn(nud, i);
            grid.Children.Add(nud);
            _parts[i] = nud;
        }
        ToolTip.SetTip(_link, "Link all four values");
        Grid.SetColumn(_link, 4);
        grid.Children.Add(_link);
        Content = grid;
    }

    private void OnPartChanged(int index, decimal? value)
    {
        if (IsUpdating) return;
        if (_link.IsChecked == true)
        {
            foreach (var p in _parts) p.Value = value;
        }
        var v = _parts.Select(p => (double)(p.Value ?? 0)).ToArray();
        string text = _cornerRadius
            ? XamlValueConverter.Serialize(new CornerRadius(v[0], v[1], v[2], v[3]), typeof(CornerRadius))
            : XamlValueConverter.Serialize(new Thickness(v[0], v[1], v[2], v[3]), typeof(Thickness));
        Commit(text);
    }

    protected override void UpdateFromValue()
    {
        double[] v = [0, 0, 0, 0];
        var effective = Effective;
        if (effective is not null)
        {
            if (_cornerRadius && XamlValueConverter.TryDeserialize(effective, typeof(CornerRadius), out var c) && c is CornerRadius cr)
                v = [cr.TopLeft, cr.TopRight, cr.BottomRight, cr.BottomLeft];
            else if (!_cornerRadius && XamlValueConverter.TryDeserialize(effective, typeof(Thickness), out var t) && t is Thickness th)
                v = [th.Left, th.Top, th.Right, th.Bottom];
        }
        for (var i = 0; i < 4; i++)
        {
            _parts[i].Value = Text is null && effective is null ? null : (decimal)v[i];
            _parts[i].Watermark = "0";
        }
        Opacity = Text is null ? 0.8 : 1;
    }
}

/// <summary>Color swatch + text + color picker popup.</summary>
public class ColorPropertyEditor : PropertyEditorBase
{
    private readonly Border _swatch = new()
    {
        Width = 18,
        Height = 18,
        BorderThickness = new Thickness(1),
        BorderBrush = Brushes.Gray,
        CornerRadius = new CornerRadius(3),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly CommitTextBox _text = new() { MinHeight = 24, Padding = new Thickness(4, 2) };
    private readonly ColorView _picker = new() { IsAlphaEnabled = true, IsColorPaletteVisible = true, Width = 300 };
    private readonly Button _pick = new() { Content = "▾", Padding = new Thickness(6, 0), MinHeight = 24 };

    public ColorPropertyEditor()
    {
        _text.Committed += (_, v) => Commit(v);
        _pick.Flyout = new Flyout { Content = _picker, Placement = PlacementMode.BottomEdgeAlignedRight };
        _picker.ColorChanged += (_, e) =>
        {
            if (IsUpdating || !_pick.Flyout!.IsOpen) return;
            Commit(XamlValueConverter.Serialize(e.NewColor, typeof(Color)));
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 4 };
        Grid.SetColumn(_text, 1);
        Grid.SetColumn(_pick, 2);
        grid.Children.Add(_swatch);
        grid.Children.Add(_text);
        grid.Children.Add(_pick);
        Content = grid;
    }

    protected override void UpdateFromValue()
    {
        _text.Value = Text;
        _text.Watermark = Watermark(Row?.DefaultText);
        if (Effective is { } s && Color.TryParse(s, out var c))
        {
            _swatch.Background = new SolidColorBrush(c);
            _picker.Color = c;
        }
        else
        {
            _swatch.Background = null;
        }
    }
}

/// <summary>Font family drop-down with a preview of each font.</summary>
public class FontPropertyEditor : PropertyEditorBase
{
    private static readonly Lazy<IReadOnlyList<string>> Fonts = new(() =>
    {
        try
        {
            return FontManager.Current.SystemFonts.Select(f => f.Name).Distinct().OrderBy(n => n).ToList();
        }
        catch (Exception)
        {
            return ["Inter", "Arial", "Courier New", "Segoe UI", "Times New Roman"];
        }
    });

    private readonly AutoCompleteBox _box = new()
    {
        FilterMode = AutoCompleteFilterMode.StartsWith,
        MinimumPrefixLength = 0,
        MinHeight = 24,
        MaxDropDownHeight = 300,
    };

    public FontPropertyEditor()
    {
        _box.ItemsSource = Fonts.Value;
        _box.ItemTemplate = new FuncDataTemplate<string>((name, _) => new TextBlock
        {
            Text = name,
            FontFamily = name is null ? FontFamily.Default : new FontFamily(name),
        });
        _box.LostFocus += (_, _) => Commit(_box.Text);
        _box.SelectionChanged += (_, _) =>
        {
            if (!IsUpdating && _box.SelectedItem is string s) Commit(s);
        };
        Content = _box;
    }

    protected override void UpdateFromValue()
    {
        _box.Text = Text;
        _box.Watermark = Watermark(Row?.DefaultText);
    }
}

/// <summary>Number + Pixel/Auto/Star selector.</summary>
public class GridLengthPropertyEditor : PropertyEditorBase
{
    private readonly NumericUpDown _number = new() { MinHeight = 24, FormatString = "0.##", Minimum = 0, Increment = 1 };
    private readonly ComboBox _unit = new() { ItemsSource = new[] { "Pixel", "Auto", "Star" }, MinHeight = 24 };

    public GridLengthPropertyEditor()
    {
        _number.ValueChanged += (_, _) => Update();
        _unit.SelectionChanged += (_, _) => Update();
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4 };
        Grid.SetColumn(_unit, 1);
        grid.Children.Add(_number);
        grid.Children.Add(_unit);
        Content = grid;
    }

    private void Update()
    {
        if (IsUpdating) return;
        var n = (double)(_number.Value ?? 1);
        var text = _unit.SelectedItem switch
        {
            "Auto" => "Auto",
            "Star" => n == 1 ? "*" : $"{XamlValueConverter.FormatDouble(n)}*",
            _ => XamlValueConverter.FormatDouble(n),
        };
        _number.IsEnabled = !Equals(_unit.SelectedItem, "Auto");
        Commit(text);
    }

    protected override void UpdateFromValue()
    {
        if (Effective is { } s && XamlValueConverter.TryDeserialize(s, typeof(GridLength), out var o) && o is GridLength gl)
        {
            _unit.SelectedItem = gl.IsAuto ? "Auto" : gl.IsStar ? "Star" : "Pixel";
            _number.Value = gl.IsAuto ? null : (decimal)gl.Value;
            _number.IsEnabled = !gl.IsAuto;
        }
        else
        {
            _unit.SelectedItem = "Star";
            _number.Value = 1;
        }
    }
}

/// <summary>Read-only summary + "…" button (collections, templates, complex values).</summary>
public class DialogPropertyEditor : PropertyEditorBase
{
    private readonly TextBlock _summary = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Opacity = 0.85,
    };

    private readonly Button _edit = new() { Content = "…", Padding = new Thickness(8, 0), MinHeight = 24 };

    public DialogPropertyEditor()
    {
        _edit.Click += (_, _) => Row?.EditCommand?.Execute().Subscribe(_ => { }, _ => { });
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4 };
        Grid.SetColumn(_edit, 1);
        grid.Children.Add(_summary);
        grid.Children.Add(_edit);
        Content = grid;
    }

    protected override void UpdateFromValue()
    {
        _summary.Text = Text ?? "(none)";
        ToolTip.SetTip(_summary, Text);
    }
}

/// <summary>Builds the right editor for each property row (the property grid's template selector).</summary>
public class PropertyEditorTemplate : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is not PropertyRowViewModel row) return null;
        Control editor = row.IsMarkupExtension && row.EditorKind is not (PropertyEditorKind.Collection or PropertyEditorKind.XamlFragment)
            ? new StringPropertyEditor()
            : row.EditorKind switch
            {
                PropertyEditorKind.Bool => new BoolPropertyEditor(),
                PropertyEditorKind.Enum => new EnumPropertyEditor(row.StandardValues ?? [],
                    row.UnderlyingType == typeof(HorizontalAlignment) || row.UnderlyingType == typeof(VerticalAlignment)
                                                                        || row.UnderlyingType == typeof(Orientation)),
                PropertyEditorKind.Numeric => new NumericPropertyEditor(row.IsInteger),
                PropertyEditorKind.Thickness => new ThicknessPropertyEditor(cornerRadius: false),
                PropertyEditorKind.CornerRadius => new ThicknessPropertyEditor(cornerRadius: true),
                PropertyEditorKind.Color => new ColorPropertyEditor(),
                PropertyEditorKind.FontFamily => new FontPropertyEditor(),
                PropertyEditorKind.GridLength => new GridLengthPropertyEditor(),
                PropertyEditorKind.Collection or PropertyEditorKind.XamlFragment or PropertyEditorKind.Complex => new DialogPropertyEditor(),
                PropertyEditorKind.Custom => CreateCustom(row),
                _ => new StringPropertyEditor(row.StandardValues),
            };

        editor.DataContext = row;
        var valueProperty = editor is PropertyEditorBase
            ? PropertyEditorBase.ValueProperty
            : AvaloniaPropertyRegistry.Instance.FindRegistered(editor, "Value");
        if (valueProperty is not null)
            editor.Bind(valueProperty, new Binding(nameof(PropertyRowViewModel.Value)) { Mode = BindingMode.TwoWay, Source = row });
        editor.IsEnabled = !row.IsReadOnly;
        return editor;
    }

    public bool Match(object? data) => data is PropertyRowViewModel;

    private static Control CreateCustom(PropertyRowViewModel row)
    {
        try
        {
            if (row.CustomEditorType is { } t && Activator.CreateInstance(t) is Control c) return c;
        }
        catch (Exception)
        {
            // Fall back to a text box if the plugin editor fails.
        }
        return new StringPropertyEditor();
    }
}
