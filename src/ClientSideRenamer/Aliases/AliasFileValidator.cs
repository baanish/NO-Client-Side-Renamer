using System;
using System.Collections.Generic;
using System.Globalization;

namespace ClientSideRenamer.Aliases;

internal static class AliasFileValidator
{
    private const ulong PublicIndividualSteamIdBase = 76561197960265728;

    public static bool TryCreateSnapshot(
        AliasFileDocument document,
        out AliasSnapshot snapshot,
        out string error)
    {
        snapshot = AliasSnapshot.Empty;

        if (document == null)
        {
            error = "The alias file is empty.";
            return false;
        }

        if (document.SchemaVersion != AliasFileDocument.CurrentSchemaVersion)
        {
            error = $"Unsupported schemaVersion {document.SchemaVersion}; expected {AliasFileDocument.CurrentSchemaVersion}.";
            return false;
        }

        if (document.Players == null)
        {
            error = "The players collection is required.";
            return false;
        }

        var aliasesBySteamId = new Dictionary<string, string>(StringComparer.Ordinal);
        var aliasesBySteamName = new Dictionary<string, string>(StringComparer.Ordinal);
        var seenSteamIds = new HashSet<string>(StringComparer.Ordinal);
        var seenSteamNames = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < document.Players.Count; index++)
        {
            var player = document.Players[index];
            if (player == null)
            {
                error = $"players[{index}] must be an object.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(player.DisplayName))
            {
                error = $"players[{index}].displayName must not be empty.";
                return false;
            }

            if (player.SteamId == null)
            {
                error = $"players[{index}].steamId must be a string.";
                return false;
            }

            if (player.SteamName == null)
            {
                error = $"players[{index}].steamName must be a string.";
                return false;
            }

            if (!string.IsNullOrEmpty(player.SteamId))
            {
                if (!IsCanonicalSteamId64(player.SteamId))
                {
                    error = $"players[{index}].steamId is not a valid SteamID64.";
                    return false;
                }

                if (!seenSteamIds.Add(player.SteamId))
                {
                    error = $"Duplicate steamId '{player.SteamId}'.";
                    return false;
                }

                if (player.Enabled)
                {
                    aliasesBySteamId.Add(player.SteamId, player.DisplayName);
                }

                continue;
            }

            if (string.IsNullOrEmpty(player.SteamName))
            {
                error = $"players[{index}] requires steamName when steamId is blank.";
                return false;
            }

            if (!seenSteamNames.Add(player.SteamName))
            {
                error = $"Duplicate fallback steamName '{player.SteamName}'.";
                return false;
            }

            if (player.Enabled)
            {
                aliasesBySteamName.Add(player.SteamName, player.DisplayName);
            }
        }

        snapshot = new AliasSnapshot(aliasesBySteamId, aliasesBySteamName);
        error = string.Empty;
        return true;
    }

    private static bool IsCanonicalSteamId64(string value)
    {
        return ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            && parsed > PublicIndividualSteamIdBase
            && parsed <= PublicIndividualSteamIdBase + uint.MaxValue
            && parsed.ToString(CultureInfo.InvariantCulture) == value;
    }
}
