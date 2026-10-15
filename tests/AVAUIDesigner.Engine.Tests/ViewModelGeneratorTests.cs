using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.UndoRedo;
using AVAUIDesigner.Engine.UndoRedo.Commands;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AVAUIDesigner.Engine.Tests;

public class ViewModelGeneratorTests
{
    private static readonly ViewModelGenerator Gen = new();
    private static CodeGenOptions Options => new() { Namespace = "MyApp.Views", ClassName = "MainWindow", BaseClass = "Window" };

    [Fact]
    public void MvvmSample_GeneratesObservablePropertyAndRelayCommand()
    {
        var code = Gen.Generate(Samples.Parse(Samples.MvvmMainWindow), Options);
        var root = CSharpAssert.ValidAndTidy(code);

        Assert.Contains("namespace MyApp.ViewModels;", code);
        Assert.Contains("using CommunityToolkit.Mvvm.ComponentModel;", code);
        Assert.Contains("using CommunityToolkit.Mvvm.Input;", code);
        var cls = Assert.Single(root.DescendantNodes().OfType<ClassDeclarationSyntax>());
        Assert.Equal("MainWindowViewModel", cls.Identifier.Text);
        Assert.Equal("ObservableObject", cls.BaseList!.Types.Single().ToString());

        Assert.Contains("    [ObservableProperty]\n    private string _txtName = string.Empty;", code);
        Assert.Contains("    [RelayCommand]\n    private void BtnOk()\n    {\n        // TODO: implement\n    }", code);

        var field = Assert.Single(cls.Members.OfType<FieldDeclarationSyntax>());
        Assert.Equal("ObservableProperty", field.AttributeLists.Single().Attributes.Single().Name.ToString());
        var method = Assert.Single(cls.Members.OfType<MethodDeclarationSyntax>());
        Assert.Equal("RelayCommand", method.AttributeLists.Single().Attributes.Single().Name.ToString());
    }

    [Fact]
    public void Analyze_FindsBoundPropertiesAndCommands()
    {
        var (props, commands) = Gen.Analyze(Samples.Parse(Samples.MvvmMainWindow));
        var p = Assert.Single(props);
        Assert.Equal(("TxtName", "string", "string.Empty"), (p.Path, p.Type, p.Initializer));
        Assert.Equal("TextBox.Text", p.Source);
        var c = Assert.Single(commands);
        Assert.Equal(("BtnOkCommand", "BtnOk"), (c.Path, c.Method));
    }

    [Fact]
    public void Analyze_DeduplicatesPaths_FirstOccurrenceWins()
    {
        var doc = Samples.ParseInWindow("""
            <StackPanel>
                <TextBox Text="{Binding Name}" />
                <TextBlock Text="{Binding Name}" />
                <Button Command="{Binding SaveCommand}" />
                <Button Command="{Binding SaveCommand}" />
                <Button Command="{Binding Refresh}" />
            </StackPanel>
            """);
        var (props, commands) = Gen.Analyze(doc);
        Assert.Single(props);
        Assert.Equal(["Save", "Refresh"], commands.Select(c => c.Method));
    }

    [Fact]
    public void Analyze_MapsPropertyTypes()
    {
        var doc = Samples.ParseInWindow("""
            <StackPanel>
                <CheckBox IsChecked="{Binding IsAgreed}" />
                <Slider Value="{Binding Volume}" />
                <NumericUpDown Value="{Binding Count}" />
                <ListBox ItemsSource="{Binding People}" />
                <Border Background="{Binding Fill}" />
                <Mystery Foo="{Binding Unknown}" />
            </StackPanel>
            """);
        var props = Gen.Analyze(doc).Properties.ToDictionary(p => p.Path, p => p.Type);
        Assert.Equal("bool?", props["IsAgreed"]);
        Assert.Equal("double", props["Volume"]);
        Assert.Equal("ObservableCollection<object>", props["People"]);
        Assert.Equal("Avalonia.Media.IBrush?", props["Fill"]);
        Assert.Equal("object?", props["Unknown"]);
        Assert.True(props.ContainsKey("Count"));

        var code = Gen.Generate(doc, Options);
        CSharpAssert.ValidAndTidy(code);
        Assert.Contains("private ObservableCollection<object> _people = new();", code);
        Assert.Contains("private bool? _isAgreed;", code);
    }

