using System.Collections.Generic;
using Newtonsoft.Json;

namespace ClientSideRenamer.Aliases;

public sealed class AliasFileDocument
{
    public const int CurrentSchemaVersion = 1;

    [JsonProperty("schemaVersion", Required = Required.Always)]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonProperty("players", Required = Required.Always)]
    public List<PlayerAliasEntry> Players { get; set; } = new();

    public static AliasFileDocument CreateInitialTemplate()
    {
        return new AliasFileDocument
        {
            Players = new List<PlayerAliasEntry>
            {
                new()
                {
                    Enabled = false,
                    SteamId = string.Empty,
                    SteamName = "Example",
                    DisplayName = "Example | Callsign"
                },
                new()
                {
                    Enabled = true,
                    SteamId = string.Empty,
                    SteamName = "Baanish",
                    DisplayName = "Baanish | Reaper 5-2"
                }
            }
        };
    }

    internal AliasFileDocument Copy()
    {
        var copy = new AliasFileDocument { SchemaVersion = SchemaVersion };
        foreach (var player in Players)
        {
            copy.Players.Add(player.Copy());
        }

        return copy;
    }
}

public sealed class PlayerAliasEntry
{
    [JsonProperty("enabled", Required = Required.Always)]
    public bool Enabled { get; set; }

    [JsonProperty("steamId", Required = Required.Always)]
    public string SteamId { get; set; } = string.Empty;

    [JsonProperty("steamName", Required = Required.Always)]
    public string SteamName { get; set; } = string.Empty;

    [JsonProperty("displayName", Required = Required.Always)]
    public string DisplayName { get; set; } = string.Empty;

    internal PlayerAliasEntry Copy()
    {
        return new PlayerAliasEntry
        {
            Enabled = Enabled,
            SteamId = SteamId,
            SteamName = SteamName,
            DisplayName = DisplayName
        };
    }
}
