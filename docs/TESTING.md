# Testing and validation

This document records what v0.1.1 has demonstrated and what remains open. It does not treat a successful build or a screenshot from one client as proof of multiplayer isolation.

## Status recorded on 2026-08-31

| Area | Status | Evidence or remaining work |
| --- | --- | --- |
| Release build | Pass | The plugin builds against the installed Nuclear Option and BepInEx assemblies. |
| Automated suite | Pass | 35 xUnit cases pass. |
| Missing-file creation and recovery | Pass | Automated tests cover the disabled generic template, deleted-file recovery, and deleted-parent-directory recovery. |
| F1 save | Observed | Runtime use created a SteamID64 mapping and refreshed displays. |
| F1 remove | Implemented, not separately recorded | Verify the original name returns and both matching row forms are removed. |
| Scoreboard | Observed | The modded client displayed the configured alias. |
| Crash, kill, and sink notifications | Observed | Fresh notifications displayed the alias after the mapping was active. |
| Tactical map | Observed in multiplayer | A remote player's renamed label was visible on the modded client. |
| HUD unit marker | Observed in multiplayer | A remote player's renamed marker was visible on the modded client. |
| Aircraft MFD pilot field | Observed in multiplayer | A remote player's renamed pilot field was visible on the modded client. |
| Existing notification refresh | Unresolved | One original-name notification coexisted with aliased live displays. Its creation time relative to reload was not established. |
| Fresh join and disconnect notifications | Not yet recorded | Save the alias before the event, then test a new join and disconnect. |
| Two-player session on 2026-08-30 | Observed on a development install | The user reported no issues. The installed DLL identified commit `037c5fe`, not the tagged `v0.1.0` release commit `99e0773`; join directions and compared surfaces were not recorded. |
| Large-lobby player list | Observed with 28 players | The bounded list and scrollbar worked in the supplied in-game capture. Tests cover 100-player filtering, bounded viewport policy, deterministic duplicate handling, roster changes, and long Unicode names. |
| Vanilla peer isolation, modded joiner | Not proven | The vanilla host must see only the original Steam name. |
| Vanilla peer isolation, modded host | Release gate | The vanilla joiner and network-visible server output must contain only the original Steam name. Host-local output is a separate compatibility surface. |
| Spectators | Unverified | Their presence in the active registry has not been established. |
| Zero-ID and lobby-only participants | Out of scope | The editor filters zero-ID entries and does not enumerate the Steam lobby. |
| Lobby-browser titles | Out of scope | They use separate Steam lobby metadata. |

“Observed” means the behavior was seen during manual testing or in a supplied screenshot. It does not mean an automated test covers the path. Map and HUD observations apply to the tested setup, including any installed display mods; they are not a claim about every vanilla or third-party surface. Host-local logs, ban-list labels, and local third-party recordings may contain aliases even when vanilla peers never receive them.

## Automated validation

Run the focused suite from the repository root:

```powershell
dotnet test .\tests\ClientSideRenamer.Tests\ClientSideRenamer.Tests.csproj -p:GamePath='D:\SteamLibrary\steamapps\common\Nuclear Option'
```

For a cached rerun without package restore:

```powershell
dotnet test .\tests\ClientSideRenamer.Tests\ClientSideRenamer.Tests.csproj --no-restore -p:GamePath='D:\SteamLibrary\steamapps\common\Nuclear Option'
```

Current result: 35 passed, 0 failed, 0 skipped.

The tests cover:

- initial file and parent-directory creation;
- detached edits before save;
- ID-only matching for ID rows;
- exact, case-sensitive name fallback;
- duplicate, invalid ID, blank alias, and schema rejection;
- noncanonical SteamID64 rejection;
- last-known-good retention after malformed or invalid reloads;
- recreation from the last valid document after the alias file or its parent directory is deleted;
- refusal to overwrite invalid external content;
- the successful replacement path and temporary-file cleanup;
- 100-player picker sorting and search;
- deterministic duplicate-ID handling and roster-change selection;
- long Unicode player-label truncation;
- watcher failure reporting and repeated disable and re-enable binding.

