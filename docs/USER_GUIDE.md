# User guide

This guide covers Nuclear Option Client-Side Renamer v0.2.1. The mod is intended for controlled testing only. Vanilla-peer isolation has not passed in either join direction. See [Testing](TESTING.md).

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

Select **Remove local** to delete both the player's local SteamID64 entry and an exact name-only fallback for that player. A remote alias can apply again after removal. Select **Reload now** to read the local file and refresh all enabled CSV rosters.

The list reads nonzero-SteamID players from the active game registry. Its viewport stays eight rows tall and scrolls when more players match. Search is case-insensitive and matches the original name or full SteamID64. The list does not populate from the main menu or Steam lobby browser. Spectator coverage has not been verified, and zero-ID entries such as a dedicated-server identity are excluded.

## Edit the JSON file directly

Each object in `players` has four required fields:

| Field | Meaning |
| --- | --- |
| `enabled` | Applies the entry when `true`. Disabled rows are retained but do not rename a player. |
| `steamId` | Canonical public individual SteamID64. Leave blank only for a temporary name fallback. |
| `steamName` | Original Steam persona name. Required when `steamId` is blank and retained as context on ID entries. |
| `displayName` | Display alias. It must contain a non-whitespace character. Local JSON does not impose a length or character limit. Remote CSV has stricter limits below. |

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

## Fetch a shared CSV roster

Ask the roster owner for a direct HTTPS endpoint. It must return HTTP 200 and UTF-8 CSV with exactly these two columns:

```csv
PlayerName,SteamID
ExamplePilot | Viper 1-1,76561198000000001
"Pilot, Two",76561198000000002
```

Column order can be reversed and header names are case-insensitive. In `SteamID`, enter a full decimal SteamID64 or the original Steam display name, such as `baanish`. Text matches exactly, including capitalization and spaces. A name can match multiple players and stops matching if they change it; use SteamID64 for a stable identity. Digit-only values are reserved for canonical SteamID64. Other text is treated as a name, without profile-URL lookup or numeric conversion. Quotes around fields and doubled quotes inside quoted fields are supported. Save the spreadsheet as UTF-8 CSV. A UTF-8 byte-order mark is accepted.

Configure up to eight independent sources. In F1, use **Remote CSV** for roster 1 and **Remote CSV 2** through **Remote CSV 8** for additional rosters. Each section has its own **Enabled**, **Url** and **Password**. Enter its URL and optional token, then enable that roster. Existing single-roster settings remain roster 1.

Without Configuration Manager, close the game and edit these sections of `com.baanish.nuclearoption.clientsiderenamer.cfg`. For two rosters:

```ini
[Remote CSV]
Enabled = true
Url = https://roster.example.org/playernames.csv
Password =

[Remote CSV 2]
Enabled = true
Url = https://squadron.example.org/playernames.csv
Password =
```

Every enabled roster fetches automatically when the mod loads, including at the main menu. Downloads run independently in the background. Local aliases work while they are pending, and the game is asked to refresh names when each valid roster arrives. Editing a URL or token clears and disables only that roster. Enable it again when the edits are complete. This prevents sending its token to an unfinished URL while you type. All rosters apply across matches on that client. There is no game-server discovery or periodic polling. **Reload now** can fetch newer rosters during a long-running game session, but is not required on startup.

A nonempty **Password** is sent as a custom header:

```http
PASSWORD: your-roster-token
```

An empty value omits the header. This is a shared token, with no login exchange. The endpoint owner must check it. Use a dedicated printable ASCII token of at most 1,024 characters. The field is masked in F1, but the token is plain text in the BepInEx config. Keep that file out of shared screenshots, backups and support uploads. Server administrators should redact the `PASSWORD` header from their request logs. Do not put secrets in the URL.

All enabled local aliases, including name-only fallbacks, take priority over remote aliases. If a player matches multiple rosters, the lowest-numbered loaded roster wins: roster 1 before roster 2, and so on. Download completion order does not change that priority. While an earlier roster is still loading, a later roster's alias can apply temporarily. Disabling or emptying the earlier roster reveals the next available match. Within one roster, an ID match wins over a name match. Duplicate IDs or exact Steam names within one CSV reject that CSV.

**Save** creates a local override. **Remove local** removes it and may reveal a remote alias again. Disabled local rows do not suppress remote aliases. Disable every remote roster to use only local mappings. Turning **General / Enabled** off cancels all fetches, clears the retained remote rosters and restores original names; turning it back on fetches every enabled roster again. A failed fetch after re-enabling has no previous remote roster to restore.

Downloads stay in memory and never overwrite `aliases.json`. A failed refresh retains only that source's last valid roster during that game run. Other rosters continue loading and working. After a restart, a failed source contributes no aliases; local aliases and other valid rosters remain available. There is no disk cache or automatic retry. Changing a URL or token clears that source's old roster even if its replacement later fails. A valid header-only CSV clears that roster alone. Background completion preserves unsaved alias edits in F1.

The full CSV is rejected if any row is invalid. Each roster is limited to a 1 MiB CSV body, 10,000 aliases and 128 UTF-16 code units per name. The eight-roster cap bounds simultaneous downloads and retained data. Names cannot be blank or contain control characters, invisible formatting characters, line separators, or `<` and `>` markup. Digit-only identifiers must be unique canonical public individual SteamID64 values. Text identifiers use the same name limits and must be unique with case-sensitive comparison. Extra columns, blank records and multiline fields are rejected. Each request has a 10-second deadline and rejects redirects and compressed responses. The HTTPS certificate must validate normally, even if another mod installs a permissive global certificate callback. System proxies are not used. Unity Mono does not enforce the requested 16 KiB response-header limit, so the body limit does not bound memory used for response headers.

F1 shows a separate numbered status and count for each configured roster. Tokens belong only to their respective endpoints. Fetch failures report the exception type without its message, URL, token or response body. This helps distinguish invalid input from transport or runtime failures. Check the roster owner's endpoint and server logs if the request fails. Remote names remain untrusted display text; each roster owner can change what names you see, and its endpoint can see your IP address and request timing. The mod uploads no player roster or Steam IDs.

## Configure the plugin

The BepInEx configuration file is `BepInEx/config/com.baanish.nuclearoption.clientsiderenamer.cfg`.

| Section | Setting | Default | Effect |
| --- | --- | --- | --- |
| `General` | `Enabled` | `true` | Applies aliases. Changing this setting asks the game to refresh existing names. |
| `Aliases` | `File` | `ClientSideRenamer/aliases.json` | Selects the JSON file. Relative paths resolve from `BepInEx/config`; absolute paths are accepted. |
| `Aliases` | `ReloadOnChange` | `true` | Watches the selected file and schedules a reload after observed changes settle for 300 ms. |
| `Remote CSV` | `Enabled` | `false` | Fetches a roster automatically on mod load. Disable to clear remote aliases and cancel the current fetch. |
| `Remote CSV` | `Url` | empty | Direct HTTPS CSV URL chosen by you. Redirects and URL-embedded credentials are rejected. |
| `Remote CSV` | `Password` | empty | Optional token sent in the `PASSWORD` request header. Stored as plain text in the config file. |

The same three settings appear in `Remote CSV 2` through `Remote CSV 8`. All rosters are disabled by default. Their number sets lookup priority; there is no shared token or global remote-roster switch.

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

- Vanilla-peer isolation is unproven when joining and hosting. Do not use v0.2.1 for ordinary multiplayer.
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
