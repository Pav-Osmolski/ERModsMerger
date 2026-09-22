# ERModsMerger v1.5.0

v1.5.0 adds first-class **Smithbox-compatible CSV PARAM merging** alongside the existing multi-version `regulation.bin` workflow, and significantly tightens validation, workflow reliability and release engineering.

## CSV PARAM merging

A new `CSVToMerge` folder is created per profile. CSV files can be placed directly in it or grouped under top-level source folders.

- Map `<ParamName>.csv` directly to the matching PARAM.
- Support comma, semicolon and tab separators.
- Require `ID`; support optional `Name`.
- Accept Smithbox partial-export headers such as `ID,,attackBasePhysics`.
- Apply only fields that are present and non-blank.
- Warn and skip removed/unknown fields instead of shifting positional data.
- Add missing row IDs using the **current** ParamDef defaults.
- Persist rows added with only `ID` / `Name` correctly.
- Reject duplicate row IDs within a CSV.
- Support Smithbox `dummy8` values.
- Apply source folders deterministically from lower to higher priority.
- Parse higher-priority CSVs against the evolving output so they can explicitly override lower-priority edits, including restoring a value back to vanilla.
- Reuse the semantic conflict tracker for cross-CSV field conflicts.
- Write output transactionally to `MergedMods/regulation.bin`.

The manager now exposes separate **Merge Mods** and **Merge CSV** actions. The console app adds `/mergecsv`.

See [CSV PARAM merging](CSVParamMerging.md) for the complete format and priority rules.

## Multi-version merge hardening

v1.5.0 builds on the v1.4 multi-version architecture and includes additional regression fixes around semantic row application, especially when new rows are created from ParamDef defaults.

The bundled regulation archive continues to cover Elden Ring **1.00 through 1.17.1**, with exact-version baseline matching and version-aware ParamDefs.

## CI and release improvements

The repository's Actions setup has been substantially simplified and hardened:

- Draft/WIP development no longer launches a full build for every incremental commit.
- Routine PR semantic tests were reduced from minutes to seconds by separating real-regulation integration coverage from fast unit coverage.
- Routine PR validation now uses sparse checkout and the preinstalled .NET 8 SDK.
- Regulation asset validation has its own targeted workflow.
- Full encrypted regulation and 34-version migration coverage remains available through **Deep Regulation Regression**.
- Failed, cancelled, timed-out, skipped and stale workflow runs are automatically pruned after a healthy validation run.
- Main/release builds retain complete asset validation and packaging.
- Duplicate `Assets.zip` generation was removed from release/deep workflows.

## Documentation

The README has been restructured as a user-facing project overview rather than a rolling changelog. Version-specific details now live in dedicated release notes.

## Credits

The multi-version regulation support, CSV PARAM merging, hardening, validation and CI/release improvements in this fork were developed by **DeViLhoOD**.

Thanks to the original ERModsMerger author **MadTekN1**, SoulsMods, Smithbox, Nordgaren and the wider Souls modding community.
