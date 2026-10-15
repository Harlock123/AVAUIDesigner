using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AVAUIDesigner.Shell.Controls;
using AVAUIDesigner.Shell.ViewModels;

namespace AVAUIDesigner.Shell.Views;

public partial class PropertyGridView : UserControl
{
    public PropertyGridView()
    {
        InitializeComponent();
        // The focused row drives the description bar.
        AddHandler(GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble);
        AddHandler(DoubleTappedEvent, OnDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (DataContext is not PropertyGridViewModel vm) return;
        for (var c = e.Source as Control; c is not null; c = c.GetVisualParent() as Control)
        {
            if (c.DataContext is PropertyRowViewModel row)
            {
                vm.SelectedRow = row;
                return;
            }
        }
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        for (var c = e.Source as Control; c is not null; c = c.GetVisualParent() as Control)
        {
            if (c is CommitTextBox { Tag: EventRowViewModel row })
            {
                row.ActivateCommand?.Execute().Subscribe(_ => { }, _ => { });
                e.Handled = true;
                return;
            }
        }
    }

    /// <summary>Moves keyboard focus into the property list (Properties command).</summary>
    public void FocusFirstEditor()
    {
        var first = PropertyList.GetVisualDescendants().OfType<InputElement>().FirstOrDefault(x => x.Focusable && x is TextBox or ComboBox or CheckBox);
        first?.Focus();
    }
}
