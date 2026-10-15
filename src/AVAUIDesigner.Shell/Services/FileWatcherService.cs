using Avalonia.Threading;

namespace AVAUIDesigner.Shell.Services;

/// <summary>
/// Watches the open .axaml file and reports external modifications. Changes whose content equals the
/// last text read or written by the designer are ignored, so the designer's own saves never trigger it.
/// </summary>
public class FileWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private string? _path;
    private string? _knownText;
    private bool _pending;

    /// <summary>Raised on the UI thread with the changed file path.</summary>
    public event EventHandler<string>? ExternallyModified;

    public void Watch(string? path, string? knownText)
    {
        Stop();
        _path = path;
        _knownText = knownText;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(Path.GetFullPath(path))!, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnChanged;
    }

    /// <summary>Records the content the designer itself just wrote or loaded.</summary>
    public void SetKnownText(string text) => _knownText = text;

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (_pending) return;
        _pending = true;
        // Editors often write in several steps; coalesce and read once things settle.
        DispatcherTimer.RunOnce(() =>
        {
            _pending = false;
            if (_path is null) return;
            string text;
            try
            {
                text = File.ReadAllText(_path);
            }
            catch (IOException)
            {
                return;
            }
            if (text == _knownText) return;
            _knownText = text;
            ExternallyModified?.Invoke(this, _path);
        }, TimeSpan.FromMilliseconds(300));
    }

    public void Stop()
    {
        if (_watcher is null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    public void Dispose() => Stop();
}
