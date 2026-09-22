[![CI Build](https://github.com/Pav-Osmolski/ERModsMerger/actions/workflows/ci-build.yml/badge.svg?branch=main)](https://github.com/Pav-Osmolski/ERModsMerger/actions/workflows/ci-build.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Pav-Osmolski/ERModsMerger?display_name=tag&sort=semver)](https://github.com/Pav-Osmolski/ERModsMerger/releases/latest)

This fork updates ERModsMerger for current Elden Ring versions with safe multi-version regulation merging and Smithbox-compatible CSV PARAM importing.

## v1.5.0 WIP: CSV PARAM merging

v1.5.0 adds a second regulation workflow for PARAM changes distributed as Smithbox-style CSV files.

- Every profile gets a new `CSVToMerge` folder.
- Drop `<ParamName>.csv` exports such as `EquipParamWeapon.csv` or `SpEffectParam.csv` directly into the folder, or organise them under top-level source folders.
- Supports comma, semicolon and tab separators.
- Existing rows only change fields explicitly present and non-blank in the CSV.
- Missing row IDs are added using the **current** ParamDef so newer fields retain safe defaults.
- Removed/unknown fields are warned and skipped rather than shifting positional data.
- Cross-CSV PARAM conflicts reuse the existing semantic conflict tracker and deterministic priority rules.
- Output is written transactionally to `MergedMods\regulation.bin`.
- Manager: **Merge Mods** and **Merge CSV** are separate actions.
- Console automation: `/mergecsv`.

[CSV PARAM merging documentation](Documentation/CSVParamMerging.md)

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

Routine pull-request CI uses a sparse source checkout, the .NET 8 SDK already present on GitHub's Windows runner (verified through `global.json`), a full solution compile with runtime asset packaging disabled, and only the fast semantic merge + CSV unit tests. Main/release builds keep the complete runtime assets and regulation validation. Real encrypted regulation compatibility and end-to-end migration tests remain intentionally isolated in the manual **Deep Regulation Regression** workflow, including the exhaustive 34-version migration matrix.

# Elden Ring Mods Manager - Merger
Simple tool to manage and merge Elden Ring mods. Work In Progress.

ERModsMerger can merge conflicting regulation.bin files and can also build a regulation.bin from Smithbox-style PARAM CSV exports. Other conflicting game files are still overwritten according to priority.

## Usage

![](https://github.com/MadTekN1/ERModsMerger/blob/main/Documentation/Images/Manager%20Demo.gif?raw=true)
- Place ERModsManager.exe wherever you want. Desktop for example.
- If the app ask you the game path at launch, just navigate to where eldenring.exe is.
- The fun part now, drag and drop your mods (can be .zip or folder) directly in the app (don't worry, the app will most likely handle it)
- Then define mods priority by dragging them up or down in the list (top is highest)
- Press **Merge Mods** for the normal mod workflow, or place PARAM CSV exports in the active profile's `CSVToMerge` folder and use **Merge CSV**.
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
	  "CSVToMergeFolderPath": "CSVToMerge",
	  "MergedModsFolderPath": "MergedMods"
	}
	```
  * Respect the format presented above and dont forget to add double `\\` between each folders in paths.
  

- Vanilla game/modded regulation.bin doesn't load: Check the preflight/merge log for the raw regulation version and ParamDef compatibility. Bundled baselines are extracted under `ERModsMergerConfig\\Regulations\\`; optional historical overrides can be placed under `ERModsMergerConfig\\VanillaRegulations\\`.

- Game don't launch, is buggy or mods are missing: Merges can cause some troubles depending of the overwrited files, also this tool only merge internal values of regulation.bin files (for now) and overwrite fields in benefits to the highest priority order. Any other individual conflicting files (eg: emevd.dcx anibnd.dcx msb.dcx etc..) will be overwrited using the same system of priority and potentially causing more troubles.

For now it's better to use this tool to merge mods who only have conflicting regulation.bin files and no .dcx individual files conflicts.

## Automation

Run the console app with `/merge` to automatically merge normal mods from `ModsToMerge`. Use `/mergecsv` to merge Smithbox-style PARAM CSV files from `CSVToMerge` into `MergedMods\\regulation.bin`.

## Contributing

If you wish to contribute to this project, you are very welcome. The [code documentation](Documentation/CodeDoc.md) explains the current architecture, multi-version regulation pipeline, asset maintenance, testing and release workflow. Enjoy coding :)

## Credits & Thanks
* **DeViLhoOD** - multi-version regulation support, CSV PARAM merging, historical compatibility work, merge hardening, validation, CI/release improvements and ongoing maintenance of this fork.
* [SoulsMods](https://github.com/soulsmods)
* [Smithbox](https://github.com/vawser/Smithbox)
* [Nordgaren](https://github.com/Nordgaren)
* Also big Thanks to all the Souls [modding community](https://discord.gg/servername)
