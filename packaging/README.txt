Nuclear Option Client-Side Renamer v@VERSION@

PRERELEASE

Use this build only for controlled isolation testing. A vanilla
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

The first-run template contains two disabled, generic examples. No alias is
active until you save a player in F1 or enable an entry in aliases.json.

Open BepInEx Configuration Manager with F1 by default. Select a player from
the scrollable Players list, or filter it with Search, then save the alias as
a SteamID64 mapping.

Configuration Manager is optional. Without it, edit aliases.json directly.
Automatic file watching is best effort. Restart the game if an edit is missed.

Known limits

- Vanilla-peer isolation is unproven in both join directions.
- Host-local logs, ban-list labels, and third-party recordings may contain
  aliases even when vanilla peers do not receive them.
- Text rendered before a reload is not rewritten.
- Spectator coverage in the editor is unverified.

Source and full documentation:
https://github.com/baanish/NO-Client-Side-Renamer

MIT license. See LICENSE.txt.
