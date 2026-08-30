Nuclear Option Client-Side Renamer v@VERSION@

PRIVATE PRERELEASE

Use this build only for the controlled two-person isolation test. A vanilla
peer has not confirmed isolation when the modded player joins or hosts. The
modded-host direction is the higher-risk case because the name resolver is
process-wide.

Requirements

- Nuclear Option 0.34.2, tested on Steam build 24724372
- BepInEx 5, tested with 5.4.23.4
- BepInEx Configuration Manager, optional for the in-game editor

Install

The plugin-only archive contains the correct BepInEx directory tree. Extract
it beside NuclearOption.exe.

For a manual install from the flat NOMM archive, copy ClientSideRenamer.dll to:

  BepInEx/plugins/ClientSideRenamer/ClientSideRenamer.dll

Start the game once. The plugin creates:

  BepInEx/config/com.baanish.nuclearoption.clientsiderenamer.cfg
  BepInEx/config/ClientSideRenamer/aliases.json

The private test template enables this exact, case-sensitive fallback:

  Baanish -> Baanish | Reaper 5-2

Open BepInEx Configuration Manager with F1 by default. Select a player from
the Current match dropdown and save the alias to replace the name fallback
with a SteamID64 mapping.

Configuration Manager is optional. Without it, edit aliases.json directly.
Automatic file watching is best effort. Restart the game if an edit is missed.

Known limits

- Vanilla-peer isolation is unproven in both join directions.
- Text rendered before a reload is not rewritten.
- Spectator coverage in the editor is unverified.
- The personal starter mapping must be removed before a public or NOMM release.

Source and full documentation:
https://github.com/baanish/NO-Client-Side-Renamer

MIT license. See LICENSE.txt.
