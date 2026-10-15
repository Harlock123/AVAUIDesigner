using System.Diagnostics;
using Avalonia.Headless.XUnit;
using AVAUIDesigner.Engine.UndoRedo.Commands;
using AVAUIDesigner.Shell.Services;
using Xunit;
using Xunit.Abstractions;

namespace AVAUIDesigner.Shell.Tests;

/// <summary>
/// Saves designer output into real Avalonia projects and compiles them with `dotnet build`.
/// Slow (two builds), so it only runs when AVAUIDESIGNER_BUILD_TEST=1.
/// </summary>
public class GeneratedProjectBuildTests(ITestOutputHelper output)
{
    private static bool Enabled => Environment.GetEnvironmentVariable("AVAUIDESIGNER_BUILD_TEST") == "1";

    private const string Csproj = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>DemoApp</RootNamespace>
            <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
            <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
            {EXTRA}
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Avalonia" Version="11.3.22" />
            <PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.22" />
            <PackageReference Include="Avalonia.Controls.DataGrid" Version="11.3.13" />
            <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
          </ItemGroup>
        </Project>
        """;

    [AvaloniaFact]
    public async Task DefaultSettings_ProjectBuilds() => await BuildProject("default", string.Empty, fields: false, wireInCode: false);

    [AvaloniaFact]
    public async Task NameGeneratorOff_FieldsAndWireEvents_ProjectBuilds() =>
        await BuildProject("fields", "<AvaloniaNameGeneratorIsEnabled>false</AvaloniaNameGeneratorIsEnabled>", fields: true, wireInCode: true);

    private async Task BuildProject(string name, string extra, bool fields, bool wireInCode)
    {
        if (!Enabled) return;
        var dir = Path.Combine(TestHelpers.ScratchDir, "build-" + name);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(Path.Combine(dir, "Views"));
        var csproj = Path.Combine(dir, "DemoApp.csproj");
        await File.WriteAllTextAsync(csproj, Csproj.Replace("{EXTRA}", extra));
        // Keep the generated project out of the designer's central package management.
        await File.WriteAllTextAsync(Path.Combine(dir, "Directory.Packages.props"), "<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(dir, "Directory.Build.props"), "<Project />");

        var settings = TestHelpers.Settings();
        settings.CodeGen.WireEventsInCode = wireInCode;
        var vm = TestHelpers.CreateViewModel(settings);
        vm.Clipboard = new InMemoryClipboard();
        await vm.OpenProjectAsync(csproj);
        Assert.Equal(!fields, vm.ProjectContext!.NameGeneratorEnabled);

        // 1) Code-behind window with every kind of control and some events.
        await vm.NewDocumentAsync(NewDocumentKind.Window, mvvm: false, prompt: false);
        foreach (var item in vm.Toolbox.Categories.SelectMany(c => c.Items))
        {
            vm.SelectionManager.SetSelection(vm.CurrentDocument!.RootElement.Children[0]);
            vm.AddControl(item);
        }
        var doc = vm.CurrentDocument!;
        foreach (var e in doc.AllElements.Where(e => e.Name is not null && e.Descriptor?.DefaultEvent is not null).ToList())
            vm.CreateEventHandler(e, e.Descriptor!.DefaultEvent!);
        vm.CreateEventHandler(doc.RootElement, "Opened");
        Assert.True(await vm.SaveToAsync(Path.Combine(dir, "Views", "MainWindow.axaml")));

        // 2) MVVM user control with bindings.
        await vm.NewDocumentAsync(NewDocumentKind.UserControl, mvvm: true, prompt: false);
        foreach (var type in new[] { "TextBox", "Button", "CheckBox", "Slider", "ProgressBar", "ComboBox" })
        {
            vm.SelectionManager.SetSelection(vm.CurrentDocument!.RootElement.Children[0]);
            vm.AddControl(vm.Toolbox.Categories.SelectMany(c => c.Items).First(i => i.DisplayName == type));
        }
        var button = vm.CurrentDocument!.NamedElements["button1"];
        vm.Execute(new SetEventHandlerCommand(button, "Click", "Button1_Click"));
        Assert.True(await vm.SaveToAsync(Path.Combine(dir, "Views", "EditorView.axaml")));

        var psi = new ProcessStartInfo("dotnet", $"build \"{csproj}\" -nologo -v:q")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        output.WriteLine(stdout);
        output.WriteLine(stderr);
        Assert.True(process.ExitCode == 0, $"Build failed:\n{stdout}\n{stderr}");
    }
}
