# Design and constraints

This document records the v0.1.1 design for reviewers. It separates accepted implementation decisions from inferred rationale, provisional behavior, and release gates.

## Goals

- Substitute mapped player names in the modded game process.
- Use SteamID64 as the durable identity.
- Let a tester add aliases from the current match.
- Support direct JSON editing and automatic reload.
- Preserve the last valid mapping when an edit is malformed.
- Refresh existing cache-aware displays without reconnecting.

## Non-goals for v0.1.1

- Changing Steam authentication, Steam persona names, or synced player IDs.
- Synchronizing aliases between modded clients.
- Renaming Steam lobby-browser titles.
- Providing a general-purpose roster or moderation interface.
- Claiming host-mode isolation before a vanilla peer verifies it.
- Rewriting text that a notification or chat surface already formatted.

## Data flow

```text
aliases.json -> validate -> last-known-good document -> ID/name snapshot
                                                        |
Steam name -> Player.TryGetNameFromSteam postfix -> resolve alias
                                                        |
                                          vanilla name caches and UI

file event -> debounce flag -> Unity Update -> reload -> cache refresh
F1 Save   -> validate -> file replace -> snapshot -> cache refresh
```

The plugin changes one resolved name and leaves downstream UI composition to the game. The same central hook also means every caller in the process can observe the alias.

## Decision: patch the central Steam-name resolver

**Status:** accepted for v0.1.1, with a host-mode release gate.

[PlayerNamePatch.cs](../src/ClientSideRenamer/PlayerNamePatch.cs) adds a Harmony postfix to the game's private static `Player.TryGetNameFromSteam(CSteamID, out string)`. It remembers the unmodified Steam name, then replaces the out value when an alias resolves.

The implementation shape indicates that this level was chosen to cover multiple displays while preserving game-owned formatting around the name. Patching individual UI surfaces would require separate hooks for each consumer. Patching a higher-level composed display string could discard or duplicate vehicle and server-tag formatting. This rationale is inferred from the one-hook design rather than recorded in the original source.

The tradeoff is process-wide scope. When a player hosts, client and server behavior run in the same process. A server-formatted outbound string could therefore observe the alias. The implementation does not modify network identity fields, but that alone does not prove that every outbound message carries the original name.

**Release contract:** a vanilla peer must always see the original Steam name in both join directions. Any alias visible on the vanilla peer or in network-visible server output fails host-mode support. Host-local logs, ban-list labels, and local third-party recordings may contain an alias because they run after the central resolver; that does not by itself prove network propagation.

## Decision: prefer SteamID64 and isolate name fallback

**Status:** accepted.

[Plugin.cs](../src/ClientSideRenamer/Plugin.cs) first resolves a SteamID64 entry. Only when no ID entry matches does it attempt a name-only lookup. [AliasFileValidator.cs](../src/ClientSideRenamer/Aliases/AliasFileValidator.cs) ensures that a populated ID row never enters the fallback-name index.

Steam persona names are mutable and not unique. Name fallback exists to make a first test possible before an ID has been captured. It is ordinal and case-sensitive so that the plugin does not silently merge names under an assumed casing policy.

When a fallback matches during normal resolution, the plugin logs the SteamID64 once per player. Saving that player through F1 upgrades the existing exact-name fallback to an ID row.

## Decision: validate disabled rows too

**Status:** accepted.

Every row must remain structurally valid, including disabled rows. Duplicate IDs and fallback names are also rejected across enabled and disabled rows. This prevents an invalid dormant row from changing meaning merely because a tester enables it later.

The current schema accepts only canonical decimal SteamID64 values for public individual accounts. It rejects whitespace, leading zeros, other Steam account types, and out-of-range values.

The validator requires a nonblank alias but sets no length or character policy. Any downstream truncation or sanitization belongs to the game and is not a contract of this plugin.

## Decision: keep a last-known-good snapshot

**Status:** accepted.

[AliasFileStore.cs](../src/ClientSideRenamer/Aliases/AliasFileStore.cs) separates the file on disk from the active snapshot. After one successful load, a malformed or invalid reload leaves the active snapshot unchanged. If the first load is invalid, no aliases are active. The invalid file is preserved for repair.

If the file or its parent directory is deleted, the store recreates it from the last valid parsed document. Before any valid load, it creates a template with disabled examples. Recreation preserves alias values but not the deleted file's original formatting or ignored properties.

The F1 editor refuses to save while the disk copy is invalid. It validates the disk again immediately before saving so that it does not overwrite a malformed external edit made since the last reload.

Successful saves serialize to a uniquely named temporary file in the destination directory, flush it to disk, and replace or move it into place. Tests cover the successful replacement path and confirm that it leaves no temporary file. They do not prove filesystem-level atomicity or crash consistency on every filesystem.

One known concurrency limit remains: a valid external edit made between the editor's last load and save can be overwritten. The store validates content but does not compare a revision, timestamp, or hash.

## Decision: marshal reload work to the Unity thread

**Status:** accepted.

[ReloadWatcher.cs](../src/ClientSideRenamer/ReloadWatcher.cs) listens for changed, created, deleted, and renamed events. The callback only records a pending reload and moves its deadline 300 ms forward. [Plugin.cs](../src/ClientSideRenamer/Plugin.cs) polls that state during `Update`, parses the file, and schedules cache refresh there.

