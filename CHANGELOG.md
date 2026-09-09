# Changelog

## 0.2.0 - unreleased

- Added opt-in HTTPS CSV fetching automatically on mod load and when remote CSV is enabled.
- Supported up to eight independent rosters with separate URLs, tokens and load status. Local aliases win, followed by the lowest-numbered loaded roster for that SteamID.
- Added an optional `PASSWORD` request header with a masked F1 field. The token is stored as plain text in BepInEx config.
- Kept remote aliases in memory, with local aliases taking priority and failed refreshes retaining the last valid roster.
- Rejected redirects, invalid certificates, compressed responses, malformed CSV, duplicate IDs, markup and control characters. Each request has a 10-second deadline and a 1 MiB CSV body limit, with at most 10,000 aliases and 128 UTF-16 code units per name.
- Canceled obsolete fetches and cleared only the affected roster when its source changes or it is disabled.
- Disabled remote fetching during URL and token edits until explicitly re-enabled, preventing requests to unfinished URLs.

The new startup fetch and display refresh still require in-game verification. The existing multiplayer isolation gates remain open.

## 0.1.1 pre-release - 2026-08-31

### Changed

- Replaced the unbounded current-match dropdown with an eight-row, scrollable player list.
- Added case-insensitive player-name and SteamID64 search for large lobbies.
- Made duplicate player selection and ordering deterministic when the active registry contains repeated SteamID64 entries.
- Replaced the personal starter alias with two disabled, generic examples.
- Recreated a deleted alias file from the last valid in-memory document instead of restoring the starter template.
- Reported watcher binding failures without throwing through the Unity update loop.
- Documented host-local surfaces that may retain aliases without sending them to vanilla peers.

### Validation

- 35 tests pass, including 100-player filtering, duplicate IDs, roster changes, long Unicode names, deleted-file recovery, and watcher binding failures.
- The bounded player list was observed in-game with 28 players.

## 0.1.0 pre-release - 2026-08-30

This is the first two-person test build.

### Added

- SteamID64-first display aliases with an exact, case-sensitive Steam-name fallback.
- A versioned JSON alias file with validation and last-known-good retention.
- Automatic file reload with a 300 ms debounce and a manual reload action.
- An optional Configuration Manager editor with a current-match player dropdown.
- Cache invalidation after reload, save, remove, enable, and disable actions.
- A private-test `Baanish` to `Baanish | Reaper 5-2` starter mapping.

### Validation

- 22 alias-file tests pass.
- Remote aliases were observed on the tested tactical map, HUD marker, and aircraft MFD.
- Scoreboard and newly generated event-notification aliases were observed on the modded client.

### Known limits

- Vanilla-peer isolation has not passed in either join direction. Use this build only for the controlled two-person test.
- Existing rendered notifications are not rewritten after a reload.
- Spectator coverage in the editor is unverified.
- The starter mapping is for private testing and must be removed before a public or NOMM release.
