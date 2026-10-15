using AVAUIDesigner.Engine.CodeGeneration;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Project;
using AVAUIDesigner.Engine.Settings;

namespace AVAUIDesigner.Engine.Tests;

public class HandlerNamingTests
{
    [Fact]
    public void NamedElement_UsesPascalCasedName()
    {
        var doc = Samples.Parse(Samples.MainWindow);
        var btn = doc.NamedElements["btnOk"];
        Assert.Equal("BtnOk_Click", HandlerNaming.Suggest(btn, "Click", doc));
        Assert.Equal("TxtName_TextChanged", HandlerNaming.Suggest(doc.NamedElements["txtName"], "TextChanged", doc));
        Assert.Equal("BtnOk_PointerPressed", HandlerNaming.Suggest(btn, "PointerPressed", null));
    }

    [Fact]
    public void UnnamedElement_UsesTypeAndIndex()
    {
        var doc = Samples.ParseInWindow("<StackPanel><Button /><TextBox /><Button /></StackPanel>");
        var panel = doc.RootElement.Children[0];
        Assert.Equal("Button1_Click", HandlerNaming.Suggest(panel.Children[0], "Click", doc));
        Assert.Equal("Button2_Click", HandlerNaming.Suggest(panel.Children[2], "Click", doc));
        Assert.Equal("TextBox1_TextChanged", HandlerNaming.Suggest(panel.Children[1], "TextChanged", doc));
        Assert.Equal("Button1_Click", HandlerNaming.Suggest(new DesignElement("Button"), "Click", null));
    }

    [Fact]
    public void Suggestion_IsUniqueAmongOtherElementsHandlers()
    {
        var doc = Samples.ParseInWindow("""
            <StackPanel>
                <Button x:Name="btnOk" />
                <Button x:Name="other" Click="BtnOk_Click" />
                <Button x:Name="third" Click="BtnOk_Click2" />
            </StackPanel>
            """);
        Assert.Equal("BtnOk_Click3", HandlerNaming.Suggest(doc.NamedElements["btnOk"], "Click", doc));
    }

    [Fact]
    public void Suggestion_KeepsHandlerAlreadyWiredToSameElement()
    {
        var doc = Samples.Parse(Samples.MainWindow);
        Assert.Equal("BtnOk_Click", HandlerNaming.Suggest(doc.NamedElements["btnOk"], "Click", doc));
    }

    [Fact]
    public void PrefixedAndOddNames_ProduceValidIdentifiers()
    {
        var doc = Samples.ParseInWindow("""<StackPanel xmlns:local="clr-namespace:A"><local:Gauge /></StackPanel>""");
        var suggestion = HandlerNaming.Suggest(doc.RootElement.Children[0].Children[0], "Click", doc);
        Assert.Equal("Gauge1_Click", suggestion);
        Assert.True(CodeIdentifiers.IsValidIdentifier(HandlerNaming.Suggest(new DesignElement("Button") { Name = "my-btn" }, "Click", null)));
    }
}

