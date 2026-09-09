namespace ClientSideRenamer.Remote;

internal sealed class RemoteRosterSet : IDisposable
{
    private readonly RemoteRoster[] _rosters;

    internal RemoteRosterSet(int count)
    {
        if (count < 1 || count > PluginConfig.MaximumRemoteRosters)
            throw new ArgumentOutOfRangeException(nameof(count));

        _rosters = new RemoteRoster[count];
        for (var index = 0; index < count; index++)
            _rosters[index] = new RemoteRoster();
    }

    internal int Count => _rosters.Length;
    internal RemoteRoster this[int index] => _rosters[index];

    internal bool TryResolve(string steamId, out string displayName)
    {
        // Configured slot order decides conflicts, regardless of request completion order.
        foreach (var roster in _rosters)
        {
            if (roster.Current.TryResolve(steamId, null, out displayName))
                return true;
        }

        displayName = string.Empty;
        return false;
    }

    public void Dispose()
    {
        foreach (var roster in _rosters)
            roster.Dispose();
    }
}
