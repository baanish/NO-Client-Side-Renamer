using HarmonyLib;
using NuclearOption.Networking;

namespace ClientSideRenamer;

internal static class PlayerNameCache
{
    private static readonly AccessTools.FieldRef<Player, PlayerName> PlayerNameCacheRef =
        AccessTools.FieldRefAccess<Player, PlayerName>("_playerNameCache");

    private static readonly AccessTools.FieldRef<Player, float> LastRequestedNameTimeRef =
        AccessTools.FieldRefAccess<Player, float>("_lastRequestedNameTime");

    public static void RefreshAll()
    {
        var players = UnitRegistry.playerLookup.Values
            .Where(player => player != null)
            .ToArray();

        UnitRegistry.cachedPlayerNames.Clear();

        foreach (var player in players)
        {
            PlayerNameCacheRef(player) = null;
            LastRequestedNameTimeRef(player) = float.NegativeInfinity;
            player.Steam_OnPersonaStateChanged();
        }
    }
}
