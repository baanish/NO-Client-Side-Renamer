using BepInEx.Configuration;
using NuclearOption.Networking;
using UnityEngine;

namespace ClientSideRenamer;

internal sealed class AliasEditor
{
    private readonly Plugin _plugin;

    private ulong _selectedSteamId;
    private ulong _loadedInputSteamId;
    private string _loadedInputSteamName = string.Empty;
    private string _aliasInput = string.Empty;
    private bool _dropdownOpen;

    public AliasEditor(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void ClearInputs()
    {
        _loadedInputSteamId = 0;
        _loadedInputSteamName = string.Empty;
    }

    public void Draw(ConfigEntryBase _)
    {
        try
        {
            GUILayout.BeginVertical();
            DrawContents();
        }
        catch (Exception exception)
        {
            _plugin.LogEditorException(exception);
            GUILayout.Label("Alias editor failed. Check BepInEx/LogOutput.log.");
        }
        finally
        {
            GUILayout.EndVertical();
        }
    }

    private void DrawContents()
    {
        GUILayout.Label($"Loaded: {_plugin.AliasCount} aliases");
        GUILayout.Label($"File: {_plugin.AliasFilePath}");

        if (GUILayout.Button("Reload now", GUILayout.ExpandWidth(false)))
        {
            _plugin.ReloadFromEditor();
        }

        if (!string.IsNullOrEmpty(_plugin.EditorStatus))
        {
            GUILayout.Label(_plugin.EditorStatus);
        }

        var players = UnitRegistry.playerLookup.Values
            .Where(player => player != null && player.SteamID != 0)
            .GroupBy(player => player.SteamID)
            .Select(group => group.First())
            .OrderBy(player => player.PlayerIndex)
            .ToArray();

        if (players.Length == 0)
        {
            GUILayout.Label("No players are loaded in the current match.");
            return;
        }

        var selectedPlayer = SelectAvailablePlayer(players);

        GUILayout.Space(6f);
        GUILayout.Label($"Current match: {players.Length} player{(players.Length == 1 ? string.Empty : "s")}");
        DrawPlayerDropdown(players, selectedPlayer);
        DrawPlayerEditor(selectedPlayer);
    }

    private Player SelectAvailablePlayer(Player[] players)
    {
        var selectedPlayer = players.FirstOrDefault(player => player.SteamID == _selectedSteamId);
        if (selectedPlayer != null)
        {
            return selectedPlayer;
        }

        _selectedSteamId = players[0].SteamID;
        _loadedInputSteamId = 0;
        _loadedInputSteamName = string.Empty;
        _dropdownOpen = false;
        return players[0];
    }

    private void DrawPlayerDropdown(Player[] players, Player selectedPlayer)
    {
        GUILayout.Label("Player");
        if (GUILayout.Button(GetPlayerLabel(selectedPlayer), GUILayout.ExpandWidth(true)))
        {
            _dropdownOpen = !_dropdownOpen;
        }

        if (!_dropdownOpen)
        {
            return;
        }

        foreach (var player in players)
        {
            if (!GUILayout.Button(GetPlayerLabel(player), GUILayout.ExpandWidth(true)))
            {
                continue;
            }

            _selectedSteamId = player.SteamID;
            _loadedInputSteamId = 0;
            _loadedInputSteamName = string.Empty;
            _dropdownOpen = false;
        }
    }

    private void DrawPlayerEditor(Player player)
    {
        var steamId = player.SteamID;
        var originalName = OriginalNameCache.GetOrRequest(player);

        if (_loadedInputSteamId != steamId || _loadedInputSteamName != originalName)
        {
            _aliasInput = _plugin.TryGetAlias(steamId, originalName, out var currentAlias)
                ? currentAlias
                : string.Empty;
            _loadedInputSteamId = steamId;
            _loadedInputSteamName = originalName;
        }

        GUILayout.Space(5f);
        GUILayout.Label($"SteamID64: {steamId}");
        GUILayout.Label("Alias");
        _aliasInput = GUILayout.TextField(_aliasInput ?? string.Empty, GUILayout.MinWidth(260f));

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save", GUILayout.ExpandWidth(false)))
        {
            _plugin.SaveAlias(steamId, originalName, _aliasInput);
        }

        if (GUILayout.Button("Remove", GUILayout.ExpandWidth(false)))
        {
            if (_plugin.RemoveAlias(steamId, originalName))
            {
                _aliasInput = string.Empty;
            }
        }
        GUILayout.EndHorizontal();
    }

    private static string GetPlayerLabel(Player player)
    {
        var name = OriginalNameCache.GetOrRequest(player);
        if (string.IsNullOrEmpty(name))
        {
            name = "[name pending]";
        }

        return $"{name} [{player.SteamID % 10000:D4}]";
    }
}
