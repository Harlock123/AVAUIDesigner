using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Serialization;

namespace AVAUIDesigner.Engine.Tests;

public class XamlSerializationTests
{
    private static readonly XamlSerializer Serializer = new();
    private static readonly XamlParser Parser = new();

    private static DesignElement Find(DesignDocument doc, string name) => doc.NamedElements[name];

    // ---------- Appendix A round trips ----------

    [Fact]
    public void MainWindowSample_ParsesIntoExpectedModel()
    {
        var doc = Samples.Parse(Samples.MainWindow);

        Assert.Equal("MyApp.Views.MainWindow", doc.XClass);
        Assert.Equal("MainWindow", doc.ClassName);
        Assert.Equal("MyApp.Views", doc.ClassNamespace);
        Assert.Equal(CodeBehindStyle.CodeBehind, doc.CodeStyle);
        Assert.Equal(["", "x", "d", "mc"], doc.Namespaces.Select(n => n.Prefix));

        var root = doc.RootElement;
        Assert.Equal("Window", root.ElementType);
        Assert.False(root.IsUnknown);
        Assert.Equal("MainWindow", root.GetValue("Title"));
        Assert.Equal("800", root.GetValue("Width"));
        Assert.Equal("450", root.GetValue("Height"));
        Assert.Equal("d", root.GetValue("mc:Ignorable"));
        Assert.DoesNotContain(root.Properties, p => p.Name == "x:Class");

        var grid = Assert.Single(root.Children);
        Assert.Equal("Grid", grid.ElementType);
        var rows = grid.GetPropertyElement("RowDefinitions");
        Assert.NotNull(rows);
        Assert.Equal(["Auto", "*", "Auto"], rows.ElementValues.Select(r => r.GetValue("Height")));
        Assert.All(rows.ElementValues, r => Assert.Same(grid, r.Parent));
        Assert.All(rows.ElementValues, r => Assert.Same(rows, r.OwnerProperty));

        Assert.Equal(["lblTitle", "txtName", "btnOk"], grid.Children.Select(c => c.Name));

        var title = Find(doc, "lblTitle");
        Assert.Equal("TextBlock", title.ElementType);
        Assert.Equal("0", title.GetValue("Grid.Row"));
        Assert.Equal("Welcome", title.GetValue("Text"));
        Assert.Equal("24", title.GetValue("FontSize"));
        Assert.Equal("0,16,0,8", title.GetValue("Margin"));

        var txt = Find(doc, "txtName");
        Assert.Equal("TxtName_TextChanged", txt.GetEventHandler("TextChanged"));
        Assert.Equal("1", txt.GetValue("Grid.Row"));
        Assert.Contains(txt.AttachedProperties, a => a is { OwnerType: "Grid", Name: "Row", StringValue: "1" });
        Assert.DoesNotContain(txt.Properties, p => p.Name == "TextChanged");

        var btn = Find(doc, "btnOk");
        Assert.Equal("BtnOk_Click", btn.GetEventHandler("Click"));
        Assert.Equal("OK", btn.GetValue("Content"));
        Assert.Equal(75, btn.Width);
        Assert.Equal(new Avalonia.Thickness(16), btn.Margin);
    }

    [Fact]
    public void MainWindowSample_RoundTripsStructurally()
    {
        var original = Samples.Parse(Samples.MainWindow);
        var text = Samples.Serialize(original);
        var reparsed = Samples.Parse(text);

        // Grid.Row="0" is the default for Grid.Row and is intentionally omitted on output.
        Assert.DoesNotContain("Grid.Row=\"0\"", text);
        Find(original, "lblTitle").SetValue("Grid.Row", null);

        DocumentAssert.Equal(original, reparsed);
    }

