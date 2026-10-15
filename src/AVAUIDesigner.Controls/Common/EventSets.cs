using AVAUIDesigner.Engine.Descriptors;

namespace AVAUIDesigner.Controls.Common;

/// <summary>Reusable event descriptor sets.</summary>
public static class EventSets
{
    public const string Interactivity = "Avalonia.Interactivity";
    public const string Input = "Avalonia.Input";
    public const string Controls = "Avalonia.Controls";
    public const string Primitives = "Avalonia.Controls.Primitives";

    public static EventDescriptor E(string name, string args = "RoutedEventArgs", string ns = Interactivity, string? description = null) =>
        new(name, args, ns, description);

    /// <summary>Input/lifecycle events every Control raises.</summary>
    public static IEnumerable<EventDescriptor> Control()
    {
        yield return E("PointerPressed", "PointerPressedEventArgs", Input, "Occurs when a pointer button is pressed over the control.");
        yield return E("PointerReleased", "PointerReleasedEventArgs", Input, "Occurs when a pointer button is released over the control.");
        yield return E("PointerMoved", "PointerEventArgs", Input, "Occurs when the pointer moves over the control.");
        yield return E("PointerEntered", "PointerEventArgs", Input, "Occurs when the pointer enters the control.");
        yield return E("PointerExited", "PointerEventArgs", Input, "Occurs when the pointer leaves the control.");
        yield return E("PointerWheelChanged", "PointerWheelEventArgs", Input, "Occurs when the mouse wheel is turned over the control.");
        yield return E("Tapped", "TappedEventArgs", Input, "Occurs when the control is tapped or clicked.");
        yield return E("DoubleTapped", "TappedEventArgs", Input, "Occurs when the control is double-tapped or double-clicked.");
        yield return E("KeyDown", "KeyEventArgs", Input, "Occurs when a key is pressed while the control has focus.");
        yield return E("KeyUp", "KeyEventArgs", Input, "Occurs when a key is released while the control has focus.");
        yield return E("GotFocus", "GotFocusEventArgs", Input, "Occurs when the control receives focus.");
        yield return E("LostFocus", "RoutedEventArgs", Interactivity, "Occurs when the control loses focus.");
        yield return E("SizeChanged", "SizeChangedEventArgs", Controls, "Occurs when the control's size changes.");
        yield return E("Loaded", "RoutedEventArgs", Interactivity, "Occurs when the control is attached to a visual tree and laid out.");
        yield return E("Unloaded", "RoutedEventArgs", Interactivity, "Occurs when the control is removed from the visual tree.");
    }

    /// <summary>Combines event sets; earlier entries win on duplicate names. Pass the control-specific set first.</summary>
    public static IReadOnlyList<EventDescriptor> Combine(params IEnumerable<EventDescriptor>[] sets)
    {
        var result = new List<EventDescriptor>();
        var seen = new HashSet<string>();
        foreach (var set in sets)
        foreach (var e in set)
            if (seen.Add(e.Name)) result.Add(e);
        return result;
    }
}
