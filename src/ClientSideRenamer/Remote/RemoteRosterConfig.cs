using BepInEx.Configuration;
using UnityEngine;

namespace ClientSideRenamer.Remote;

internal sealed class RemoteRosterConfig
{
    public string Section { get; }
    public ConfigEntry<bool> Enabled { get; }
    public ConfigEntry<string> Url { get; }
    public ConfigEntry<string> Password { get; }

    internal RemoteRosterConfig(ConfigFile config, string section)
    {
        Section = section;
        Enabled = config.Bind(
            section, "Enabled", false,
            "Fetch this HTTPS roster automatically on mod load. Local aliases take priority, then earlier roster slots. Set up the URL and authentication before enabling.");
        Url = config.Bind(
            section, "Url", string.Empty,
            "Direct HTTPS CSV endpoint with PlayerName and SteamID columns. Editing disables this roster until you enable it again. Redirects are refused. Applies across matches; no server discovery.");
        Password = config.Bind(
            section, "Password", string.Empty,
            new ConfigDescription(
                "Optional token sent only to this roster in the PASSWORD request header over HTTPS. Stored as plain text in this config file; use a dedicated roster token. Editing disables this roster until you enable it again.",
                null, new ConfigurationManagerAttributes { CustomDrawer = DrawPassword }));

        Url.SettingChanged += OnSourceChanged;
        Password.SettingChanged += OnSourceChanged;
    }

    private void OnSourceChanged(object sender, EventArgs eventArgs)
    {
        // Text fields emit partial edits. Never send the stored token to an unfinished URL.
        Enabled.Value = false;
    }

    private static void DrawPassword(ConfigEntryBase entry)
    {
        var current = (string)entry.BoxedValue;
        var next = GUILayout.PasswordField(current, '*', 1024, GUILayout.ExpandWidth(true));
        if (next != current)
        {
            entry.BoxedValue = next;
        }
    }
}
