namespace ClientSideRenamer;

internal sealed class ReloadWatcher : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);

    private readonly object _gate = new();
    private FileSystemWatcher _watcher;
    private DateTime _reloadAfterUtc;
    private bool _reloadPending;

    public void Bind(string filePath, bool enabled)
    {
        DisposeWatcher();
        ClearPending();

        if (!enabled)
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            return;
        }

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };

        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.EnableRaisingEvents = true;
    }

    public bool Poll()
    {
        lock (_gate)
        {
            if (!_reloadPending || DateTime.UtcNow < _reloadAfterUtc)
            {
                return false;
            }

            _reloadPending = false;
            return true;
        }
    }

    public void Dispose()
    {
        DisposeWatcher();
        ClearPending();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        ScheduleReload();
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        ScheduleReload();
    }

    private void ScheduleReload()
    {
        lock (_gate)
        {
            _reloadAfterUtc = DateTime.UtcNow + Debounce;
            _reloadPending = true;
        }
    }

    private void ClearPending()
    {
        lock (_gate)
        {
            _reloadPending = false;
        }
    }

    private void DisposeWatcher()
    {
        if (_watcher == null)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnChanged;
        _watcher.Created -= OnChanged;
        _watcher.Deleted -= OnChanged;
        _watcher.Renamed -= OnRenamed;
        _watcher.Dispose();
        _watcher = null;
    }
}
