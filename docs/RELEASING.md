# Releasing

This project publishes test builds as GitHub prereleases until host-mode isolation passes. The release process builds from the working source, runs the focused suite, creates two packages, validates their layout, and records SHA-256 checksums.

## Build the release packages

Commit the reviewed release source without creating the tag. Confirm the working tree is clean, close Nuclear Option, then run this command from the repository root:

```powershell
pwsh -NoProfile -File .\build\Release.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\Nuclear Option'
```

The script refuses a dirty working tree by default. It reads the version from `ClientSideRenamer.csproj` and verifies that `Plugin.PluginVersion` and the built assembly match it. It then performs a clean Release build and runs the tests without rebuilding.

The script writes these ignored artifacts:

```text
artifacts/
  ClientSideRenamer-v0.1.0-nomm.zip
  ClientSideRenamer-v0.1.0-plugin-only.zip
  SHA256SUMS.txt
```

The NOMM archive is flat:

```text
ClientSideRenamer.dll
LICENSE.txt
README.txt
```

The plugin-only archive can be merged beside `NuclearOption.exe`:

```text
BepInEx/plugins/ClientSideRenamer/ClientSideRenamer.dll
BepInEx/plugins/ClientSideRenamer/LICENSE.txt
BepInEx/plugins/ClientSideRenamer/README.txt
```

Neither archive contains game assemblies, BepInEx, Configuration Manager, generated config, debug symbols, or the test output.

## Publish the GitHub prerelease

Review the source diff and [release notes](release-notes/v0.1.0.md) before committing. Build the packages from that clean commit. Create the annotated `v0.1.0` tag only after build, test, and package validation pass. Push the commit and tag, then publish a GitHub prerelease with these assets in this order:

1. `ClientSideRenamer-v0.1.0-nomm.zip`
2. `ClientSideRenamer-v0.1.0-plugin-only.zip`
3. `SHA256SUMS.txt`

The NOMM archive must be the first uploaded asset because the registry uses the first release asset as the default package when a release contains several assets.

Read the commit, tag, release flags, asset names, asset sizes, and checksums back from GitHub after publication. A successful upload is not enough evidence by itself.

## Prepare for Nuclear Option Mod Manager

NOMM reads its catalog from the [NOMNOM registry](https://github.com/KopterBuzz/NOMNOM). Its current [submission policy](https://github.com/KopterBuzz/NOMNOM#how-to-add-your-nuclear-option-mod-to-nomnom) requires open source for mods that ship a custom DLL. It also requires a GitHub release package, one mod per repository, a parseable version tag, and BepInEx 5 compatibility.

The [manifest schema](https://github.com/KopterBuzz/NOMNOM/blob/main/SCHEMA.md) uses the assembly name as the preferred mod ID. The future manifest should use:

| Field | Planned value |
| --- | --- |
| `id` | `ClientSideRenamer` |
| `displayName` | `Nuclear Option Client-Side Renamer` |
| `githubOwner` | `baanish` |
| `githubRepoName` | `NO-Client-Side-Renamer` |
| `autoUpdateArtifacts` | `True` |
| artifact `type` | `plugin` |
| artifact `category` | `preRelease` until the isolation gate passes |
| artifact `fileName` | `ClientSideRenamer-v0.1.0-nomm.zip` |
| artifact `gameVersion` | `0.34.2` for this test build |

Do not submit the manifest while the repository is private. Before submission:

1. Pass both vanilla-peer isolation directions in [Testing](TESTING.md).
2. Remove the enabled personal starter mapping from the generated template.
3. Make the source and matching release commit public.
4. Confirm the GitHub release asset is built from that public source.
5. Fork NOMNOM, add `modManifests/ClientSideRenamer.json`, and open a pull request to its `main` branch.

NOMNOM supports a prerelease category, but its `isClientOrServer` value still makes a behavior claim. Set it to `Client` only after the host-direction isolation test passes.
