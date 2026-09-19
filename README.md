This fork updates ERModsMerger for current Elden Ring versions and adds safer multi-version regulation merging.

## v1.4.0 highlights

v1.4.0 is a major reliability and compatibility update focused on regulation merging across Elden Ring versions.

- Supports the complete bundled vanilla regulation archive from **1.00 through 1.17.1**.
- Migrates older mod changes onto the current regulation using an exact-version three-way merge.
- Uses version-aware ParamDefs and semantic row/field matching instead of positional assumptions.
- Verifies bundled, user-supplied and installed-game vanilla baselines with SHA-256.
- Detects unsupported future game versions and fails closed before changing output.
- Reports cross-mod PARAM field conflicts and preserves the configured priority order.
- Writes merged regulations transactionally with verification and a `regulation.bin.bak` fallback.
- Includes manager-side regulation readiness information before merging.
- Adds exhaustive archive/ParamDef validation, semantic tests and real encrypted end-to-end migration tests.
- Modernises CI/release packaging and generates `Assets.zip` from the maintained `Assets/` source tree.

These multi-version regulation, validation, hardening and release improvements were developed by **DeViLhoOD**.

[Full v1.4.0 release notes](Documentation/ReleaseNotes-v1.4.0.md)

## Multi-version regulation support

ERModsMerger can merge a mod built against an older Elden Ring regulation into the currently installed regulation without treating FromSoftware's intervening balance/schema changes as mod edits.

For a mod whose regulation version differs from the installed game, ERModsMerger performs a three-way merge:

1. Compare the modded regulation against a vanilla regulation from the **same exact raw regulation version**.
2. Extract only the mod's actual row/field changes.
3. Apply those changes by row ID and ParamDef field name to the current installed vanilla regulation.

Historical vanilla baselines are maintained in `Assets/Regulations`. On startup, the bundled baselines are extracted to:

```text
ERModsMergerConfig\Regulations\
```

The installed game's `regulation.bin` always wins for the current version. Optional user-supplied baselines under `ERModsMergerConfig\VanillaRegulations\` can supplement or override historical bundled versions. Filenames do not determine compatibility: ERModsMerger reads the raw BND regulation version from each file.

Before merging, a preflight reports every historical version required by the selected mods and flags missing, unreadable or unsupported baselines before any output is changed. If preflight fails, regulation merging is aborted rather than producing a partial result.

User-supplied historical baselines are SHA-256 checked against the known vanilla manifest before they can override bundled data. An installed game regulation that is not in the tested manifest is also blocked, so a future Elden Ring patch cannot be merged until its regulation and ParamDefs have been added and validated.

When two mods change the same PARAM row/field to different values, the merge log reports the conflict and the later processed (higher-priority) mod wins. Row deletion/edit conflicts are handled consistently with the same priority model. The manager also shows a lightweight regulation readiness strip with the installed game version, detected mod versions, migration count and baseline/support state.

Merged regulation output is written transactionally: the temporary encrypted result is reopened and version-checked before replacing the previous output. When a previous merged regulation exists, it is preserved as `regulation.bin.bak`.

### Maintaining the regulation archive

`Assets/Regulations/manifest.json` records the complete 1.00 through 1.17.1 archive with the raw regulation version, expected file size and SHA-256 for every baseline. The source archive is TKGP's [ER Regulation Archive](https://www.nexusmods.com/eldenring/mods/4262).

To refresh the bundled assets from a complete archive:

```powershell
.\tools\Import-RegulationArchive.ps1 -ArchivePath "C:\path\to\ER Regulation Archive.zip"
```

The importer normalises folder names and verifies all 34 files against the manifest. `Assets/` is the source of truth; `Assets.zip` is generated from it by `tools/Build-AssetsArchive.ps1`, ignored by Git, and validated in CI. This avoids large binary archive churn in Git history and makes it impossible for a committed ZIP to become stale.

Release/CI packaging uses `tools/Build-ReleasePackage.ps1` to publish both executables as framework-dependent single files and ship one shared `Assets.zip` beside them. Tagging `v*` runs the tested release workflow and publishes the resulting ZIP; the same workflow can be run manually to produce a prerelease package without creating a GitHub release.