    [Fact]
    public void NoBindings_GeneratesEmptyClass()
    {
        var code = Gen.Generate(Samples.Parse(Samples.MainWindow), Options);
        var root = CSharpAssert.ValidAndTidy(code);
        Assert.Empty(root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single().Members);
    }

    [Fact]
    public void ViewModelNamespace_CanBeOverridden()
    {
        var options = Options;
        options.ViewModelNamespace = "Other.VMs";
        Assert.Contains("namespace Other.VMs;", Gen.Generate(Samples.Parse(Samples.MvvmMainWindow), options));
        Assert.Equal("MyApp.ViewModels", CodeGenOptions.DefaultViewModelNamespace("MyApp.Views"));
        Assert.Equal("MyApp.ViewModels", CodeGenOptions.DefaultViewModelNamespace("MyApp"));
        Assert.Equal("MainWindowViewModel", Gen.GetViewModelClassName(Options));
    }

    // ---------- Merge ----------

    [Fact]
    public void Merge_AddsOnlyMissingMembers()
    {
        const string existing = """
            using CommunityToolkit.Mvvm.ComponentModel;
            using CommunityToolkit.Mvvm.Input;

            namespace MyApp.ViewModels;

            public partial class MainWindowViewModel : ObservableObject
            {
                [ObservableProperty]
                private string _txtName = "preset";

                public int Extra { get; set; }
            }

            """;
        var code = Gen.Generate(Samples.Parse(Samples.MvvmMainWindow), Options, existing);
        var root = CSharpAssert.ValidAndTidy(code);
        Assert.Contains("private string _txtName = \"preset\";", code);
        Assert.Single(root.DescendantNodes().OfType<FieldDeclarationSyntax>());
        Assert.Contains("public int Extra { get; set; }", code);
        var cmd = CSharpAssert.Method(root, "BtnOk");
        Assert.Equal("RelayCommand", cmd.AttributeLists.Single().Attributes.Single().Name.ToString());
        Assert.StartsWith(existing[..existing.IndexOf("public int Extra", StringComparison.Ordinal)], code);
    }

    [Fact]
    public void Merge_NothingMissing_ReturnsSourceUnchanged()
    {
        var doc = Samples.Parse(Samples.MvvmMainWindow);
        var generated = Gen.Generate(doc, Options);
        Assert.Equal(generated, Gen.Generate(doc, Options, generated));
    }

    [Fact]
    public void Merge_RecognizesExistingPropertyAndCommandNames()
    {
        const string existing = """
            namespace MyApp.ViewModels;
            public partial class MainWindowViewModel : ObservableObject
            {
                public string TxtName { get; set; } = "";
                public ICommand BtnOkCommand { get; }
            }
            """;
        var code = Gen.Generate(Samples.Parse(Samples.MvvmMainWindow), Options, existing);
        Assert.Equal(existing, code);
    }

    [Fact]
    public void Merge_IntoEmptyClass_ProducesValidCode()
    {
        const string existing = "namespace MyApp.ViewModels;\n\npublic partial class MainWindowViewModel : ObservableObject\n{\n}\n";
        var code = Gen.Generate(Samples.Parse(Samples.MvvmMainWindow), Options, existing);
        var root = CSharpAssert.ValidAndTidy(code);
        CSharpAssert.Method(root, "BtnOk");
        Assert.Contains("private string _txtName = string.Empty;", code);
    }

    // ---------- Binding heuristics (spec §9) ----------

    [Theory]
    [InlineData("TextBox", "txtName", "Text", "{Binding TxtNameText, Mode=TwoWay}")]
    [InlineData("Button", "btnOk", "Command", "{Binding BtnOkCommand}")]
    [InlineData("CheckBox", "agree", "IsChecked", "{Binding IsAgreeChecked}")]
    [InlineData("ComboBox", "country", "SelectedItem", "{Binding SelectedCountryItem}")]
    [InlineData("Slider", "volume", "Value", "{Binding VolumeValue}")]
    [InlineData("ProgressBar", "progress", "Value", "{Binding ProgressValue}")]
    public void Heuristics_ProduceSpecBindings(string type, string name, string property, string binding)
    {
        var e = new DesignElement(type, Samples.Registry.GetByElementName(type)) { Name = name };
        Assert.Equal((property, binding), MvvmBindingHeuristics.GetDefaultBinding(e));
    }

