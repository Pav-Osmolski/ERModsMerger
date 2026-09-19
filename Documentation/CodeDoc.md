# Code Documentation

This document gives contributors a high-level map of ERModsMerger and the regulation merge pipeline.

## Structure

The solution is divided into four main projects:

- `ERModsMerger.Core` contains merge orchestration, format handling, configuration and validation.
- `ERModsManager` is the WPF manager application.
- `ERModsMerger` is the console application.
- `ERModsMerger.Core.Tests` contains semantic, archive-compatibility and end-to-end regulation tests.

`SoulsFormats` is vendored and used for Elden Ring binary formats.

## Merge flow

### `ModsMerger`

`ModsMerger.StartMerge()` is the main Core entry point.

It loads the active configuration/profile, discovers enabled mod files, passes them to `MergeableFilesDispatcher`, resolves conflicts according to configured priority and writes the merged output.

### `MergeableFilesDispatcher`

The dispatcher groups files by relative path and selects the appropriate merge strategy.

Important responsibilities include:

- collecting `FileToMerge` entries;
- detecting file conflicts;
- preserving configured mod priority;
- dispatching `regulation.bin` conflicts to the regulation merger;
- copying/overwriting unsupported file formats according to priority.

## Regulation merging

The regulation implementation lives under `ERModsMerger.Core/Formats`.

### Three-way migration

Cross-version regulation merging is deliberately performed as a three-way merge:

1. Load the modded regulation.
2. Load the **exact vanilla regulation version** that the mod was built against.
3. Calculate the semantic delta between the mod and that exact vanilla baseline.
4. Start from the installed game's verified current vanilla regulation.
5. Apply only the semantic mod delta to the current regulation.

This prevents official FromSoftware changes between versions from being misidentified as mod changes.

### `RegulationBin`

`RegulationBin` owns encrypted regulation I/O and PARAM loading.

It:

- decrypts/encrypts Elden Ring `regulation.bin`;
- records the raw BND regulation version;
- applies the correct version-filtered ParamDef for that raw version;
- delegates semantic comparison/application to `RegulationMergeEngine`;
- saves merged output transactionally;
- verifies the temporary encrypted output before replacing the previous result.

An existing merged output is preserved as `regulation.bin.bak`.

### `RegulationParamDefCatalog`

Version-aware ParamDef XML files are parsed once per asset set and cached for the lifetime of the process. Each regulation receives its own dictionary view of the cached definitions.

Historical `FirstVersion` and `RemovedVersion` metadata is retained and filtered for the exact raw regulation version before applying a ParamDef.

### `RegulationParamDefCompatibility`

Some historical PARAMs use older data-version metadata even when their physical row layout is compatible. This helper permits that mismatch only when the PARAM type and row size still match exactly.

It must not be loosened to force incompatible schemas.

### `RegulationMergeEngine`

This class contains the testable semantic delta/apply algorithm.

Rows are identified by row ID. Cells are identified by ParamDef internal field name plus duplicate-name occurrence, with a conservative positional fallback.

The engine supports:

- modified rows;
- added rows;
- deleted rows;
- fields introduced or removed by newer regulations;
- row-order changes;
- high-ID row insertion;
- duplicate-row safeguards;
- priority restoration when a higher-priority edit/add follows a lower-priority deletion.

### `RegulationConflictTracker`

The tracker records changes already applied by lower-priority mods.

If a later/higher-priority mod changes the same PARAM / row ID / field to a different value, the conflict is logged with both sources and both values. The later processed mod wins, matching the manager's priority model.

Deletion/edit and deletion/add conflicts are reported at row level.

### Baseline trust and future-version guard

`Assets/Regulations/manifest.json` is the compatibility authority.

For every supported version it records:

- normalised asset folder;
- raw BND regulation version;
- expected file size;
- SHA-256.

The current installed regulation, bundled historical regulations and optional user overrides must match the known vanilla hash for their raw version.

A regulation version absent from the manifest is unsupported and regulation merging fails closed until its vanilla baseline and compatible ParamDefs have been added and validated.

### Preflight and manager status

`RegulationMergePreflight` scans all selected mod regulations before merge output is changed.

It reports unreadable regulations, unsupported versions and missing exact-version baselines. A failed preflight aborts regulation merging.

`RegulationStatusService` exposes a lightweight version/baseline summary to the WPF manager so users can see the installed regulation, selected mod versions and migration readiness before starting a merge.

## Regulation assets

The maintained source of truth is:

```text
Assets/
  ParamDefs/
  Regulations/
    manifest.json
    1.00.0/regulation.bin
    ...
    1.17.1/regulation.bin
```

`Assets.zip` is generated and is intentionally not tracked in Git.

### Build the archive

```powershell
.\tools\Build-AssetsArchive.ps1
```

### Import/update the regulation archive

```powershell
.\tools\Import-RegulationArchive.ps1 -ArchivePath "C:\path\to\ER Regulation Archive.zip"
```

The importer validates every source file against the manifest before accepting it.

### Validate assets

```powershell
.\tools\Test-RegulationAssets.ps1
```

CI generates `Assets.zip` first and then verifies the loose regulation files and packaged copies.

## Tests

`ERModsMerger.Core.Tests` includes:

- semantic merge unit tests;
- priority/conflict tests;
- historical ParamDef compatibility tests;
- validation of all 34 bundled vanilla regulations;
- real encrypted end-to-end migration tests.

Routine CI and releases hash/package-validate all 34 bundled regulation files, then fully decrypt/schema-test and migrate three representative versions (`1.00.0`, `1.12.1`, and `1.16.1`). The **Deep Regulation Regression** workflow is available on demand for exhaustive all-34 ParamDef compatibility and encrypted migration testing.

## Build and release

The repository targets .NET 8 and is pinned by `global.json`.

CI:

1. generates `Assets.zip`;
2. validates the regulation archive;
3. restores/builds the solution;
4. treats maintained-project warnings as errors;
5. runs tests;
6. builds a compact release package;
7. uploads the package as a workflow artifact.

`tools/Build-ReleasePackage.ps1` publishes the console and manager as framework-dependent Windows x64 single-file executables and packages them with one shared `Assets.zip`, README and licence.

Tags matching `v*` invoke `.github/workflows/release.yml`, which repeats the full validation pipeline and publishes the resulting ZIP as a GitHub release.

## Applications

### ERModsMerger

The console application delegates merge work to Core. It also supports the `/merge` automation argument.

### ERModsManager

The WPF application provides profiles, drag-and-drop mod management, priority ordering, logs, configuration and regulation readiness information.

## Credits

The multi-version regulation support, historical compatibility work, validation/hardening and modern release pipeline in this fork were developed by **DeViLhoOD**.