public class CodeIdentifiersTests
{
    [Theory]
    [InlineData("abc", true)]
    [InlineData("_a1", true)]
    [InlineData("BtnOk_Click", true)]
    [InlineData("1abc", false)]
    [InlineData("class", false)]
    [InlineData("a b", false)]
    [InlineData("a-b", false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData(null, false)]
    public void IsValidIdentifier(string? name, bool expected) => Assert.Equal(expected, CodeIdentifiers.IsValidIdentifier(name));

    [Theory]
    [InlineData("my-window", "my_window")]
    [InlineData("1st", "_1st")]
    [InlineData("class", "@class")]
    [InlineData("", "_")]
    [InlineData("Main Window", "Main_Window")]
    [InlineData("Ok", "Ok")]
    public void MakeIdentifier(string text, string expected) => Assert.Equal(expected, CodeIdentifiers.MakeIdentifier(text));

    [Theory]
    [InlineData("btnOk", "BtnOk")]
    [InlineData("_txtName", "TxtName")]
    [InlineData("X", "X")]
    [InlineData("", "")]
    [InlineData("___", "___")]
    public void ToPascalCase(string input, string expected) => Assert.Equal(expected, CodeIdentifiers.ToPascalCase(input));

    [Theory]
    [InlineData("TxtName", "txtName")]
    [InlineData("x", "x")]
    [InlineData("", "")]
    public void ToCamelCase(string input, string expected) => Assert.Equal(expected, CodeIdentifiers.ToCamelCase(input));

    [Fact]
    public void BareTypeName_StripsPrefix()
    {
        Assert.Equal("MyControl", CodeIdentifiers.BareTypeName("local:MyControl"));
        Assert.Equal("Button", CodeIdentifiers.BareTypeName("Button"));
    }

    [Fact]
    public void Reindent_NormalizesLeadingWhitespaceAndTrims()
    {
        var code = "\n\n            a();\n              if (x)\n\n            b();   \n\n";
        Assert.Equal("    a();\n      if (x)\n\n    b();", CodeIdentifiers.Reindent(code, "    "));
        Assert.Equal(string.Empty, CodeIdentifiers.Reindent("  \n \n", "    "));
        Assert.Equal("  x", CodeIdentifiers.Reindent("\r\n\tx\r\n", "  "));
    }
}

public class DesignDocumentTests
{
    [Fact]
    public void GenerateUniqueName_CountsUpFromOne()
    {
        var doc = Samples.ParseInWindow("""<StackPanel><Button x:Name="button1" /><Button x:Name="button3" /></StackPanel>""");
        Assert.Equal("button2", doc.GenerateUniqueName("Button"));
        Assert.Equal("textBox1", doc.GenerateUniqueName("TextBox"));
        Assert.Equal("myControl1", doc.GenerateUniqueName("local:MyControl"));
    }

    [Fact]
    public void MakeUniqueName_AppendsNumberOnlyWhenTaken()
    {
        var doc = Samples.Parse(Samples.MainWindow);
        Assert.Equal("fresh", doc.MakeUniqueName("fresh"));
        Assert.Equal("btnOk1", doc.MakeUniqueName("btnOk"));
        var btn = doc.NamedElements["btnOk"];
        Assert.Equal("btnOk", doc.MakeUniqueName("btnOk", ignore: btn));

        btn.Name = "button1";
        Assert.Equal("button2", doc.MakeUniqueName("button1"));
        doc.NamedElements["txtName"].Name = "7";
        Assert.Equal("71", doc.MakeUniqueName("7"));
    }

    [Fact]
    public void NamedElements_IncludesPropertyElementContents()
    {
        var doc = Samples.ParseInWindow("""
            <Button x:Name="b"><Button.ContextMenu><ContextMenu x:Name="menu" /></Button.ContextMenu></Button>
            """);
        Assert.True(doc.NamedElements.ContainsKey("menu"));
        Assert.Equal(3, doc.AllElements.Count());
    }

    [Fact]
    public void ClassNameAndNamespace_DeriveFromXClassOrFileName()
    {
        var doc = Samples.Parse(Samples.MainWindow);
        Assert.Equal("MainWindow", doc.ClassName);
        Assert.Equal("MyApp.Views", doc.ClassNamespace);

        doc.XClass = null;
        doc.FilePath = "/tmp/Settings-Dialog.axaml";
        Assert.Equal("Settings_Dialog", doc.ClassName);
        Assert.Null(doc.ClassNamespace);
        Assert.Equal("Settings-Dialog.axaml", doc.FileName);

        doc.FilePath = "";
        Assert.Equal("Untitled.axaml", doc.FileName);
        Assert.Equal("Untitled", doc.ClassName);
    }

    [Fact]
    public void RootElementType_DelegatesToRoot()
    {
        var doc = Samples.Parse(Samples.MainWindow);
        Assert.Equal("Window", doc.RootElementType);
        var uc = new DesignElement("UserControl");
        doc.RootElement = uc;
        Assert.Equal("UserControl", doc.RootElementType);
        Assert.Null(uc.Parent);
    }

    [Fact]
    public void SetNamespace_ReplacesExistingPrefix()
    {
        var doc = new DesignDocument(new DesignElement("Window"));
        Assert.Equal(["", "x", "d", "mc"], doc.Namespaces.Select(n => n.Prefix));
        doc.SetNamespace("vm", "clr-namespace:A");
        doc.SetNamespace("vm", "clr-namespace:B");
        Assert.Single(doc.Namespaces, n => n.Prefix == "vm");
        Assert.Equal("clr-namespace:B", doc.GetNamespaceUri("vm"));
    }

