# Nuclear Option Client-Side Renamer

Use local callsigns for Nuclear Option players without changing their Steam identities. This BepInEx 5 plugin maps SteamID64 values to display aliases and applies them when the game resolves player names.

> **v0.1.0 test status:** Remote aliases have been observed on the modded client's tactical map, HUD marker, and aircraft MFD. Scoreboard and event-notification renaming have also been observed. A vanilla peer has not confirmed isolation in either join direction. **Use this private prerelease only for the controlled two-person test, not ordinary multiplayer.**

## Install

1. Install BepInEx 5 for Nuclear Option.
2. Copy `ClientSideRenamer.dll` to `BepInEx/plugins/ClientSideRenamer/`.
3. Start the game once.

The first run creates:

- `BepInEx/config/com.baanish.nuclearoption.clientsiderenamer.cfg`
- `BepInEx/config/ClientSideRenamer/aliases.json`

The JSON file is created with the private-test mapping `Baanish` to `Baanish | Reaper 5-2`. Name-only mappings are exact and case-sensitive, so the generated row will not match `baanish`. Saving the player through the F1 editor replaces that fallback with a stable SteamID64 mapping.

## Add an alias in-game

The in-game editor requires BepInEx Configuration Manager.

1. Join a match.
2. Open BepInEx Configuration Manager (`F1` by default) and expand **Nuclear Option Client-Side Renamer**.
3. Choose a player from the **Current match** dropdown.
4. Enter the alias and select **Save**.

The editor writes the alias file through a same-directory temporary file and invalidates the game's current player-name caches. The dropdown includes nonzero-SteamID players registered in the active match. Spectator coverage has not been verified.

Configuration Manager is optional. Without it, edit `aliases.json` directly and rely on best-effort file watching or a restart. With it, **Reload now** provides a manual reload.

## Documentation

- [User guide](docs/USER_GUIDE.md): installation, configuration, JSON editing, reload behavior, and troubleshooting.
- [Design](docs/DESIGN.md): architecture, deliberate decisions, constraints, and review questions.
- [Testing](docs/TESTING.md): current evidence, open multiplayer checks, and release gates.
- [Releasing](docs/RELEASING.md): reproducible packages, GitHub publication, and the eventual NOMM submission.
- [Changelog](CHANGELOG.md): versioned changes and known limits.

## Build

Pass the installed Nuclear Option directory as `GamePath`:

```powershell
dotnet test .\ClientSideRenamer.sln -p:GamePath='D:\SteamLibrary\steamapps\common\Nuclear Option'
dotnet build .\src\ClientSideRenamer\ClientSideRenamer.csproj -c Release -p:GamePath='D:\SteamLibrary\steamapps\common\Nuclear Option'
```

The release DLL is written to `src/ClientSideRenamer/bin/Release/netstandard2.1/`. Building does not copy files into the game directory.

## Scope and release gate

The plugin changes the name returned by a central game resolver. It does not modify Steam authentication or player IDs. The hook can reach multiple name consumers, but the host and server code share a process when a player hosts a match. Until the host-direction test passes, the project must not claim that aliases can never appear on a vanilla peer.

Lobby-browser titles are out of scope because they use separate Steam lobby metadata.

## License

[MIT](LICENSE)
