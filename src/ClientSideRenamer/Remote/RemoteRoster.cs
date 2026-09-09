using System.Threading;
using System.Threading.Tasks;
using ClientSideRenamer.Aliases;

namespace ClientSideRenamer.Remote;

// Only the Unity thread reads or changes this state. Workers return detached snapshots.
internal sealed class RemoteRoster : IDisposable
{
    private CancellationTokenSource _cancellation;
    private Task<(AliasSnapshot Snapshot, string FailureType)> _pending;

    internal AliasSnapshot Current { get; private set; } = AliasSnapshot.Empty;
    internal string Status { get; private set; } = "Remote CSV is disabled.";

    internal void Refresh(Func<CancellationToken, Task<AliasSnapshot>> fetch)
    {
        CancelPending();
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        Status = "Fetching remote CSV...";
        _pending = Task.Run(async () =>
        {
            try
            {
                return (await fetch(token).ConfigureAwait(false), (string)null);
            }
            catch (Exception exception)
            {
                // Exceptions can contain credentials, URLs or server-controlled text.
                return (null, exception.GetType().Name);
            }
        });
    }

    internal bool Poll()
    {
        if (_pending == null || !_pending.IsCompleted)
        {
            return false;
        }

        var result = _pending.GetAwaiter().GetResult();
        _pending = null;
        _cancellation.Dispose();
        _cancellation = null;
        if (result.Snapshot == null)
        {
            Status = $"Remote CSV fetch failed ({result.FailureType}); "
                + "check HTTPS endpoint, authentication and CSV format. "
                + $"Retained {Current.Count} remote aliases.";
        }
        else
        {
            Current = result.Snapshot;
            Status = $"Loaded {Current.Count} remote aliases.";
        }

        return true;
    }

    internal void Clear()
    {
        CancelPending();
        Current = AliasSnapshot.Empty;
        Status = "Remote CSV is disabled or awaiting configuration.";
    }

    public void Dispose() => Clear();

    private void CancelPending()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        // A canceled request may finish late. Only the current task can publish a snapshot.
        _pending = null;
    }
}
