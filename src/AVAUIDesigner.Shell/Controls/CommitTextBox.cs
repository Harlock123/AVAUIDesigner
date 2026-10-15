using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AVAUIDesigner.Shell.Controls;

/// <summary>
/// A TextBox whose <see cref="Value"/> is only updated when the user presses Enter or leaves the box
/// (Escape reverts). Used wherever a change must be one undo step instead of one per keystroke.
/// </summary>
public class CommitTextBox : TextBox
{
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<CommitTextBox, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    private bool _updating;

    protected override Type StyleKeyOverride => typeof(TextBox);

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Raised after a commit (Enter or focus loss) with the committed text.</summary>
    public event EventHandler<string?>? Committed;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty && !_updating) Text = Value;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !AcceptsReturn)
        {
            Commit();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape)
        {
            Text = Value;
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        Commit();
        base.OnLostFocus(e);
    }

    public void Commit()
    {
        var text = string.IsNullOrEmpty(Text) ? null : Text;
        if (text == Value)
        {
            Committed?.Invoke(this, text);
            return;
        }
        _updating = true;
        try
        {
            SetCurrentValue(ValueProperty, text);
        }
        finally
        {
            _updating = false;
        }
        // The bound source may reject or normalize the value.
        if (Value != text) Text = Value;
        Committed?.Invoke(this, text);
    }
}