The implementation keeps JSON and Unity object work off the `FileSystemWatcher` callback thread. Configuration changes for the file path and watcher setting are also deferred to `Update`.

File watching remains best effort. The debounce runs only after an event is observed, so manual reload is the recovery path for a missed notification. A missing or inaccessible directory is reported in F1 and the log without throwing through `Update`; loaded aliases and manual reload remain available.

## Decision: refresh through the game's existing name path

**Status:** provisional across game updates.

[PlayerNameCache.cs](../src/ClientSideRenamer/PlayerNameCache.cs) clears `UnitRegistry.cachedPlayerNames`, resets each loaded player's private `_playerNameCache` and `_lastRequestedNameTime`, then calls `Steam_OnPersonaStateChanged()`.

Using the existing persona-state callback allows cache-aware game listeners to request names again instead of the mod enumerating every UI surface. It also depends on private field names and behavior. A Nuclear Option update can break this integration even when the alias-file core still passes.

Already-rendered text remains unchanged. The refresh affects subsequent name resolution and listeners that respond to the callback.

## Decision: cache original personas separately

**Status:** accepted.

[OriginalNameCache.cs](../src/ClientSideRenamer/OriginalNameCache.cs) stores the unmodified persona captured before the Harmony postfix substitutes an alias. The F1 editor uses this cache rather than reading an alias-shaped game cache.

This prevents alias text from feeding back into fallback matching or appearing as the player's Steam identity in the editor. The process-lifetime cache does not evict entries. A changed Steam persona is recorded when the patched resolver next returns it.

## Decision: make Configuration Manager optional

**Status:** accepted.

The plugin declares BepInEx Configuration Manager as a soft dependency. [ConfigurationManagerAttributes.cs](../src/ClientSideRenamer/ConfigurationManagerAttributes.cs) provides the metadata shape that Configuration Manager discovers by property name, avoiding a compile-time assembly reference.

Without Configuration Manager, the alias resolver, JSON file, watcher, and behavior settings still work. Only the custom editor is unavailable. The generated config contains an empty `[Alias editor] Editor` string entry that exists only to host the reflective drawer; changing its text has no effect.

## Decision: use one bounded player list and one editor

**Status:** accepted for the active registry.

[AliasEditor.cs](../src/ClientSideRenamer/AliasEditor.cs) reads `UnitRegistry.playerLookup.Values` and maps the live roster into [PlayerPicker.cs](../src/ClientSideRenamer/PlayerPicker.cs). The picker removes zero-ID entries, deduplicates by SteamID64, and orders players by `PlayerIndex` with stable tie-breakers.

The editor draws the roster as NMG-style button rows inside a nested scroll box. The box stays eight rows tall even when the registry contains 100 players. Search matches the original name or full SteamID64 without changing roster order.

The selection is stored as SteamID64 rather than list position. The selected row is marked in the list. Labels include the original Steam name and the last four ID digits. Long names are shortened without splitting a Unicode text element, while the full name remains in the tooltip and search index. The full SteamID64 also remains visible below the list.

A repeated editor block per player and an unbounded dropdown were rejected because both scale poorly with match size. The single selected-player editor stays below the roster list and reloads when the selected SteamID64 changes.

This is not a Steam-lobby roster. Lobby-only participants do not appear, zero-ID entries are excluded, and spectator coverage remains unverified.

## Decision: allow configurable file locations

**Status:** accepted, without path containment.

[Plugin.cs](../src/ClientSideRenamer/Plugin.cs) accepts an absolute path or resolves a relative path from `BepInEx/config` using `Path.GetFullPath`. Relative paths are not confined to that directory. A value containing `..` can resolve elsewhere.

This flexibility supports shared or relocated test files, but it also gives the plugin read and write access to the resolved location under the current user's permissions. The F1 panel exposes the final path for review.

## Compatibility constraints

- The plugin targets BepInEx 5 and `netstandard2.1`.
- The build references assemblies from the user's installed Nuclear Option and BepInEx directories.
- The Harmony target is a private game method.
- Cache refresh reads two private game fields.
- Roster editing relies on `UnitRegistry.playerLookup` and `PlayerIndex`.
- Persona lookup relies on Steamworks being initialized and can temporarily return a pending or unknown name.
- Building produces a DLL but never installs it into the game directory.

These are compatibility seams, not guaranteed public APIs. A game or loader update requires a build check and an in-game smoke test.

## Questions for review

1. Is the central resolver hook acceptable if both two-person isolation directions pass, or should host mode remain disabled by policy?
2. Is unrestricted alias length acceptable for v0.1.1, or should the schema pin a display-safe limit?
3. Is overwriting a newer but valid external file acceptable, or should F1 saves use revision detection?
4. Should the alias path be confined to `BepInEx/config`, or is the current explicit-path flexibility intentional?
5. Should spectators be added before a public test, or remain an unverified registry case?
6. Which name-consuming surfaces are release requirements beyond those already observed?

See [Testing](TESTING.md) for the evidence and acceptance criteria behind these questions.
