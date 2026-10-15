using Avalonia;
using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Shell.Controls;

/// <summary>What view models need from the design surface (layout information lives only in the live controls).</summary>
public interface IDesignSurface
{
    /// <summary>Bounds of an element in design-surface coordinates, or null when it is not visible.</summary>
    Rect? GetElementBounds(DesignElement element);

    /// <summary>
    /// Property values that would put <paramref name="element"/> at <paramref name="newBounds"/> within its
    /// current parent; null when the parent does not support absolute positioning (StackPanel...).
    /// </summary>
    IReadOnlyDictionary<string, string?>? GetPlacementValues(DesignElement element, Rect newBounds);

    /// <summary>Placement values for <paramref name="element"/> at <paramref name="bounds"/> inside <paramref name="container"/> (which need not be its parent yet).</summary>
    IReadOnlyDictionary<string, string?>? GetPlacementValuesIn(DesignElement container, DesignElement element, Rect bounds);

    void ZoomToFit();
    void ZoomTo(double factor);
    double ZoomFactor { get; set; }
}
