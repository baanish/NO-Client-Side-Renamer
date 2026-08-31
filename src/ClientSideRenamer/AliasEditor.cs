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
    private string _playerSearch = string.Empty;
    private Vector2 _playerScroll;

    private const float PlayerRowHeight = 22f;
    private const float PlayerListHeight = PlayerPicker.MaxVisibleRows * (PlayerRowHeight + 2f);

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
            .ToArray();

        var candidates = players
            .Select((player, sourceIndex) => new PlayerPickerCandidate(
                sourceIndex,
                player.SteamID,
                player.PlayerIndex,
                OriginalNameCache.GetOrRequest(player)))
            .ToArray();
        var options = PlayerPicker.Normalize(candidates);

        if (options.Length == 0)
        {
            ClearPlayerSelection();
            GUILayout.Label("No players are loaded in the current match.");
            return;
        }

        var selectedPlayer = SelectAvailablePlayer(players, options);

        GUILayout.Space(6f);
        GUILayout.Label($"Current match: {options.Length} player{(options.Length == 1 ? string.Empty : "s")}");
        selectedPlayer = DrawPlayerList(players, options, selectedPlayer);
        DrawPlayerEditor(selectedPlayer);
    }

    private Player SelectAvailablePlayer(Player[] players, PlayerPickerOption[] options)
    {
        var selectedIndex = PlayerPicker.ResolveSelectionIndex(options, _selectedSteamId);
        var option = options[selectedIndex];
        if (option.SteamId != _selectedSteamId)
        {
            _selectedSteamId = option.SteamId;
            _loadedInputSteamId = 0;
            _loadedInputSteamName = string.Empty;
            _playerScroll = Vector2.zero;
        }

        return players[option.SourceIndex];
    }

    private Player DrawPlayerList(
        Player[] players,
        PlayerPickerOption[] options,
        Player selectedPlayer)
    {
        GUILayout.Label("Players");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Search", GUILayout.Width(48f));
        var nextSearch = GUILayout.TextField(_playerSearch ?? string.Empty, GUILayout.ExpandWidth(true));
        if (!string.Equals(nextSearch, _playerSearch, StringComparison.Ordinal))
        {
            _playerSearch = nextSearch;
            _playerScroll = Vector2.zero;
        }

        if (GUILayout.Button("Clear", GUILayout.Width(48f)) && !string.IsNullOrEmpty(_playerSearch))
        {
            _playerSearch = string.Empty;
            _playerScroll = Vector2.zero;
        }
        GUILayout.EndHorizontal();

        var matches = PlayerPicker.Filter(options, _playerSearch);
        GUILayout.Label($"Showing {matches.Length} of {options.Length} players");

        GUILayout.BeginVertical(GUI.skin.box);
        _playerScroll = GUILayout.BeginScrollView(
            _playerScroll,
            false,
            true,
            GUILayout.Height(PlayerListHeight));

        if (matches.Length == 0)
        {
            GUILayout.Label("No players match this search.");
        }
        else
        {
            foreach (var option in matches)
            {
                var prefix = option.SteamId == _selectedSteamId ? "Selected: " : string.Empty;
                var content = new GUIContent(prefix + option.VisibleLabel, option.FullLabel);
                if (!GUILayout.Button(
                    content,
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(PlayerRowHeight)))
                {
                    continue;
                }

                _selectedSteamId = option.SteamId;
                _loadedInputSteamId = 0;
                _loadedInputSteamName = string.Empty;
                selectedPlayer = players[option.SourceIndex];
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        return selectedPlayer;
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

    private void ClearPlayerSelection()
    {
        _selectedSteamId = 0;
        _loadedInputSteamId = 0;
        _loadedInputSteamName = string.Empty;
        _playerSearch = string.Empty;
        _playerScroll = Vector2.zero;
    }
}
