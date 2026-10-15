using System.Text.Json;
using System.Text.Json.Serialization;
using AVAUIDesigner.Engine.CodeGeneration;

namespace AVAUIDesigner.Engine.Settings;

/// <summary>User preferences, persisted to {AppData}/AVAUIDesigner/settings.json.</summary>
public class DesignerSettings
{
    public UiSettings Ui { get; set; } = new();
    public DesignSettings Design { get; set; } = new();
    public CodeGenSettings CodeGen { get; set; } = new();

    public class UiSettings
    {
        /// <summary>"System", "FluentLight" or "FluentDark".</summary>
        public string Theme { get; set; } = "System";
        public PanelLayout PanelLayout { get; set; } = new();
        public List<string> ToolboxExpanded { get; set; } = ["Common Controls", "Containers"];
        public List<string> RecentFiles { get; set; } = [];
        public List<string> RecentProjects { get; set; } = [];
    }

    public class PanelLayout
    {
        public bool ShowToolbox { get; set; } = true;
        public bool ShowProperties { get; set; } = true;
        public bool ShowOutline { get; set; } = true;
        public bool ShowSource { get; set; } = true;
        public double ToolboxWidth { get; set; } = 200;
        public double RightPanelWidth { get; set; } = 280;
        public double SourceHeightRatio { get; set; } = 0.3;
        public double OutlineHeightRatio { get; set; } = 0.4;
        public double WindowWidth { get; set; } = 1400;
        public double WindowHeight { get; set; } = 900;
        public bool WindowMaximized { get; set; }
    }

    public class DesignSettings
    {
        public bool SnapToGrid { get; set; } = true;
        public int GridSize { get; set; } = 8;
        public int SnapThreshold { get; set; } = 6;
        public bool ShowGrid { get; set; } = true;
        public bool SmartSnapLines { get; set; } = true;
        public double DefaultZoom { get; set; } = 1.0;
        public bool AutoNameNewControls { get; set; } = true;
    }

    public class CodeGenSettings
    {
        /// <summary>"CodeBehind" or "MVVM": style of File &gt; New documents.</summary>
        public string DefaultStyle { get; set; } = "CodeBehind";
        public string MvvmToolkit { get; set; } = "CommunityToolkit";
        public bool GenerateNullableAnnotations { get; set; } = true;
        /// <summary>
        /// Emit explicit fields for named elements. Off by default because Avalonia's name generator
        /// already creates them; automatically enabled for projects that turn the generator off.
        /// </summary>
        public bool GenerateFieldDeclarations { get; set; }
        /// <summary>Wire events in a WireEvents() method instead of XAML attributes.</summary>
        public bool WireEventsInCode { get; set; }
        public string DefaultNamespace { get; set; } = "";

        [JsonIgnore]
        public CodeBehindStyle DefaultStyleValue =>
            string.Equals(DefaultStyle, "MVVM", StringComparison.OrdinalIgnoreCase) ? CodeBehindStyle.MVVM : CodeBehindStyle.CodeBehind;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
            "AVAUIDesigner");

    public static string DefaultPath => Path.Combine(DefaultDirectory, "settings.json");

    /// <summary>Loads settings; returns defaults if the file is missing or unreadable.</summary>
    public static DesignerSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<DesignerSettings>(File.ReadAllText(path), JsonOptions) ?? new DesignerSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Corrupt settings must never prevent startup.
        }
        return new DesignerSettings();
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    public void AddRecentFile(string path)
    {
        Ui.RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Ui.RecentFiles.Insert(0, path);
        if (Ui.RecentFiles.Count > 10) Ui.RecentFiles.RemoveRange(10, Ui.RecentFiles.Count - 10);
    }

    public DesignerSettings Clone() =>
        JsonSerializer.Deserialize<DesignerSettings>(JsonSerializer.Serialize(this, JsonOptions), JsonOptions)!;
}