    [Fact]
    public void MvvmSample_RoundTripsStructurally()
    {
        var original = Samples.Parse(Samples.MvvmMainWindow);
        Assert.Equal(CodeBehindStyle.MVVM, original.CodeStyle);
        Assert.Equal("clr-namespace:MyApp.ViewModels", original.GetNamespaceUri("vm"));
        Assert.Equal("vm:MainWindowViewModel", original.RootElement.GetValue("x:DataType"));

        var dc = original.RootElement.GetPropertyElement("DataContext");
        Assert.NotNull(dc);
        var vm = Assert.Single(dc.ElementValues);
        Assert.Equal("vm:MainWindowViewModel", vm.ElementType);
        Assert.True(vm.IsUnknown);

        var text = Samples.Serialize(original);
        Assert.Contains("<Window.DataContext>", text);
        Assert.Contains("<vm:MainWindowViewModel />", text);
        Assert.Contains("Text=\"{Binding TxtName, Mode=TwoWay}\"", text);
        Assert.Contains("Command=\"{Binding BtnOkCommand}\"", text);

        var reparsed = Samples.Parse(text);
        var title = original.RootElement.Children[0].Children[0];
        Assert.Equal("0", title.GetValue("Grid.Row"));
        title.SetValue("Grid.Row", null); // default, omitted
        DocumentAssert.Equal(original, reparsed);
    }

    [Theory]
    [InlineData(Samples.MainWindow)]
    [InlineData(Samples.MvvmMainWindow)]
    public void Serialization_IsIdempotent(string xaml)
    {
        var first = Samples.Serialize(Samples.Parse(xaml));
        var second = Samples.Serialize(Samples.Parse(first));
        var third = Samples.Serialize(Samples.Parse(second));
        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }

    [Theory]
    [InlineData(Samples.MainWindow)]
    [InlineData(Samples.MvvmMainWindow)]
    public void Output_IsWellFormedXml(string xaml)
    {
        var text = Samples.Serialize(Samples.Parse(xaml));
        var x = XDocument.Parse(text);
        Assert.Equal("Window", x.Root!.Name.LocalName);
    }

    // ---------- Formatting rules ----------

    [Fact]
    public void Output_UsesFourSpaceIndentationPerDepth()
    {
        var text = Samples.Serialize(Samples.Parse(Samples.MainWindow));
        var lines = text.Split('\n');
        Assert.Contains("    <Grid>", lines);
        Assert.Contains("        <Grid.RowDefinitions>", lines);
        Assert.Contains("            <RowDefinition Height=\"Auto\" />", lines);
        Assert.Contains("        </Grid.RowDefinitions>", lines);
        Assert.Contains("    </Grid>", lines);
        Assert.Contains("</Window>", lines);
        Assert.DoesNotContain(lines, l => l.Contains('\t'));
        // Every start tag line is indented by a multiple of 4.
        foreach (var l in lines.Where(l => l.TrimStart().StartsWith('<')))
            Assert.Equal(0, (l.Length - l.TrimStart().Length) % 4);
        Assert.DoesNotContain(lines, l => l.EndsWith(' '));
    }

