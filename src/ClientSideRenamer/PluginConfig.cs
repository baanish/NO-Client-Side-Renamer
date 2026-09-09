using BepInEx.Configuration;
using ClientSideRenamer.Remote;

namespace ClientSideRenamer;

internal sealed class PluginConfig
{
    public const int MaximumRemoteRosters = 8;

    public ConfigEntry<bool> Enabled { get; }
    public ConfigEntry<string> AliasFile { get; }
    public ConfigEntry<bool> ReloadOnChange { get; }
    public ConfigEntry<string> AliasEditor { get; }
    public IReadOnlyList<RemoteRosterConfig> RemoteRosters { get; }

    public PluginConfig(ConfigFile config, Action<ConfigEntryBase> drawAliasEditor)
    {
        Enabled = config.Bind(
            "General",
            "Enabled",
            true,
            "Replace mapped player names on this client."
        );

        AliasFile = config.Bind(
            "Aliases",
            "File",
            "ClientSideRenamer/aliases.json",
            "Alias JSON file. Relative paths resolve under BepInEx/config; absolute paths are allowed."
        );

        ReloadOnChange = config.Bind(
            "Aliases",
            "ReloadOnChange",
            true,
            "Reload aliases after the JSON file changes."
        );

        var remoteRosters = new RemoteRosterConfig[MaximumRemoteRosters];
        for (var index = 0; index < remoteRosters.Length; index++)
        {
            remoteRosters[index] = new RemoteRosterConfig(config, index == 0 ? "Remote CSV" : $"Remote CSV {index + 1}");
        }
        RemoteRosters = Array.AsReadOnly(remoteRosters);

        AliasEditor = config.Bind(
            "Alias editor",
            "Editor",
            string.Empty,
            new ConfigDescription(
                "Add, update, or remove aliases for players in the current session.",
                null,
                new ConfigurationManagerAttributes
                {
                    CustomDrawer = drawAliasEditor,
                    HideDefaultButton = true,
                    HideSettingName = true,
                    Order = 1000,
                }
            )
        );
    }
}
