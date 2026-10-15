using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;

namespace AVAUIDesigner.Engine.CodeGeneration;

/// <summary>Turns a document into an MVVM view: xmlns:vm, x:DataType and a design DataContext.</summary>
public static class MvvmScaffolder
{
    public static void Apply(DesignDocument document, string viewModelNamespace, string viewModelClassName)
    {
        document.SetNamespace("vm", Serialization.XamlNamespaceMap.ClrNamespaceUri(viewModelNamespace));
        var root = document.RootElement;
        root.SetValue("x:DataType", $"vm:{viewModelClassName}");
        var dc = root.GetOrCreatePropertyElement("DataContext");
        dc.ElementValues.Clear();
        root.AddToPropertyElement(dc, new DesignElement($"vm:{viewModelClassName}"));
        document.CodeStyle = CodeBehindStyle.MVVM;
    }
}
