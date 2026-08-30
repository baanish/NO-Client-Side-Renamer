using System;
using System.Collections.Generic;

namespace ClientSideRenamer.Aliases;

public sealed class AliasSnapshot
{
    private readonly IReadOnlyDictionary<string, string> _aliasesBySteamId;
    private readonly IReadOnlyDictionary<string, string> _aliasesBySteamName;

    internal AliasSnapshot(
        IReadOnlyDictionary<string, string> aliasesBySteamId,
        IReadOnlyDictionary<string, string> aliasesBySteamName)
    {
        _aliasesBySteamId = aliasesBySteamId;
        _aliasesBySteamName = aliasesBySteamName;
    }

    public static AliasSnapshot Empty { get; } = new(
        new Dictionary<string, string>(StringComparer.Ordinal),
        new Dictionary<string, string>(StringComparer.Ordinal));

    public int Count => _aliasesBySteamId.Count + _aliasesBySteamName.Count;

    public bool TryResolve(string steamId, string steamName, out string displayName)
    {
        if (!string.IsNullOrWhiteSpace(steamId)
            && _aliasesBySteamId.TryGetValue(steamId, out displayName))
        {
            return true;
        }

        if (steamName != null && _aliasesBySteamName.TryGetValue(steamName, out displayName))
        {
            return true;
        }

        displayName = string.Empty;
        return false;
    }
}
