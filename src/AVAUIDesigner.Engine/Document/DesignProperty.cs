namespace AVAUIDesigner.Engine.Document;

/// <summary>
/// A property set on an element in XAML. Values are kept in their XAML string form, which is the
/// single source of truth; typed values are derived on demand through <c>XamlValueConverter</c>.
/// </summary>
public class DesignProperty
{
    public DesignProperty(string name, string? stringValue)
    {
        Name = name;
        StringValue = stringValue;
    }

    /// <summary>
    /// Attribute name. Plain properties use the bare name ("Width"); property elements owned by
    /// another type keep their dotted form ("ToolTip.Tip"); prefixed attributes keep the prefix ("d:DesignWidth").
    /// </summary>
    public string Name { get; set; }

    /// <summary>Raw XAML attribute string. Null for element-form properties.</summary>
    public string? StringValue { get; set; }

    /// <summary>Parsed typed value, when the property type is known. Informational cache only.</summary>
    public object? TypedValue { get; set; }

    /// <summary>True = attribute form; false = property-element form (&lt;Grid.RowDefinitions&gt;).</summary>
    public bool IsAttributeForm { get; set; } = true;

    /// <summary>Child elements of a property element (RowDefinition, ContextMenu, DataTemplate...).</summary>
    public List<DesignElement> ElementValues { get; } = [];

    /// <summary>Text content of a property element that holds plain text.</summary>
    public string? ElementText { get; set; }

    public DesignProperty Clone(DesignElement newOwner)
    {
        var copy = new DesignProperty(Name, StringValue)
        {
            TypedValue = TypedValue,
            IsAttributeForm = IsAttributeForm,
            ElementText = ElementText,
        };
        foreach (var child in ElementValues)
        {
            var c = child.DeepClone();
            c.Parent = newOwner;
            c.OwnerProperty = copy;
            copy.ElementValues.Add(c);
        }
        return copy;
    }

    public override string ToString() => IsAttributeForm ? $"{Name}=\"{StringValue}\"" : $"<{Name}> ({ElementValues.Count})";
}

/// <summary>An attached property such as Grid.Row="1".</summary>
public class DesignAttachedProperty
{
    public DesignAttachedProperty(string ownerType, string name, string? stringValue)
    {
        OwnerType = ownerType;
        Name = name;
        StringValue = stringValue;
    }

    public string OwnerType { get; set; }
    public string Name { get; set; }
    public string? StringValue { get; set; }
    public string FullName => $"{OwnerType}.{Name}";

    public DesignAttachedProperty Clone() => new(OwnerType, Name, StringValue);

    public override string ToString() => $"{FullName}=\"{StringValue}\"";
}

/// <summary>An event wired to a handler method, e.g. Click="BtnOk_Click".</summary>
public class DesignEventHandler
{
    public DesignEventHandler(string eventName, string handlerName)
    {
        EventName = eventName;
        HandlerName = handlerName;
    }

    public string EventName { get; set; }
    public string HandlerName { get; set; }

    public DesignEventHandler Clone() => new(EventName, HandlerName);

    public override string ToString() => $"{EventName}={HandlerName}";
}

/// <summary>An xmlns declaration on the root element.</summary>
public record XamlNamespaceDeclaration(string Prefix, string Uri)
{
    public string AttributeName => string.IsNullOrEmpty(Prefix) ? "xmlns" : $"xmlns:{Prefix}";
}
