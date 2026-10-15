using AVAUIDesigner.Engine.Descriptors;

namespace AVAUIDesigner.Controls.Common;

/// <summary>Base class of the built-in descriptors: wires up the vector icon factory.</summary>
public abstract class StandardDescriptor : ControlDescriptor
{
    protected StandardDescriptor(string elementName, Type clrType, string category)
        : base(elementName, clrType, category)
    {
        IconFactory = DescriptorIcons.Create;
    }

    /// <summary>For designable XAML objects that are not <c>Control</c>s (Flyout, DataGridTextColumn...): no live control factory.</summary>
    protected StandardDescriptor(string elementName, string fullTypeName, string category)
        : base(elementName, fullTypeName, category)
    {
        IconFactory = DescriptorIcons.Create;
    }
}
