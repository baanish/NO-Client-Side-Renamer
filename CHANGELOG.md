# Changelog

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
