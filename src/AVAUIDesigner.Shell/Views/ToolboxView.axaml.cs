using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Shell.Controls;
using AVAUIDesigner.Shell.ViewModels;

namespace AVAUIDesigner.Shell.Views;

/// <summary>Toolbox: drag an item onto the design surface, or double-click to add it.</summary>
public partial class ToolboxView : UserControl
{
    private ToolboxItem? _pressedItem;
    private Point _pressPoint;

    public ToolboxView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_, _) => _pressedItem = null, RoutingStrategies.Tunnel);
        AddHandler(DoubleTappedEvent, OnDoubleTapped, RoutingStrategies.Bubble);
    }

    private static ToolboxItem? ItemFrom(object? source)
    {
        for (var c = source as Control; c is not null; c = c.GetVisualParent() as Control)
            if (c.DataContext is ToolboxItem item && c is Border) return item;
        return null;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pressedItem = ItemFrom(e.Source);
        _pressPoint = e.GetPosition(this);
        if (_pressedItem is not null && DataContext is ToolboxViewModel vm) vm.SelectedItem = _pressedItem;
    }

    private async void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedItem is not { } item) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _pressPoint.X) < 4 && Math.Abs(p.Y - _pressPoint.Y) < 4) return;
        _pressedItem = null;
#pragma warning disable CS0618 // DataObject is the in-process drag payload in Avalonia 11
        var data = new DataObject();
        data.Set(DesignCanvas.DescriptorDataFormat, item.Descriptor.ElementName);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
#pragma warning restore CS0618
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ItemFrom(e.Source) is { } item && DataContext is ToolboxViewModel vm)
            vm.AddControlCommand.Execute(item).Subscribe(_ => { }, _ => { });
    }
}