The suite does not load BepInEx, Harmony, Steamworks, Unity, or Nuclear Option runtime objects. It does not cover configuration path resolution, watcher debounce, cache refresh, F1 behavior, game-version compatibility, or multiplayer isolation.

NuGet may report `NU1900` when its vulnerability-audit service cannot be reached. That warning is separate from test execution; record it, but use the test summary to determine whether the suite ran.

## Build verification

```powershell
dotnet build .\src\ClientSideRenamer\ClientSideRenamer.csproj -c Release -p:GamePath='D:\SteamLibrary\steamapps\common\Nuclear Option'
```

Confirm all of the following before packaging:

- the build exits successfully;
- `src/ClientSideRenamer/bin/Release/netstandard2.1/ClientSideRenamer.dll` exists;
- the plugin has no unexpected private copies of game or BepInEx assemblies;
- building did not change the installed game directory.

## Two-person isolation test

Use one modded client and one client with no Client-Side Renamer DLL. Keep both clients' screenshots and relevant logs as evidence.

### Prepare the mapping

1. Start both clients and join the same match.
2. On the modded client, select the other player in Configuration Manager.
3. Save a distinctive alias that cannot be confused with the Steam name.
4. Wait until the editor reports that the alias was saved and a live display changes.
5. Record the alias file and resolved SteamID64 used for the test.

### Scenario A: modded player joins a vanilla host

1. The vanilla player hosts.
2. The modded player joins.
3. Trigger each required surface after the alias is active.
4. Confirm that the modded client sees the alias.
5. Confirm that the vanilla host sees only the original Steam name.

### Scenario B: modded player hosts

1. The modded player hosts a new match only for this controlled test.
2. The vanilla player joins.
3. Trigger the same surfaces after the alias is active.
4. Confirm that the modded host sees the alias.
5. Confirm that the vanilla joiner and network-visible server messages use only the original Steam name.

Scenario B is the hard release gate because client and server behavior share the modded host process.

### Required surfaces

- scoreboard or pause leaderboard;
- player-information or moderation screen, if present;
- a target-hover or HUD name label, if present;
- tactical map and aircraft display labels in the tested setup;
- a new join or disconnect notification;
- a new crash notification;
- a new kill or sink notification where the mission permits it.

Record which surfaces are vanilla and which come from another mod. A third-party surface is separate compatibility evidence.

### Reload and reverse transitions

While both players remain connected:

1. Change the alias in `aliases.json` and save it.
2. Confirm the new alias appears on cache-aware live displays after reload.
3. Set `General.Enabled` to `false` and confirm the original name returns on live displays.
4. Re-enable the setting and confirm the alias returns.
5. Remove the alias in F1 and confirm the original name returns.
6. Corrupt a disposable copy of the JSON file and confirm the last valid alias remains active while F1 save is refused.
7. Repair the file and confirm reload recovers without reconnecting.

Use **Reload now** if the file watcher misses an edit. The vanilla peer must show the original name throughout every transition.

## Distinguish stale text from desynchronization

A notification rendered before a reload may keep the old text even while live displays show the alias. That is not enough to establish a resolver failure.

Use this sequence:

1. Save the alias and wait for a live surface to update.
2. Trigger a new event with a recorded time.
3. Capture the new notification and a live surface in the same test interval.
4. Compare both clients.

Treat a newly created modded-client event that uses the old name as a display-coverage failure. Treat any alias on the vanilla peer or in network-visible server output as an isolation failure. An alias in a host-local log, ban-list label, or local third-party recording is a compatibility finding, not proof that the alias crossed the network.

## Release acceptance criteria

v0.1.1 is ready for a wider test only when:

- the focused automated suite passes;
- a release build succeeds against the current installed game;
- both two-person scenarios pass;
- each required modded-client surface resolves the alias for newly generated content;
- the vanilla peer and network-visible server output retain the original Steam name on every surface and transition;
- invalid JSON retains the last valid mapping and does not get overwritten by F1;
- known omissions remain documented.

If the vanilla peer receives an alias in either direction, do not describe the mod as client-side isolated. Disable or redesign host-mode behavior before release.
