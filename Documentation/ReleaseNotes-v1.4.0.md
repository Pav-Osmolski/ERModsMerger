# ERModsMerger v1.4.0

v1.4.0 is a major compatibility, reliability and release-engineering update for ERModsMerger.

The multi-version regulation, historical compatibility, validation and hardening improvements in this release were developed by **DeViLhoOD**.

## Highlights

- Supports the complete bundled vanilla Elden Ring regulation archive from **1.00 through 1.17.1**.
- Migrates older mod changes onto the current regulation using an exact-version three-way merge.
- Uses version-aware ParamDefs and semantic PARAM row/field matching instead of positional assumptions.
- Verifies the installed-game, bundled and optional user-provided vanilla baselines with SHA-256.
- Blocks unsupported future game/mod regulation versions before changing output.
- Reports cross-mod PARAM conflicts with the lower/higher-priority sources and winning value.
- Handles row additions, deletions and priority overrides consistently.
- Writes merged regulation output transactionally and preserves the previous output as `regulation.bin.bak`.
- Adds regulation readiness information to the manager before a merge begins.
- Adds extensive archive, schema, semantic and real encrypted end-to-end migration tests.
- Modernises CI and release packaging around .NET 8.
- Makes `Assets/` the maintained source of truth and generates `Assets.zip` during builds instead of tracking it in Git.

## Multi-version regulation merging

For a mod built against an older game regulation, ERModsMerger now:

1. Detects the mod's raw BND regulation version.
2. Loads the exact vanilla regulation from that same version.
3. Calculates only the mod's semantic PARAM changes.
4. Starts from the currently installed, verified vanilla regulation.
5. Applies the mod delta by PARAM name, row ID and ParamDef field identity.

This prevents official FromSoftware changes between regulation versions from being mistaken for mod edits.

## Supported regulation archive

The bundled archive contains 34 verified vanilla regulations covering:

`1.00` → `1.17.1`

The archive manifest stores the raw regulation version, expected size and SHA-256 for each supported file.

All bundled historical baselines, optional user overrides and the currently installed game regulation are checked against known vanilla hashes before they can be used as merge baselines.

## Historical ParamDef compatibility

ParamDefs are now loaded with `FirstVersion` / `RemovedVersion` metadata enabled and filtered to the exact regulation version being read.

Additional historical layout handling covers known schema transitions across the early 1.01/1.02 line, 1.03, 1.11/1.12, and 1.17.x.

Parsed version-aware ParamDefs are cached per process to avoid reparsing the full XML set for every regulation.

## Conflict and priority handling

When two mods change the same PARAM row/field to different values, the merge log reports the conflict and identifies the winning higher-priority mod.

Row deletion/edit and deletion/add interactions now follow the same priority model. A higher-priority mod can restore the current vanilla row where needed before applying its own changes.

## Safety improvements

- Failed preflight aborts regulation merging before output is changed.
- Unsupported future regulation versions fail closed.
- Modified/non-vanilla baselines are rejected.
- Output is written to a temporary encrypted regulation first.
- The temporary result is reopened and its regulation version verified.
- The verified file atomically replaces the previous merged output.
- The previous output is preserved as `regulation.bin.bak`.

## Manager improvements

The WPF manager now exposes a regulation readiness summary showing:

- installed game regulation version;
- detected mod regulation versions;
- number of mods that require migration;
- baseline/support readiness.

Field-level merge conflicts remain available in the detailed merge log.

## Assets and maintenance

`Assets/` is now the source of truth.

`Assets.zip` is generated with:

```powershell
.\tools\Build-AssetsArchive.ps1
```

The complete regulation archive can be refreshed with:

```powershell
.\tools\Import-RegulationArchive.ps1 -ArchivePath "C:\path\to\ER Regulation Archive.zip"
```

Regulation assets can be independently verified with:

```powershell
.\tools\Test-RegulationAssets.ps1
```

## Testing and CI

The release pipeline:

- validates all 34 regulation assets;
- builds the solution on Windows / .NET 8 with maintained-project warnings treated as errors;
- validates every bundled regulation against its version-aware ParamDefs;
- runs semantic merge and priority/conflict tests;
- runs real encrypted end-to-end migration tests;
- runs three representative encrypted migrations during routine CI/release validation; the full 34-version migration sweep is available manually through the Deep Regulation Regression workflow;
- builds a compact Windows x64 package containing the manager, console merger, one shared `Assets.zip`, README and licence.

## Packaging

The release package is framework-dependent and requires the **.NET 8 Desktop Runtime x64**.

Both applications are published as Windows x64 single-file executables and share one generated `Assets.zip`.
