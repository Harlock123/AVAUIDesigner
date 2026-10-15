using System.Xml.Linq;

namespace AVAUIDesigner.Engine.Project;

/// <summary>Information about the .csproj an edited file belongs to (or a standalone context).</summary>
public class ProjectContext
{
    private static readonly string[] ExcludedDirs = ["bin", "obj", ".git", ".vs", ".idea", "node_modules"];

    private ProjectContext(string? projectFilePath)
    {
        ProjectFilePath = projectFilePath;
    }

    /// <summary>Path to the .csproj, or null if standalone.</summary>
    public string? ProjectFilePath { get; }
    public string? ProjectDirectory => ProjectFilePath is null ? null : Path.GetDirectoryName(ProjectFilePath);
    public string? RootNamespace { get; private set; }
    public string? AssemblyName { get; private set; }
    public string? TargetFramework { get; private set; }

    /// <summary>False when the project sets &lt;AvaloniaNameGeneratorIsEnabled&gt;false.</summary>
    public bool NameGeneratorEnabled { get; private set; } = true;

    /// <summary>Whether CommunityToolkit.Mvvm is referenced.</summary>
    public bool ReferencesMvvmToolkit { get; private set; }

    public List<string> AxamlFiles { get; } = [];
    public List<string> CsFiles { get; } = [];

    public bool IsStandalone => ProjectFilePath is null;

    public static ProjectContext Standalone() => new(null);

    public static async Task<ProjectContext> LoadFromCsprojAsync(string csprojPath)
    {
        var fullPath = Path.GetFullPath(csprojPath);
        var ctx = new ProjectContext(fullPath);
        var text = await File.ReadAllTextAsync(fullPath);
        var xdoc = XDocument.Parse(text);

        string? Prop(string name) =>
            xdoc.Descendants().FirstOrDefault(e => e.Name.LocalName == name && !string.IsNullOrWhiteSpace(e.Value))?.Value.Trim();

        var stem = Path.GetFileNameWithoutExtension(fullPath);
        ctx.AssemblyName = Prop("AssemblyName") ?? stem;
        ctx.RootNamespace = Prop("RootNamespace") ?? ctx.AssemblyName;
        ctx.TargetFramework = Prop("TargetFramework") ?? Prop("TargetFrameworks")?.Split(';')[0];
        ctx.NameGeneratorEnabled = !string.Equals(Prop("AvaloniaNameGeneratorIsEnabled"), "false", StringComparison.OrdinalIgnoreCase);
        ctx.ReferencesMvvmToolkit = xdoc.Descendants().Any(e => e.Name.LocalName == "PackageReference"
                                                               && (string?)e.Attribute("Include") == "CommunityToolkit.Mvvm");
        ctx.ScanFiles();
        return ctx;
    }

    /// <summary>Walks up from <paramref name="filePath"/> to find the nearest .csproj.</summary>
    public static string? FindProjectFile(string filePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        while (dir is not null)
        {
            var proj = Directory.EnumerateFiles(dir, "*.csproj").FirstOrDefault();
            if (proj is not null) return proj;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    public void ScanFiles()
    {
        AxamlFiles.Clear();
        CsFiles.Clear();
        if (ProjectDirectory is null) return;
        foreach (var file in EnumerateFiles(ProjectDirectory))
        {
            if (file.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)) AxamlFiles.Add(file);
            else if (file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) CsFiles.Add(file);
        }
        AxamlFiles.Sort(StringComparer.OrdinalIgnoreCase);
        CsFiles.Sort(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            IEnumerable<string> files, dirs;
            try
            {
                files = Directory.EnumerateFiles(dir);
                dirs = Directory.EnumerateDirectories(dir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            foreach (var f in files) yield return f;
            foreach (var d in dirs)
                if (!ExcludedDirs.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)) pending.Push(d);
        }
    }

    /// <summary>
    /// The default x:Class for a file: RootNamespace + relative folder path + file name.
    /// "MyApp" + "Views/MainWindow.axaml" → "MyApp.Views.MainWindow".
    /// </summary>
    public string GetDefaultXClass(string axamlPath)
    {
        var className = CodeGeneration.CodeIdentifiers.MakeIdentifier(
            Path.GetFileNameWithoutExtension(axamlPath).Replace(".axaml", "", StringComparison.OrdinalIgnoreCase));
        return $"{GetNamespaceForFile(axamlPath)}.{className}";
    }

    /// <summary>The namespace implied by a file's folder within the project.</summary>
    public string GetNamespaceForFile(string path)
    {
        var rootNs = RootNamespace ?? "MyApp";
        if (ProjectDirectory is null) return rootNs;
        var relDir = Path.GetRelativePath(ProjectDirectory, Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (relDir == "." || relDir.StartsWith("..", StringComparison.Ordinal)) return rootNs;
        var parts = relDir.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Select(CodeGeneration.CodeIdentifiers.MakeIdentifier);
        return string.Join(".", new[] { rootNs }.Concat(parts));
    }

    /// <summary>Where the ViewModel for a view lives: a sibling "ViewModels" folder of a "Views" folder, else the project's ViewModels folder.</summary>
    public string GetViewModelPath(string axamlPath, string viewModelClassName)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(axamlPath))!;
        string vmDir;
        if (string.Equals(Path.GetFileName(dir), "Views", StringComparison.OrdinalIgnoreCase))
            vmDir = Path.Combine(Path.GetDirectoryName(dir)!, "ViewModels");
        else if (ProjectDirectory is not null)
            vmDir = Path.Combine(ProjectDirectory, "ViewModels");
        else
            vmDir = dir;
        return Path.Combine(vmDir, viewModelClassName + ".cs");
    }
}
