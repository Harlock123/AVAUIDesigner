using Avalonia;
using Avalonia.Styling;

namespace AVAUIDesigner.Shell.Services;

/// <summary>Applies the "System" / "FluentLight" / "FluentDark" theme setting.</summary>
public static class ThemeService
{
    public static void Apply(string? theme)
    {
        if (Application.Current is not { } app) return;
        app.RequestedThemeVariant = theme switch
        {
            "FluentLight" or "Light" => ThemeVariant.Light,
            "FluentDark" or "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
