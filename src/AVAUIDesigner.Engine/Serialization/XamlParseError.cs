namespace AVAUIDesigner.Engine.Serialization;

/// <summary>Location and message of a XAML parse failure. Line/column are 1-based (0 = unknown).</summary>
public record XamlParseError(string Message, int Line, int Column)
{
    public override string ToString() => Line > 0 ? $"({Line},{Column}): {Message}" : Message;
}

/// <summary>A non-fatal problem found while parsing (unknown attribute, invalid value...).</summary>
public record XamlParseWarning(string Message, int Line, int Column, int Length);

public class XamlParseException : Exception
{
    public XamlParseException(XamlParseError error, Exception? inner = null) : base(error.ToString(), inner)
    {
        Error = error;
    }

    public XamlParseError Error { get; }
}