    [Fact]
    public void Heuristics_IgnoreUnnamedAndUnsupported()
    {
        Assert.Null(MvvmBindingHeuristics.GetDefaultBinding(new DesignElement("TextBox")));
        Assert.Null(MvvmBindingHeuristics.GetDefaultBinding(new DesignElement("TextBlock") { Name = "lbl" }));
    }

    [Fact]
    public void Suggest_SkipsExistingBindings_AndAppliesThroughCompositeCommand()
    {
        var doc = Samples.ParseInWindow("""
            <StackPanel>
                <TextBox x:Name="txtName" Text="literal" />
                <TextBox x:Name="txtBound" Text="{Binding Something}" />
                <Button x:Name="btnOk" Content="OK" />
                <CheckBox x:Name="chk" />
                <TextBox Text="unnamed" />
            </StackPanel>
            """);
        var suggestions = MvvmBindingHeuristics.Suggest(doc).ToList();
        Assert.Equal(["txtName", "btnOk", "chk"], suggestions.Select(s => s.Element.Name));

        var stack = new UndoRedoStack { Document = doc };
        var before = Samples.Serialize(doc);
        stack.Execute(new CompositeCommand("Apply MVVM bindings",
            suggestions.Select(s => (IDesignCommand)new SetPropertyCommand(s.Element, s.Property, s.Binding))));
        Assert.Equal("{Binding TxtNameText, Mode=TwoWay}", doc.NamedElements["txtName"].GetValue("Text"));
        Assert.Equal("{Binding BtnOkCommand}", doc.NamedElements["btnOk"].GetValue("Command"));
        Assert.Equal("{Binding IsChkChecked}", doc.NamedElements["chk"].GetValue("IsChecked"));

        var (props, commands) = Gen.Analyze(doc);
        Assert.Equal(["TxtNameText", "Something", "IsChkChecked"], props.Select(p => p.Path));
        Assert.Equal(["BtnOk"], commands.Select(c => c.Method));
        CSharpAssert.ValidAndTidy(Gen.Generate(doc, Options));

        stack.Undo();
        Assert.Equal(before, Samples.Serialize(doc));
    }

    [Theory]
    [InlineData("{Binding Foo}", "Foo")]
    [InlineData("{Binding Foo, Mode=TwoWay}", "Foo")]
    [InlineData("{Binding Path=Foo}", "Foo")]
    [InlineData("{Binding Mode=TwoWay, Path=Foo}", "Foo")]
    [InlineData("{CompiledBinding Foo}", "Foo")]
    [InlineData("{ReflectionBinding Foo}", "Foo")]
    [InlineData("{ Binding Foo }", "Foo")]
    [InlineData("{Binding}", null)]
    [InlineData("{Binding Foo.Bar}", null)]
    [InlineData("{StaticResource Foo}", null)]
    [InlineData("Foo", null)]
    [InlineData(null, null)]
    public void GetBindingPath(string? value, string? expected) =>
        Assert.Equal(expected, MvvmBindingHeuristics.GetBindingPath(value));

    [Fact]
    public void MvvmScaffolder_ProducesSpecXamlShape()
    {
        var doc = Samples.Parse(Samples.MainWindow);
        MvvmScaffolder.Apply(doc, "MyApp.ViewModels", "MainWindowViewModel");
        Assert.Equal(CodeBehindStyle.MVVM, doc.CodeStyle);
        var xaml = Samples.Serialize(doc);
        Assert.Contains("xmlns:vm=\"clr-namespace:MyApp.ViewModels\"", xaml);
        Assert.Contains("x:DataType=\"vm:MainWindowViewModel\"", xaml);
        Assert.Contains("    <Window.DataContext>\n        <vm:MainWindowViewModel />\n    </Window.DataContext>", xaml);

        // Applying twice does not duplicate the DataContext.
        MvvmScaffolder.Apply(doc, "MyApp.ViewModels", "MainWindowViewModel");
        Assert.Equal(xaml, Samples.Serialize(doc));
        Assert.Equal(CodeBehindStyle.MVVM, Samples.Parse(xaml).CodeStyle);
    }
}
