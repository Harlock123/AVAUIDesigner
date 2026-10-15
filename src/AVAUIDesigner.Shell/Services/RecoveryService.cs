using System.Text.Json;
using Avalonia.Threading;

namespace AVAUIDesigner.Shell.Services;

public record RecoveryInfo(string? OriginalPath, DateTime SavedAt, string Text);

/// <summary>Writes a crash-recovery copy of the current document every 60 seconds to {Temp}/AVAUIDesigner/.</summary>
public class RecoveryService : IDisposable
{
    private readonly string _directory;
    private readonly DispatcherTimer _timer;
    private Func<(string? Path, string Text, bool Dirty)?>? _snapshot;

    public RecoveryService(string? directory = null, TimeSpan? interval = null)
    {
        _directory = directory ?? Path.Combine(Path.GetTempPath(), "AVAUIDesigner");
        _timer = new DispatcherTimer { Interval = interval ?? TimeSpan.FromSeconds(60) };
        _timer.Tick += (_, _) => SaveNow();
    }

    private string TextPath => Path.Combine(_directory, "recovery.axaml");
    private string InfoPath => Path.Combine(_directory, "recovery.json");

    /// <summary>Starts auto-saving; <paramref name="snapshot"/> returns the current document state (or null).</summary>
    public void Start(Func<(string? Path, string Text, bool Dirty)?> snapshot)
    {
        _snapshot = snapshot;
        _timer.Start();
    }

    /// <summary>Writes the recovery copy now if the document has unsaved changes (also used on unhandled exceptions).</summary>
    public void SaveNow()
    {
        try
        {
            var state = _snapshot?.Invoke();
            if (state is not { Dirty: true } s)
            {
                Clear();
                return;
            }
            Directory.CreateDirectory(_directory);
            File.WriteAllText(TextPath, s.Text);
            File.WriteAllText(InfoPath, JsonSerializer.Serialize(new { s.Path, SavedAt = DateTime.Now }));
        }
        catch (Exception)
        {
            // Recovery must never take the app down.
        }
    }

    public RecoveryInfo? TryGetRecovery()
    {
        try
        {
            if (!File.Exists(TextPath)) return null;
            string? path = null;
            var savedAt = File.GetLastWriteTime(TextPath);
            if (File.Exists(InfoPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(InfoPath));
                if (doc.RootElement.TryGetProperty("Path", out var p) && p.ValueKind == JsonValueKind.String) path = p.GetString();
                if (doc.RootElement.TryGetProperty("SavedAt", out var t) && t.TryGetDateTime(out var dt)) savedAt = dt;
            }
            return new RecoveryInfo(path, savedAt, File.ReadAllText(TextPath));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(TextPath)) File.Delete(TextPath);
            if (File.Exists(InfoPath)) File.Delete(InfoPath);
        }
        catch (Exception)
        {
            // ignore
        }
    }

    public void Dispose() => _timer.Stop();
}
