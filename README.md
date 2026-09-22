# ERModsMerger

[![CI Build](https://github.com/Pav-Osmolski/ERModsMerger/actions/workflows/ci-build.yml/badge.svg?branch=main)](https://github.com/Pav-Osmolski/ERModsMerger/actions/workflows/ci-build.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Pav-Osmolski/ERModsMerger?display_name=tag&sort=semver)](https://github.com/Pav-Osmolski/ERModsMerger/releases/latest)

ERModsMerger is an Elden Ring mod manager and regulation merger. This fork extends the original project with safe multi-version `regulation.bin` merging, version-aware ParamDefs, conflict handling, transactional output, and Smithbox-compatible CSV PARAM importing.

## Features

- Merge multiple modded `regulation.bin` files while preserving mod priority.
- Migrate changes from older Elden Ring regulations onto the currently installed vanilla regulation.
- Supports the bundled vanilla regulation archive from **1.00 through 1.17.1**.
- Match PARAM edits semantically by row ID and field name instead of assuming identical layouts.
- Import Smithbox-style PARAM CSV exports directly into a clean vanilla regulation.
- Detect cross-mod / cross-CSV field conflicts and apply deterministic priority rules.
- Verify known vanilla baselines with SHA-256 and fail closed on unsupported future game versions.
- Write merged regulation output transactionally, with verification and `regulation.bin.bak` fallback.
- Use either the graphical **ERModsManager** or the console **ERModsMerger** workflow.
- Automate normal merges with `/merge` and CSV merges with `/mergecsv`.

## Quick start

### Manager

1. Download the latest release and extract it.
2. Launch `ERModsManager.exe`.
3. Select your Elden Ring `Game` folder if prompted.
4. Drag mod folders or archives into the manager.
5. Arrange priority so the highest-priority mod is at the top.
6. Select **Merge Mods**.
7. Launch the game through your normal ModEngine2 setup.

The manager performs regulation preflight checks before writing output and reports unsupported or missing baselines instead of producing a partial merge.

### CSV PARAM merging

Each profile has a `CSVToMerge` folder. Place Smithbox-style PARAM exports such as:

```text
CSVToMerge/
├─ EquipParamWeapon.csv
├─ SpEffectParam.csv
└─ My Mod/
   └─ NpcParam.csv
```

Then select **Merge CSV**.

CSV filenames map to PARAM names. `ID` is required; `Name` is optional. Only supplied, non-blank fields are applied. Missing rows are created from the **current** ParamDef so newer fields receive safe defaults.

For format details, source priority, partial exports and limitations, see [CSV PARAM merging](Documentation/CSVParamMerging.md).

## Multi-version regulation merging

When a mod was built for an older Elden Ring regulation, ERModsMerger performs an exact-version three-way merge:

1. Load the modded regulation and the vanilla baseline from the same raw regulation version.
2. Extract only the mod's actual row/field changes.
3. Apply those semantic changes to the current installed vanilla regulation.

This prevents FromSoftware's intervening balance and schema changes from being mistaken for mod edits.

Historical baselines are maintained under `Assets/Regulations` and extracted at runtime to:

```text
ERModsMergerConfig/Regulations/
```

Optional user-supplied baselines can be placed under:

```text
ERModsMergerConfig/VanillaRegulations/
```

User-supplied and installed-game vanilla regulations are checked against the known manifest before use. If a future Elden Ring patch is not yet supported, merging stops safely.

## ModEngine2

Using [ModEngine2](https://github.com/soulsmods/ModEngine2) is recommended. Point it at the generated `MergedMods` folder:

```toml
mods = [
    { enabled = true, name = "default", path = "MergedMods" }
]
```

## Console automation

```text
ERModsMerger.exe /merge
ERModsMerger.exe /mergecsv
```

- `/merge` merges normal mods from `ModsToMerge`.
- `/mergecsv` merges PARAM CSV files from `CSVToMerge`.

Both workflows produce `MergedMods/regulation.bin`.

## Important limitations

- Regulation merging only applies to `regulation.bin` internals. Other conflicting game files such as `.dcx`, `.emevd.dcx`, `.anibnd.dcx` or `.msb.dcx` are still resolved according to file priority rather than semantically merged.
- Normal mod merging and CSV PARAM merging are separate output workflows.
- CSV importing supports modification and row addition; row deletion is not represented by CSV input.
- A modded/unknown installed game regulation is intentionally rejected as the vanilla target.

## Requirements

- Windows
- Elden Ring
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- ModEngine2 recommended for loading `MergedMods`

## Documentation

- [CSV PARAM merging](Documentation/CSVParamMerging.md)
- [Code / architecture documentation](Documentation/CodeDoc.md)
- [Change log](Documentation/ChangeLog.md)

## Feedback and issues

Use [GitHub Issues](https://github.com/Pav-Osmolski/ERModsMerger/issues) for bug reports, feature requests and active development feedback. The old static feedback/to-do list has been retired so current work is not duplicated across multiple sources.

## Development and validation

Routine pull-request CI is intentionally fast: it performs a sparse source checkout, compiles the full solution, and runs the semantic merge + CSV unit tests. Expensive real-regulation compatibility and encrypted end-to-end migration checks are isolated in the manual **Deep Regulation Regression** workflow, which can exercise the complete 34-version archive.

Release and main builds retain full asset validation and package generation.

## Credits & thanks

- **DeViLhoOD** — multi-version regulation support, CSV PARAM merging, historical compatibility work, merge hardening, validation, CI/release improvements and ongoing maintenance of this fork.
- [MadTekN1](https://github.com/MadTekN1/ERModsMerger) — original ERModsMerger.
- [SoulsMods](https://github.com/soulsmods)
- [Smithbox](https://github.com/vawser/Smithbox)
- [Nordgaren](https://github.com/Nordgaren)
- The wider Souls modding community.