    [Fact]
    public void DeepClone_CopiesEverythingDetached()
    {
        var doc = Samples.Parse(Samples.MainWindow);
        var grid = doc.RootElement.Children[0];
        var copy = grid.DeepClone();
        Assert.Null(copy.Parent);
        DocumentAssert.Equal(grid, copy);
        Assert.NotSame(grid.Children[0], copy.Children[0]);
        var rows = copy.GetPropertyElement("RowDefinitions")!;
        Assert.All(rows.ElementValues, r => Assert.Same(copy, r.Parent));
        copy.Children[0].SetValue("Text", "changed");
        Assert.Equal("Welcome", grid.Children[0].GetValue("Text"));
    }

    [Fact]
    public void SetValue_AttributeReplacesElementForm()
    {
        var doc = Samples.Parse(Samples.MainWindow);
        var grid = doc.RootElement.Children[0];
        grid.SetValue("RowDefinitions", "Auto,*");
        Assert.Null(grid.GetPropertyElement("RowDefinitions"));
        Assert.Equal("Auto,*", grid.GetValue("RowDefinitions"));
        Assert.Equal("Auto,*", grid.GetValue("Grid.RowDefinitions")); // owner-qualified form normalized
    }

    [Fact]
    public void TypedPropertyAccess_UsesDescriptorTypes()
    {
        var doc = Samples.Parse(Samples.MainWindow);
        var btn = doc.NamedElements["btnOk"];
        Assert.Equal(75d, btn.GetProperty("Width"));
        Assert.Equal(new Avalonia.Thickness(16), btn.GetProperty("Margin"));
        Assert.Equal(Avalonia.Layout.HorizontalAlignment.Right, btn.GetProperty("HorizontalAlignment"));
        Assert.Equal(2, btn.GetProperty("Grid.Row"));
        btn.SetProperty("Margin", new Avalonia.Thickness(1, 2, 3, 4));
        Assert.Equal("1,2,3,4", btn.GetValue("Margin"));
        btn.SetProperty("Margin", null);
        Assert.Null(btn.GetValue("Margin"));
        btn.Width = double.NaN;
        Assert.Null(btn.GetValue("Width"));
    }
}

public sealed class ProjectContextTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("avaui-proj-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private string Write(string relative, string content)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private const string Csproj = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <RootNamespace>MyApp</RootNamespace>
            <AssemblyName>MyApp.Desktop</AssemblyName>
            <AvaloniaNameGeneratorIsEnabled>false</AvaloniaNameGeneratorIsEnabled>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public async Task LoadFromCsproj_ReadsPropertiesAndFiles()
    {
        var proj = Write("MyApp.csproj", Csproj);
        var view = Write("Views/MainWindow.axaml", Samples.MainWindow);
        Write("Views/MainWindow.axaml.cs", "// cs");
        Write("App.axaml", "<Application />");
        Write("obj/Debug/Generated.axaml", "<x />");
        Write("bin/Debug/Copied.cs", "// x");

        var ctx = await ProjectContext.LoadFromCsprojAsync(proj);
        Assert.False(ctx.IsStandalone);
        Assert.Equal(Path.GetFullPath(proj), ctx.ProjectFilePath);
        Assert.Equal("MyApp", ctx.RootNamespace);
        Assert.Equal("MyApp.Desktop", ctx.AssemblyName);
        Assert.Equal("net10.0", ctx.TargetFramework);
        Assert.False(ctx.NameGeneratorEnabled);
        Assert.True(ctx.ReferencesMvvmToolkit);

        Assert.Equal("MyApp.Views.MainWindow", ctx.GetDefaultXClass(view));
        Assert.Equal("MyApp.App", ctx.GetDefaultXClass(Path.Combine(_dir, "App.axaml")));
        Assert.Equal("MyApp.Views.Dialogs", ctx.GetNamespaceForFile(Path.Combine(_dir, "Views", "Dialogs", "X.axaml")));

        Assert.Equal(2, ctx.AxamlFiles.Count);
        Assert.Contains(Path.GetFullPath(view), ctx.AxamlFiles);
        Assert.DoesNotContain(ctx.AxamlFiles, f => f.Contains("obj"));
        Assert.Single(ctx.CsFiles);
    }

    [Fact]
    public async Task LoadFromCsproj_DefaultsFromFileName()
    {
        var proj = Write("Cool.App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFrameworks>net10.0;net9.0</TargetFrameworks></PropertyGroup></Project>
            """);
        var ctx = await ProjectContext.LoadFromCsprojAsync(proj);
        Assert.Equal("Cool.App", ctx.AssemblyName);
        Assert.Equal("Cool.App", ctx.RootNamespace);
        Assert.Equal("net10.0", ctx.TargetFramework);
        Assert.True(ctx.NameGeneratorEnabled);
        Assert.False(ctx.ReferencesMvvmToolkit);
        Assert.Equal("Cool.App.Views.MainWindow", ctx.GetDefaultXClass(Path.Combine(_dir, "Views", "MainWindow.axaml")));
    }

    [Fact]
    public async Task NameGenerator_TrueOrCaseVariants()
    {
        var proj = Write("A.csproj", "<Project><PropertyGroup><AvaloniaNameGeneratorIsEnabled>FALSE</AvaloniaNameGeneratorIsEnabled></PropertyGroup></Project>");
        Assert.False((await ProjectContext.LoadFromCsprojAsync(proj)).NameGeneratorEnabled);
        File.WriteAllText(proj, "<Project><PropertyGroup><AvaloniaNameGeneratorIsEnabled>true</AvaloniaNameGeneratorIsEnabled></PropertyGroup></Project>");
        Assert.True((await ProjectContext.LoadFromCsprojAsync(proj)).NameGeneratorEnabled);
    }

    [Fact]
    public async Task GetViewModelPath_UsesSiblingViewModelsFolder()
    {
        var proj = Write("MyApp.csproj", Csproj);
        var ctx = await ProjectContext.LoadFromCsprojAsync(proj);
        Assert.Equal(Path.Combine(_dir, "ViewModels", "MainWindowViewModel.cs"),
            ctx.GetViewModelPath(Path.Combine(_dir, "Views", "MainWindow.axaml"), "MainWindowViewModel"));
        Assert.Equal(Path.Combine(_dir, "Features", "ViewModels", "XViewModel.cs"),
            ctx.GetViewModelPath(Path.Combine(_dir, "Features", "Views", "X.axaml"), "XViewModel"));
        // Not in a Views folder: the project's ViewModels folder.
        Assert.Equal(Path.Combine(_dir, "ViewModels", "AboutViewModel.cs"),
            ctx.GetViewModelPath(Path.Combine(_dir, "Dialogs", "About.axaml"), "AboutViewModel"));
    }

    [Fact]
    public void Standalone_Context()
    {
        var ctx = ProjectContext.Standalone();
        Assert.True(ctx.IsStandalone);
        Assert.Null(ctx.ProjectDirectory);
        Assert.Equal("MyApp", ctx.GetNamespaceForFile(Path.Combine(_dir, "X.axaml")));
        Assert.Equal("MyApp.X", ctx.GetDefaultXClass(Path.Combine(_dir, "X.axaml")));
        Assert.Equal(Path.Combine(_dir, "XViewModel.cs"), ctx.GetViewModelPath(Path.Combine(_dir, "X.axaml"), "XViewModel"));
        Assert.Equal(Path.Combine(_dir, "ViewModels", "XViewModel.cs"), ctx.GetViewModelPath(Path.Combine(_dir, "Views", "X.axaml"), "XViewModel"));
        ctx.ScanFiles();
        Assert.Empty(ctx.AxamlFiles);
    }

    [Fact]
    public void FindProjectFile_WalksUp()
    {
        var proj = Write("MyApp.csproj", Csproj);
        var view = Write("Views/Deep/Thing.axaml", "<x/>");
        Assert.Equal(proj, ProjectContext.FindProjectFile(view));
    }
}

public sealed class DesignerSettingsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("avaui-settings-").FullName;
    private string PathIn(string name) => Path.Combine(_dir, name);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var s = new DesignerSettings();
        s.Ui.Theme = "FluentDark";
        s.Ui.PanelLayout.ToolboxWidth = 321.5;
        s.Ui.PanelLayout.WindowMaximized = true;
        s.Ui.RecentFiles.Add("/a/b.axaml");
        s.Design.GridSize = 16;
        s.Design.SnapToGrid = false;
        s.CodeGen.DefaultStyle = "MVVM";
        s.CodeGen.WireEventsInCode = true;
        s.CodeGen.GenerateFieldDeclarations = true;
        s.CodeGen.DefaultNamespace = "Acme";

        var path = PathIn("nested/dir/settings.json"); // directory is created on save
        s.Save(path);
        var loaded = DesignerSettings.Load(path);

        Assert.Equal("FluentDark", loaded.Ui.Theme);
        Assert.Equal(321.5, loaded.Ui.PanelLayout.ToolboxWidth);
        Assert.True(loaded.Ui.PanelLayout.WindowMaximized);
        Assert.Equal(["/a/b.axaml"], loaded.Ui.RecentFiles);
        Assert.Equal(16, loaded.Design.GridSize);
        Assert.False(loaded.Design.SnapToGrid);
        Assert.Equal(CodeBehindStyle.MVVM, loaded.CodeGen.DefaultStyleValue);
        Assert.True(loaded.CodeGen.WireEventsInCode);
        Assert.True(loaded.CodeGen.GenerateFieldDeclarations);
        Assert.Equal("Acme", loaded.CodeGen.DefaultNamespace);

        var json = File.ReadAllText(path);
        Assert.Contains("\"ui\"", json); // camelCase
        Assert.DoesNotContain("defaultStyleValue", json);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        var path = PathIn("settings.json");
        File.WriteAllText(path, "{ this is not json ");
        var s = DesignerSettings.Load(path);
        Assert.Equal("System", s.Ui.Theme);
        Assert.Equal(8, s.Design.GridSize);
        Assert.Equal(CodeBehindStyle.CodeBehind, s.CodeGen.DefaultStyleValue);
    }

    [Fact]
    public void Load_MissingFileOrNullJson_ReturnsDefaults()
    {
        Assert.Equal("System", DesignerSettings.Load(PathIn("missing.json")).Ui.Theme);
        File.WriteAllText(PathIn("null.json"), "null");
        Assert.NotNull(DesignerSettings.Load(PathIn("null.json")).Ui);
    }

    [Fact]
    public void Load_PartialFile_KeepsDefaultsForMissingSections_AndToleratesComments()
    {
        File.WriteAllText(PathIn("p.json"), "{\n // comment\n \"Design\": { \"GridSize\": 4, },\n}");
        var s = DesignerSettings.Load(PathIn("p.json"));
        Assert.Equal(4, s.Design.GridSize);
        Assert.True(s.Design.ShowGrid);
        Assert.Equal("System", s.Ui.Theme);
    }

    [Fact]
    public void Defaults_MatchSpec()
    {
        var s = new DesignerSettings();
        Assert.False(s.CodeGen.GenerateFieldDeclarations);
        Assert.False(s.CodeGen.WireEventsInCode);
        Assert.True(s.CodeGen.GenerateNullableAnnotations);
        Assert.Equal(["Common Controls", "Containers"], s.Ui.ToolboxExpanded);
    }

    [Fact]
    public void AddRecentFile_MovesToFrontDedupesAndCaps()
    {
        var s = new DesignerSettings();
        for (var i = 0; i < 12; i++) s.AddRecentFile($"/f{i}.axaml");
        Assert.Equal(10, s.Ui.RecentFiles.Count);
        Assert.Equal("/f11.axaml", s.Ui.RecentFiles[0]);
        s.AddRecentFile("/F5.AXAML");
        Assert.Equal("/F5.AXAML", s.Ui.RecentFiles[0]);
        Assert.Equal(10, s.Ui.RecentFiles.Count);
        Assert.Single(s.Ui.RecentFiles, f => f.Equals("/f5.axaml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Clone_IsDeep()
    {
        var s = new DesignerSettings();
        s.Ui.RecentFiles.Add("/x");
        var c = s.Clone();
        c.Ui.RecentFiles.Add("/y");
        c.Design.GridSize = 99;
        Assert.Single(s.Ui.RecentFiles);
        Assert.Equal(8, s.Design.GridSize);
    }
}
