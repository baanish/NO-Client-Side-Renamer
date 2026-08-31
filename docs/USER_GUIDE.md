# User guide

This guide covers Nuclear Option Client-Side Renamer v0.1.1. The mod is intended for controlled testing only. Vanilla-peer isolation has not passed in either join direction. See [Testing](TESTING.md).

## Requirements

- Nuclear Option
- BepInEx 5 installed for Nuclear Option
- `ClientSideRenamer.dll`
- BepInEx Configuration Manager if you want to add aliases from the F1 menu

Configuration Manager is a soft dependency. The renamer loads and reads `aliases.json` without it.

## Install the plugin

1. Close Nuclear Option.
2. Create `BepInEx/plugins/ClientSideRenamer/` under the game directory if it does not exist.
3. Copy `ClientSideRenamer.dll` into that directory.
4. Start Nuclear Option.

The plugin creates its parent directory and alias file when either is missing:

```text
BepInEx/
  config/
    com.baanish.nuclearoption.clientsiderenamer.cfg
    ClientSideRenamer/
      aliases.json
  plugins/
    ClientSideRenamer/
      ClientSideRenamer.dll
```

The generated alias file contains two disabled examples. It does not activate an alias:

```json
{
  "schemaVersion": 1,
  "players": [
    {
      "enabled": false,
      "steamId": "",
      "steamName": "ExamplePilot",
      "displayName": "ExamplePilot | Viper 1-1"
    },
    {
      "enabled": false,
      "steamId": "",
      "steamName": "AnotherPilot",
      "displayName": "AnotherPilot | Falcon 2-3"
    }
  ]
}
```

## Add or change an alias in F1

1. Enter a match with the target player.
2. Open BepInEx Configuration Manager. Its default key is `F1`.
3. Expand **Nuclear Option Client-Side Renamer**.
4. Find the player in the scrollable **Players** list under **Current match**. In a large lobby, enter part of the original name or SteamID64 in **Search**.
5. Select the target player's row. The four digits in brackets are the last four digits of the player's SteamID64.
6. Enter the preferred display name in **Alias**.
7. Select **Save**.

The selection is tracked by SteamID64, so it remains on the same player if the roster order changes. If that player leaves, the editor selects the first remaining player.

Saving does the following:

- adds or updates the SteamID64 entry;
- removes a name-only fallback for the same original Steam name;
- replaces the JSON file through a same-directory temporary file;
- refreshes the alias lookup and existing player-name caches.

Select **Remove** to delete both the player's SteamID64 entry and an exact name-only fallback for that player. Select **Reload now** to read the file immediately.

The list reads nonzero-SteamID players from the active game registry. Its viewport stays eight rows tall and scrolls when more players match. Search is case-insensitive and matches the original name or full SteamID64. The list does not populate from the main menu or Steam lobby browser. Spectator coverage has not been verified, and zero-ID entries such as a dedicated-server identity are excluded.

## Edit the JSON file directly

Each object in `players` has four required fields:

| Field | Meaning |
| --- | --- |
| `enabled` | Applies the entry when `true`. Disabled rows are retained but do not rename a player. |
| `steamId` | Canonical public individual SteamID64. Leave blank only for a temporary name fallback. |
| `steamName` | Original Steam persona name. Required when `steamId` is blank and retained as context on ID entries. |
| `displayName` | Display alias. It must contain a non-whitespace character. v0.1.1 does not impose a length or character limit. |

`schemaVersion` must be `1`.

SteamID64 matching takes priority. An entry with a populated `steamId` matches only that ID and never falls back to `steamName`. A blank `steamId` enables an exact, case-sensitive `steamName` match. For example, `ExamplePilot` and `examplepilot` are different fallback names.

Use name-only rows only to bootstrap a test. When one matches, the plugin logs the resolved SteamID64. Saving the player in F1 upgrades the row to that stable ID.

The file is rejected if it contains:

- an unsupported schema version;
- a missing required collection or field;
- an invalid or noncanonical SteamID64;
- a blank alias;
- a name-only row with no Steam name;
- duplicate SteamID64 values;
- duplicate fallback Steam names.