### Validation and regression coverage

CI runs on Windows with .NET 8, treats maintained-project warnings as errors, validates the complete 34-version regulation archive, and tests every bundled regulation against the version-aware ParamDefs. It also runs semantic merge unit tests plus an end-to-end migration matrix that decrypts historical regulations, creates controlled mod edits, re-encrypts them, migrates them into the current baseline, saves transactionally and reloads the result.

# Elden Ring Mods Manager - Merger
Simple tool to manage and merge Elden Ring mods. Work In Progress.

Can only merge regulation.bin files for now (every other files will be overwrited depending of priority order), more merging capabilities will be added in the future.

## Usage

![](https://github.com/MadTekN1/ERModsMerger/blob/main/Documentation/Images/Manager%20Demo.gif?raw=true)
- Place ERModsManager.exe wherever you want. Desktop for example.
- If the app ask you the game path at launch, just navigate to where eldenring.exe is.
- The fun part now, drag and drop your mods (can be .zip or folder) directly in the app (don't worry, the app will most likely handle it)
- Then define mods priority by dragging them up or down in the list (top is highest)
- Press Merge and wait the logs telling you the merge is done.
- Press Play & Enjoy!

# Elden Ring Mods Merger (Console App)

Console App version with merging and automation features.

## Usage
![](https://github.com/MadTekN1/ERModsMerger/blob/main/Documentation/Images/Console%20Merger%20Demo.gif?raw=true)

Highly recommended: Use [ModEngine2](https://github.com/soulsmods/ModEngine2), place ERModsMerger.exe in the same folder and edit config_eldenring.toml as follow:
```
mods = [
    { enabled = true, name = "default", path = "MergedMods" }
]
```
Launch ERModsMerger.exe and let it guide you through the process, it will self extract and create all the folders you need, it will also give you an example and explanations to make it easy.

## Troubleshooting & Solutions

- ERModsMerger.exe don't launch: Make sure [.Net 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.6-windows-x64-installer) is installed on your system.

- Fail to load config or could not locate regulation bin: This likely happen when ERModsMergerConfig\\config.json is modified with invalid values / format. Here is an example of a modified config.json:

	```json
	{
	  "GamePath": "C:\\New\\path\\to the folder of\\ELDEN RING\\Game",
	  "ModsToMergeFolderPath": "ModsToMerge",
	  "MergedModsFolderPath": "MergedMods"
	}
	```
  * Respect the format presented above and dont forget to add double `\\` between each folders in paths.
  

- Vanilla game/modded regulation.bin doesn't load: Check the preflight/merge log for the raw regulation version and ParamDef compatibility. Bundled baselines are extracted under `ERModsMergerConfig\\Regulations\\`; optional historical overrides can be placed under `ERModsMergerConfig\\VanillaRegulations\\`.

- Game don't launch, is buggy or mods are missing: Merges can cause some troubles depending of the overwrited files, also this tool only merge internal values of regulation.bin files (for now) and overwrite fields in benefits to the highest priority order. Any other individual conflicting files (eg: emevd.dcx anibnd.dcx msb.dcx etc..) will be overwrited using the same system of priority and potentially causing more troubles.

For now it's better to use this tool to merge mods who only have conflicting regulation.bin files and no .dcx individual files conflicts.

## Automation

Run the console app with /merge argument to automatically merge mods located inside ModsToMerge to MergedMods folder, no user interaction will be asked and the console will close after the merge.﻿﻿

## Contributing

If you wish to contribute to this project, you are very welcome. The [code documentation](Documentation/CodeDoc.md) explains the current architecture, multi-version regulation pipeline, asset maintenance, testing and release workflow. Enjoy coding :)

## Credits & Thanks
* **DeViLhoOD** - multi-version regulation support, historical compatibility work, merge hardening, validation, CI/release improvements and ongoing maintenance of this fork.
* [SoulsMods](https://github.com/soulsmods)
* [Smithbox](https://github.com/vawser/Smithbox)
* [Nordgaren](https://github.com/Nordgaren)
* Also big Thanks to all the Souls [modding community](https://discord.gg/servername)