    [Fact]
    public void Output_WrapsRootAttributesAlignedUnderFirst()
    {
        var text = Samples.Serialize(Samples.Parse(Samples.MainWindow));
        var lines = text.Split('\n');
        Assert.Equal("<Window xmlns=\"https://github.com/avaloniaui\"", lines[0]);
        Assert.Equal("        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"", lines[1]);
    }

    [Fact]
    public void NamespaceDeclarations_OnlyOnRoot()
    {
        var text = Samples.Serialize(Samples.Parse(Samples.MvvmMainWindow));
        var rootEnd = text.IndexOf('>');
        Assert.DoesNotContain("xmlns", text[(rootEnd + 1)..]);
        Assert.Contains("xmlns:vm=\"clr-namespace:MyApp.ViewModels\"", text[..rootEnd]);
    }

    [Fact]
    public void NamespaceDeclaredOnNestedElement_IsHoistedToRoot()
    {
        var doc = Samples.ParseInWindow("""<local:Gauge xmlns:local="clr-namespace:MyApp.Controls" Value="3" />""");
        Assert.Equal("clr-namespace:MyApp.Controls", doc.GetNamespaceUri("local"));

        var text = Samples.Serialize(doc);
        var rootEnd = text.IndexOf('>');
        Assert.Contains("xmlns:local=\"clr-namespace:MyApp.Controls\"", text[..rootEnd]);
        Assert.Contains("<local:Gauge Value=\"3\" />", text);
        XDocument.Parse(text); // must be well-formed (prefix declared)
    }

    [Fact]
    public void RootNamespaces_OrderedDefaultXDMcThenOthers()
    {
        var doc = Samples.Parse("""
            <UserControl xmlns:vm="clr-namespace:A" xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                         xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns="https://github.com/avaloniaui" />
            """);
        var attrs = XamlSerializer.GetOrderedAttributes(doc.RootElement, doc, isRoot: true).Select(a => a.Key).ToList();
        Assert.Equal(["xmlns", "xmlns:x", "xmlns:d", "xmlns:mc", "xmlns:vm"], attrs.Take(5));
    }

    [Fact]
    public void Attributes_AreOrdered_NameLayoutContentOthersPrefixedAttachedEvents()
    {
        var doc = Samples.ParseInWindow("""
            <Grid>
                <Button Click="B_Click" Grid.Row="1" Grid.Column="2" Background="Red" FontSize="14" Content="Go"
                        d:DesignWidth="50" Margin="4" IsDefault="True" Width="80" x:Name="b" Height="30"
                        HorizontalAlignment="Left" xmlns:d="http://schemas.microsoft.com/expression/blend/2008" />
            </Grid>
            """);
        var b = doc.NamedElements["b"];
        var keys = XamlSerializer.GetOrderedAttributes(b, doc, isRoot: false).Select(a => a.Key).ToList();
        Assert.Equal(
        [
            "x:Name",
            "Width", "Height", "Margin", "HorizontalAlignment", // layout, in LayoutOrder
            "Content",                                          // content
            "Background", "FontSize", "IsDefault",              // others by category: Brush, Text, Behavior
            "d:DesignWidth",                                    // prefixed
            "Grid.Column", "Grid.Row",                          // attached, alphabetical
            "Click",                                            // events last
        ], keys);
    }

    [Fact]
    public void Attributes_XDirectivesFollowName()
    {
        var doc = Samples.Parse(Samples.MvvmMainWindow);
        var keys = XamlSerializer.GetOrderedAttributes(doc.RootElement, doc, isRoot: true).Select(a => a.Key).ToList();
        var cls = keys.IndexOf("x:Class");
        Assert.Equal("x:DataType", keys[cls + 1]);
        Assert.Equal("Width", keys[cls + 2]);
        Assert.True(keys.IndexOf("Title") > keys.IndexOf("Height"));
    }

    [Fact]
    public void DefaultValues_AreOmitted()
    {
        var doc = Samples.ParseInWindow("""
            <StackPanel>
                <Button HorizontalAlignment="Stretch" IsEnabled="True" Opacity="1" Margin="0" Content="A" />
                <Button IsEnabled="true" VerticalAlignment="stretch" Content="B" />
                <Button HorizontalAlignment="Left" IsEnabled="False" Content="C" />
            </StackPanel>
            """);
        var text = Samples.Serialize(doc);
        Assert.Contains("<Button Content=\"A\" />", text);
        Assert.Contains("<Button Content=\"B\" />", text);
        Assert.Contains("<Button HorizontalAlignment=\"Left\" Content=\"C\" IsEnabled=\"False\" />", text);
        Assert.DoesNotContain("Stretch", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Opacity", text);
    }

    [Fact]
    public void DefaultValues_WithoutDescriptorDefault_AreKept()
    {
        // Window.IsVisible has no default in the descriptor (Avalonia's default is False), so it is always written.
        var doc = Samples.Parse("""<Window xmlns="https://github.com/avaloniaui" IsVisible="True" />""");
        Assert.Contains("IsVisible=\"True\"", Samples.Serialize(doc));
    }

    [Fact]
    public void DefaultAttachedValues_AreOmitted()
    {
        var doc = Samples.ParseInWindow("""
            <Grid><Button Grid.Row="0" Grid.Column="0" Grid.RowSpan="1" Grid.ColumnSpan="2" /></Grid>
            """);
        var text = Samples.Serialize(doc);
        Assert.Contains("<Button Grid.ColumnSpan=\"2\" />", text);
    }

    [Fact]
    public void AttributeValues_AreEscaped()
    {
        var doc = Samples.ParseInWindow("<Button x:Name=\"b\" />");
        var b = doc.NamedElements["b"];
        b.SetValue("Content", "Fish & Chips < \"Deluxe\" >");
        b.SetValue("Tag", "line1\nline2\ttab");

        var text = Samples.Serialize(doc);
        Assert.Contains("Content=\"Fish &amp; Chips &lt; &quot;Deluxe&quot; &gt;\"", text);
        Assert.Contains("Tag=\"line1&#10;line2&#9;tab\"", text);

        var reparsed = Samples.Parse(text);
        Assert.Equal("Fish & Chips < \"Deluxe\" >", reparsed.NamedElements["b"].GetValue("Content"));
        Assert.Equal("line1\nline2\ttab", reparsed.NamedElements["b"].GetValue("Tag"));
    }

    [Fact]
    public void InnerText_IsEscaped()
    {
        var doc = Samples.ParseInWindow("<TextBlock>a &amp; b &lt; c</TextBlock>");
        Assert.Equal("a & b < c", doc.RootElement.Children[0].GetValue("Text"));
        var text = Samples.Serialize(doc);
        Assert.Contains("Text=\"a &amp; b &lt; c\"", text);
    }

    [Fact]
    public void MarkupExtensions_ArePreservedVerbatimAndNotTreatedAsEvents()
    {
        var doc = Samples.ParseInWindow("""<Button x:Name="b" Click="{Binding Foo}" Content="{DynamicResource Caption}" />""");
        var b = doc.NamedElements["b"];
        Assert.Empty(b.EventHandlers);
        Assert.Equal("{Binding Foo}", b.GetValue("Click"));
        var text = Samples.Serialize(doc);
        Assert.Contains("Content=\"{DynamicResource Caption}\"", text);
    }

    // ---------- Unknown elements ----------

    [Fact]
    public void UnknownElements_ArePreserved()
    {
        var doc = Samples.ParseInWindow("""
            <StackPanel xmlns:local="clr-namespace:MyApp.Controls">
                <FancyWidget Speed="3" Mode="Turbo">
                    <Button Content="Inside" />
                </FancyWidget>
                <local:Foo x:Name="foo" Bar="1" />
                <local:Button Content="not Avalonia's" />
            </StackPanel>
            """);
        var panel = doc.RootElement.Children[0];
        var fancy = panel.Children[0];
        Assert.True(fancy.IsUnknown);
        Assert.Null(fancy.Descriptor);
        Assert.Equal("3", fancy.GetValue("Speed"));
        Assert.Single(fancy.Children);

        var foo = doc.NamedElements["foo"];
        Assert.Equal("local:Foo", foo.ElementType);
        Assert.True(foo.IsUnknown);

        // A prefixed element in a non-Avalonia namespace never binds to a standard descriptor.
        var localButton = panel.Children[2];
        Assert.Equal("local:Button", localButton.ElementType);
        Assert.True(localButton.IsUnknown);

        var text = Samples.Serialize(doc);
        Assert.Contains("<FancyWidget Speed=\"3\" Mode=\"Turbo\">", text);
        Assert.Contains("<local:Foo x:Name=\"foo\" Bar=\"1\" />", text);
        Assert.Contains("<local:Button Content=\"not Avalonia's\" />", text);
        DocumentAssert.Equal(doc, Samples.Parse(text));
    }

    [Fact]
    public void KnownElements_AreNotUnknown()
    {
        var doc = Samples.Parse(Samples.MainWindow);
        Assert.All(doc.RootElement.SelfAndDescendants(), e => Assert.False(e.IsUnknown, e.ElementType));
        Assert.Equal("Avalonia.Controls.Button", doc.NamedElements["btnOk"].ClrType);
    }

    // ---------- Content ----------

    [Fact]
    public void InnerText_MapsToContentProperty()
    {
        var doc = Samples.ParseInWindow("""
            <StackPanel>
                <Button>OK</Button>
                <CheckBox>  Remember me  </CheckBox>
                <Button><Button.Content>Element form</Button.Content></Button>
            </StackPanel>
            """);
        var panel = doc.RootElement.Children[0];
        Assert.Equal("OK", panel.Children[0].GetValue("Content"));
        Assert.Null(panel.Children[0].InnerText);
        Assert.Equal("Remember me", panel.Children[1].GetValue("Content"));
        Assert.Equal("Element form", panel.Children[2].GetValue("Content"));
        Assert.Empty(panel.Children[2].PropertyElements);

        var text = Samples.Serialize(doc);
        Assert.Contains("<Button Content=\"OK\" />", text);
        Assert.Contains("<CheckBox Content=\"Remember me\" />", text);
    }

    [Fact]
    public void InnerText_MapsToTextContentProperty()
    {
        var registry = new ControlDescriptorRegistry();
        registry.Register(new ControlDescriptor("Window", "Avalonia.Controls.Window", "Root") { ContentProperty = "Content" });
        registry.Register(new ControlDescriptor("Caption", "MyApp.Caption", "Common") { ContentProperty = "Text" });
        var doc = Parser.Parse("""<Window xmlns="https://github.com/avaloniaui"><Caption>Hello</Caption></Window>""", registry);
        Assert.Equal("Hello", doc.RootElement.Children[0].GetValue("Text"));
    }

    [Fact]
    public void InnerText_WithoutContentProperty_IsKeptAsInnerText()
    {
        // Rectangle has no content property, so text content is preserved verbatim.
        var doc = Samples.ParseInWindow("<Rectangle>Hello</Rectangle>");
        var rect = doc.RootElement.Children[0];
        Assert.Equal("Hello", rect.InnerText);
        Assert.Contains("<Rectangle>Hello</Rectangle>", Samples.Serialize(doc));
    }

    [Fact]
    public void InnerText_DoesNotOverrideExplicitContentAttribute()
    {
        var doc = Samples.ParseInWindow("<Button Content=\"Attr\">Text</Button>");
        var b = doc.RootElement.Children[0];
        Assert.Equal("Attr", b.GetValue("Content"));
        Assert.Equal("Text", b.InnerText);
    }

    [Fact]
    public void PropertyElement_OwnedByOtherType_KeepsDottedName()
    {
        var doc = Samples.ParseInWindow("""
            <Button x:Name="b">
                <ToolTip.Tip>
                    <TextBlock Text="Help" />
                </ToolTip.Tip>
                <Button.ContextMenu>
                    <ContextMenu><MenuItem Header="Copy" /></ContextMenu>
                </Button.ContextMenu>
            </Button>
            """);
        var b = doc.NamedElements["b"];
        Assert.NotNull(b.GetPropertyElement("ToolTip.Tip"));
        Assert.NotNull(b.GetPropertyElement("ContextMenu"));
        var text = Samples.Serialize(doc);
        Assert.Contains("<ToolTip.Tip>", text);
        Assert.Contains("<Button.ContextMenu>", text);
        DocumentAssert.Equal(doc, Samples.Parse(text));
    }

    // ---------- Errors ----------

    [Fact]
    public void TryParse_ReportsLineAndColumnOnError()
    {
        const string bad = "<Window xmlns=\"https://github.com/avaloniaui\">\n  <Grid>\n    <Button>\n  </Grid>\n</Window>";
        var ok = Parser.TryParse(bad, Samples.Registry, out var doc, out var error);
        Assert.False(ok);
        Assert.Null(doc);
        Assert.NotNull(error);
        Assert.Equal(4, error.Line);
        Assert.True(error.Column > 0);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.StartsWith("(4,", error.ToString());
    }

    [Fact]
    public void Parse_ThrowsXamlParseExceptionOnError()
    {
        var ex = Assert.Throws<XamlParseException>(() => Parser.Parse("<Window", Samples.Registry));
        Assert.True(ex.Error.Line >= 1);
    }

    [Fact]
    public void TryParse_SucceedsForValidXaml()
    {
        Assert.True(Parser.TryParse(Samples.MainWindow, Samples.Registry, out var doc, out var error));
        Assert.NotNull(doc);
        Assert.Null(error);
    }

    [Fact]
    public void ParseDetailed_RecordsElementLocations()
    {
        var result = Parser.ParseDetailed(Samples.MainWindow, Samples.Registry);
        var btn = result.Document.NamedElements["btnOk"];
        var (line, col) = result.ElementLocations[btn];
        Assert.Equal(Samples.MainWindow.Split('\n').ToList().FindIndex(l => l.Contains("<Button")) + 1, line);
        Assert.True(col > 1);
    }

    [Fact]
    public void ParseDetailed_WarnsAboutUnknownAttributes()
    {
        var result = Parser.ParseDetailed(
            """<Window xmlns="https://github.com/avaloniaui"><Button Contnet="typo" /></Window>""", Samples.Registry);
        var w = Assert.Single(result.Warnings);
        Assert.Contains("Contnet", w.Message);
    }

    // ---------- Fragments ----------

    [Fact]
    public void ParseFragment_ParsesSiblingsUsingDocumentNamespaces()
    {
        var doc = Samples.Parse(Samples.MvvmMainWindow);
        var elements = Parser.ParseFragment(
            """<Button x:Name="a" Content="A" Click="A_Click" /><vm:Thing /><TextBox Grid.Row="2" />""", doc, Samples.Registry);
        Assert.Equal(3, elements.Count);
        Assert.Equal("a", elements[0].Name);
        Assert.Equal("A_Click", elements[0].GetEventHandler("Click"));
        Assert.False(elements[0].IsUnknown);
        Assert.Equal("vm:Thing", elements[1].ElementType);
        Assert.Equal("2", elements[2].GetValue("Grid.Row"));
        Assert.All(elements, e => Assert.Null(e.Parent));
    }

    [Fact]
    public void ParseFragment_InvalidXamlThrows()
    {
        var doc = Samples.Parse(Samples.MainWindow);
        Assert.Throws<XamlParseException>(() => Parser.ParseFragment("<Button>", doc, Samples.Registry));
    }

    [Fact]
    public void SerializeElement_WritesSubtreeWithoutNamespaces()
    {
        var doc = Samples.Parse(Samples.MainWindow);
        var grid = doc.RootElement.Children[0];
        var text = Serializer.SerializeElement(grid);
        Assert.StartsWith("<Grid>", text);
        Assert.EndsWith("</Grid>", text);
        Assert.DoesNotContain("xmlns", text);
        Assert.Contains("    <Grid.RowDefinitions>", text);
        Assert.Contains("x:Name=\"btnOk\"", text);

        var single = Serializer.SerializeElement(doc.NamedElements["btnOk"]);
        Assert.DoesNotContain("\n\n", single);
        Assert.StartsWith("<Button x:Name=\"btnOk\"", single);
    }

    [Fact]
    public void SerializeElement_ThenParseFragment_RoundTrips()
    {
        var doc = Samples.Parse(Samples.MainWindow);
        var grid = doc.RootElement.Children[0];
        var copy = Assert.Single(Parser.ParseFragment(Serializer.SerializeElement(grid), doc, Samples.Registry));
        grid.Children[0].SetValue("Grid.Row", null); // default, omitted on output
        DocumentAssert.Equal(grid, copy);
    }

    [Fact]
    public void SerializeWithMap_SpansCoverEachElement()
    {
        var doc = Samples.Parse(Samples.MainWindow);
        var text = Serializer.SerializeWithMap(doc, out var spans);
        var btn = doc.NamedElements["btnOk"];
        var (start, length) = spans[btn];
        var slice = text.Substring(start, length);
        Assert.StartsWith("<Button x:Name=\"btnOk\"", slice);
        Assert.EndsWith("/>", slice);
        var (rs, rl) = spans[doc.RootElement];
        Assert.Equal(0, rs);
        Assert.EndsWith("</Window>", text.Substring(rs, rl));
    }

    // ---------- Model-built documents ----------

    [Fact]
    public void NewDocument_SerializesWithDefaultNamespaces()
    {
        var window = Samples.Registry.GetByElementName("Window")!.DefaultElementFactory();
        var doc = new DesignDocument(window) { XClass = "MyApp.NewWindow" };
        var b = Samples.Registry.GetByElementName("Button")!.DefaultElementFactory();
        b.Name = "button1";
        window.AddChild(b);

        var text = Samples.Serialize(doc);
        Assert.Contains("xmlns=\"https://github.com/avaloniaui\"", text);
        Assert.Contains("xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"", text);
        Assert.Contains("x:Class=\"MyApp.NewWindow\"", text);
        Assert.Contains("    <Button x:Name=\"button1\" Width=\"75\" Height=\"23\" Content=\"Button\" />", text);
        DocumentAssert.Equal(doc, Samples.Parse(text));
    }

    [Fact]
    public void EmptyPropertyElement_IsWrittenSelfClosing()
    {
        var doc = Samples.ParseInWindow("<Grid x:Name=\"g\" />");
        doc.NamedElements["g"].GetOrCreatePropertyElement("RowDefinitions");
        Assert.Contains("<Grid.RowDefinitions />", Samples.Serialize(doc));
    }

    // ---------- Performance (spec §15) ----------

    [Fact]
    public void FiveHundredElementDocument_ParseAndSerialize_Under200ms()
    {
        var xaml = BuildLargeDocument(500);
        var doc0 = Samples.Parse(xaml);
        Assert.Equal(500, doc0.AllElements.Count());

        // Warm-up (JIT, descriptor lookups, regex/template caches).
        for (var i = 0; i < 3; i++) Samples.Serialize(Samples.Parse(xaml));

        var best = long.MaxValue;
        for (var run = 0; run < 5; run++)
        {
            var sw = Stopwatch.StartNew();
            var doc = Samples.Parse(xaml);
            var text = Samples.Serialize(doc);
            sw.Stop();
            Assert.NotEmpty(text);
            best = Math.Min(best, sw.ElapsedMilliseconds);
        }
        Assert.True(best < 200, $"Parse + serialize of 500 elements took {best} ms");
    }

    [Fact]
    public void LargeDocument_RoundTripsIdempotently()
    {
        var xaml = BuildLargeDocument(500);
        var first = Samples.Serialize(Samples.Parse(xaml));
        Assert.Equal(first, Samples.Serialize(Samples.Parse(first)));
    }

    private static string BuildLargeDocument(int elementCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Title="Big">""");
        sb.AppendLine("<StackPanel>");
        var count = 2; // Window + StackPanel
        var i = 0;
        while (count < elementCount)
        {
            // Each group: Grid + 3 children = 4 elements.
            if (elementCount - count >= 4)
            {
                sb.AppendLine($"""
                    <Grid Margin="4" Background="#FFEEEEEE">
                        <TextBlock x:Name="lbl{i}" Text="Label {i}" FontSize="12" Grid.Column="0" />
                        <TextBox x:Name="txt{i}" Watermark="Value &amp; more" Width="120" Grid.Column="1" TextChanged="Txt{i}_TextChanged" />
                        <Button x:Name="btn{i}" Content="Go {i}" HorizontalAlignment="Right" Grid.Column="2" Click="Btn{i}_Click" />
                    </Grid>
                    """);
                count += 4;
            }
            else
            {
                sb.AppendLine($"""<Border BorderThickness="1" CornerRadius="2" />""");
                count++;
            }
            i++;
        }
        sb.AppendLine("</StackPanel>");
        sb.AppendLine("</Window>");
        return sb.ToString();
    }
}