Duplicates are rejected even when one of the rows is disabled.

## Configure the plugin

The BepInEx configuration file is `BepInEx/config/com.baanish.nuclearoption.clientsiderenamer.cfg`.

| Section | Setting | Default | Effect |
| --- | --- | --- | --- |
| `General` | `Enabled` | `true` | Applies aliases. Changing this setting asks the game to refresh existing names. |
| `Aliases` | `File` | `ClientSideRenamer/aliases.json` | Selects the JSON file. Relative paths resolve from `BepInEx/config`; absolute paths are accepted. |
| `Aliases` | `ReloadOnChange` | `true` | Watches the selected file and schedules a reload after observed changes settle for 300 ms. |

Relative paths are not contained inside `BepInEx/config`. A path containing `..` can resolve outside it, so use only a location you intend the plugin to read and write.

The generated config also contains an empty `[Alias editor] Editor` string. Configuration Manager uses that entry to host the optional custom drawer; changing its text has no effect.

Changing `Aliases.File` rebinds the store and watcher on the Unity update thread. Turning `ReloadOnChange` off stops automatic reloads; **Reload now** remains available in F1.

## Understand reload and failure behavior

File-system callbacks do not parse JSON or touch game objects. They schedule work, and the plugin reloads the file on the Unity update thread after a 300 ms debounce. File-system notifications are best effort, so use **Reload now** when an external edit does not appear.

If the alias file is deleted after a successful load, the plugin recreates it from the last valid in-memory document. If there is no valid document yet, it creates the disabled generic template. Recreated files preserve the parsed alias data, but not the original whitespace or formatting.

If the configured directory cannot be watched, the plugin keeps the loaded aliases and manual reload available. It reports the watcher error in F1 and `BepInEx/LogOutput.log`. Create or correct the directory, then toggle `ReloadOnChange` off and on to retry.

After a successful reload, the plugin clears the game's shared player-name cache, resets each loaded player's cached name, and invokes the game's existing persona-state refresh path. Existing displays should update without reconnecting, but text already captured by an event notification is not rewritten.

If a reload finds malformed JSON or an invalid document, the plugin:

- keeps the last valid in-memory mapping when one exists;
- leaves the invalid file unchanged;
- reports the error in F1 and `BepInEx/LogOutput.log`;
- refuses F1 saves until the file on disk is valid again.

Fix the file, save it, and wait for automatic reload or select **Reload now**. This protection prevents the editor from replacing manual changes it cannot understand.

## Known limits

- Vanilla-peer isolation is unproven when joining and hosting. Do not use v0.1.1 for ordinary multiplayer.
- Aliases may appear in host-local logs, ban-list labels, and local third-party recordings. This does not by itself mean a vanilla peer received the alias.
- Lobby-browser titles are not renamed.
- The F1 roster excludes zero-ID entries, and spectator coverage is unverified.
- Text formatted before a reload or disable action is not rewritten.
- The cache refresh relies on private Nuclear Option fields and may need updating after a game patch.

## Troubleshooting

### The F1 editor is missing

Install BepInEx Configuration Manager. The renamer itself should still load without it, and direct JSON editing remains available.

### No players appear in the list

Join a match and reopen the plugin section. The editor reads the active player registry and does not populate from the main menu or lobby browser.

### A JSON edit did not appear

Check that `ReloadOnChange` is enabled, then select **Reload now**. Read `BepInEx/LogOutput.log` for validation or file-access errors. The F1 panel also shows the resolved alias-file path.

### The old name remains in one notification

Trigger a new event after F1 reports the alias as loaded. Existing notification text may have been formatted before the refresh. If a newly generated event still uses the old name, record the surface and timing as a test failure.

## Disable or uninstall

Set `General.Enabled` to `false` to restore original names on live displays without removing mappings. Previously formatted text will remain unchanged. To uninstall, close the game and remove `BepInEx/plugins/ClientSideRenamer/ClientSideRenamer.dll`. The config and alias files may be kept for a later install or removed separately.
