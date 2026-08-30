using System.Reflection;
using HarmonyLib;
using NuclearOption.Networking;
using Steamworks;

namespace ClientSideRenamer;

[HarmonyPatch]
internal static class PlayerNamePatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(Player),
            "TryGetNameFromSteam",
            [typeof(CSteamID), typeof(string).MakeByRefType()]
        );
    }

    private static void Postfix(CSteamID steamID, ref string playerName, bool __result)
    {
        if (!__result)
        {
            return;
        }

        var originalName = playerName;
        OriginalNameCache.Remember(steamID, originalName);

        if (Plugin.TryResolveAlias(steamID.m_SteamID, originalName, out var alias))
        {
            playerName = alias;
        }
    }
}
