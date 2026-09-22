# Change Log

All notable ERModsMerger changes are recorded here. Current limitations and future ideas are kept separately in [Feedback and Roadmap](Feedback-and-Roadmap.md).

## v1.5.0 - 2026-09-22

### CSV PARAM merging

- Added first-class Smithbox-compatible CSV PARAM merging through a profile-specific `CSVToMerge` folder.
- Added separate **Merge Mods** and **Merge CSV** actions in the manager, plus `/mergecsv` in the console app.
- CSV filenames map directly to PARAM names.
- Supports comma, semicolon and tab separators.
- Requires `ID`; supports optional `Name`.
- Accepts Smithbox partial-export headers such as `ID,,attackBasePhysics`.
- Applies only supplied, non-blank fields.
- Warns and skips unknown/removed fields instead of shifting positional data.
- Adds missing row IDs using the current ParamDef defaults.
- Correctly persists rows added with only `ID` / `Name`.
- Rejects duplicate row IDs within a CSV.
- Supports Smithbox `dummy8` values.
- Applies top-level CSV source folders deterministically from lower to higher priority.
- Parses higher-priority CSVs against the evolving output so they can override lower-priority edits or restore values to vanilla.
- Reuses the semantic conflict tracker for cross-CSV field conflicts.
- Writes CSV merge output transactionally to `MergedMods/regulation.bin`.

### Folder and console fixes

- Fixed console first-launch setup so `ModsToMerge`, `CSVToMerge` and `MergedMods` are created only inside the active profile directory rather than duplicated beside the executables.
- Updated the interactive console introduction to explain both normal mod merging and CSV PARAM merging.
- Added regression coverage for the default profile folder layout.

### Multi-version merge hardening

- Hardened semantic row application when new rows are created from ParamDef defaults.
- Continued support for the bundled Elden Ring vanilla regulation archive from **1.00 through 1.17.1**.
- Retained exact-version baseline matching and version-aware ParamDefs.

### CI, validation and release engineering

- Separated slow real-regulation integration tests from routine semantic/unit coverage.
- Reduced routine semantic test time from several minutes to a few seconds.
- Added sparse pull-request checkout and use of the preinstalled .NET 8 SDK.
- Added targeted regulation asset validation.
- Kept encrypted regulation and exhaustive 34-version migration coverage in the manual **Deep Regulation Regression** workflow.
- Automatically prunes failed, cancelled, timed-out, skipped and stale workflow runs after healthy validation.
- Removed duplicate `Assets.zip` generation from release/deep workflows.
- Restructured the README as a user-facing project overview instead of a running changelog.

### Credits

The multi-version regulation support, CSV PARAM merging, hardening, validation and CI/release improvements in this fork were developed by **DeViLhoOD**.

Thanks to the original ERModsMerger author **MadTekN1**, SoulsMods, Smithbox, Nordgaren and the wider Souls modding community.

## v1.4.0 - 2026-09-19

### Multi-version regulation support

- Added the complete bundled vanilla Elden Ring regulation archive from **1.00 through 1.17.1**.
- Added exact-version three-way merging so mods built against older regulations can be migrated onto the currently installed vanilla regulation.
- Added version-aware ParamDefs and semantic PARAM row/field matching instead of positional assumptions.
- Added SHA-256 verification for installed, bundled and optional user-provided vanilla baselines.
- Added safe failure for unsupported future game/mod regulation versions.

### Conflict and merge semantics

- Added cross-mod PARAM conflict reporting with lower/higher-priority sources and the winning value.
- Hardened row additions, deletions and priority overrides.
- Added handling for deletion/edit and deletion/add interactions under the same priority model.

### Safety and output integrity

- Added preflight validation before output changes.
- Rejected modified/non-vanilla baselines.
- Added transactional regulation output through a temporary encrypted file.
- Reopens and verifies the temporary result before replacement.
- Preserves the previous merged output as `regulation.bin.bak`.

### Manager, assets and validation

- Added regulation readiness information to the manager.
- Made `Assets/` the maintained source of truth and generates `Assets.zip` during builds.
- Added archive, schema, semantic and encrypted end-to-end migration tests.
- Modernised CI and release packaging around .NET 8.
- Added the optional exhaustive 34-version deep regression workflow.

### Credits

The multi-version regulation, historical compatibility, validation and hardening work in v1.4.0 was developed by **DeViLhoOD**.
