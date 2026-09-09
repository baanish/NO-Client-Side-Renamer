Nuclear Option Client-Side Renamer v@VERSION@

PRERELEASE

Use this build only for controlled isolation testing. A vanilla
peer has not confirmed isolation when the modded player joins or hosts. The
modded-host direction is the higher-risk case because the name resolver is
process-wide.

Requirements

- Nuclear Option, see the documentation for recorded in-game test versions
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
active until you save a player in F1, enable an entry in aliases.json, or
enable a remote CSV roster.

Open BepInEx Configuration Manager with F1 by default. Select a player from
the scrollable Players list, or filter it with Search, then save the alias as
a SteamID64 mapping.

Configuration Manager is optional. Without it, edit aliases.json directly.
Automatic file watching is best effort. Restart the game if an edit is missed.

Remote CSV

Set the Remote CSV Url to a direct HTTPS endpoint returning UTF-8 CSV with
PlayerName and SteamID columns. Enter an optional dedicated roster token in
Password, then set Remote CSV Enabled to true. The mod fetches automatically
on every load. Add sources under Remote CSV 2 through Remote CSV 8, each with
its own URL, token and Enabled setting. Local aliases take priority, then the
lowest-numbered loaded roster for each SteamID. Downloads stay in memory;
one failing or disabled source does not clear the others.

Password is sent in a PASSWORD request header, never added to the URL. It is
masked in F1 but stored as plain text in the BepInEx config. Keep that file
private. Redirects and invalid certificates are refused. Remote CSV names
must be plain text, with unique SteamID64 values. Full setup and limits:
https://github.com/baanish/NO-Client-Side-Renamer/blob/main/docs/USER_GUIDE.md#fetch-a-shared-csv-roster

Known limits

- Vanilla-peer isolation is unproven in both join directions.
- Host-local logs, ban-list labels, and third-party recordings may contain
  aliases even when vanilla peers do not receive them.
- Text rendered before a reload is not rewritten.
- Spectator coverage in the editor is unverified.

Source and full documentation:
https://github.com/baanish/NO-Client-Side-Renamer

MIT license. See LICENSE.txt.
