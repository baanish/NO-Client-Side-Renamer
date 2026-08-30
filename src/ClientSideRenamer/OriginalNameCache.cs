using Steamworks;

namespace ClientSideRenamer;

internal static class OriginalNameCache
{
    private static readonly Dictionary<ulong, string> Names = new();

    public static void Remember(CSteamID steamId, string name)
    {
        if (steamId.m_SteamID == 0 || string.IsNullOrEmpty(name))
        {
            return;
        }

        Names[steamId.m_SteamID] = name;
    }

    public static string GetOrRequest(NuclearOption.Networking.Player player)
    {
        if (player == null || player.SteamID == 0)
        {
            return string.Empty;
        }

        if (Names.TryGetValue(player.SteamID, out var cached))
        {
            return cached;
        }

        if (!SteamManager.ClientInitialized)
        {
            return string.Empty;
        }

        var name = SteamFriends.GetFriendPersonaName(player.CSteamID);
        if (!string.IsNullOrEmpty(name) && name != "[unknown]")
        {
            Names[player.SteamID] = name;
            return name;
        }

        SteamFriends.RequestUserInformation(player.CSteamID, true);
        return name ?? string.Empty;
    }
}
