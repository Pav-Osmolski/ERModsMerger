# CSV PARAM Merging

ERModsMerger v1.5.0 adds a second regulation workflow for users who have PARAM changes exported as CSV rather than a complete modded `regulation.bin`.

## Folder layout

Every profile now has a `CSVToMerge` folder beside `ModsToMerge` and `MergedMods`.

```text
Profile/
├── ModsToMerge/
├── CSVToMerge/
└── MergedMods/
    └── regulation.bin
```

Smithbox-exported CSV files may be placed directly in `CSVToMerge` or grouped into top-level source folders.

```text
CSVToMerge/
├── My Weapon Tweaks/
│   └── EquipParamWeapon.csv
├── Better Status Effects/
│   └── SpEffectParam.csv
└── Bullet.csv
```

The CSV filename must match the PARAM name. Smithbox already uses this convention when exporting PARAM CSV files.

## Supported CSV format

ERModsMerger follows Smithbox's standard PARAM CSV layout:

```text
ID,Name,<ParamDef InternalName>,<ParamDef InternalName>,...
```

Example:

```csv
ID,Name,attackBasePhysics,attackBaseMagic
1000000,Sword,125,0
```

Supported separators are comma, semicolon and tab; the separator is detected from the header.

The `Name` column is optional. Smithbox's partial-export form with an empty placeholder column, such as `ID,,attackBasePhysics`, is also accepted.

Row names are used when creating new rows. Existing-row gameplay changes are matched and applied through ParamDef field columns, so row-name metadata does not create a PARAM conflict by itself.

## Merge semantics

CSV import is applied against the verified current vanilla `regulation.bin`.

For an existing row:

- only fields whose columns are present in the CSV are considered;
- blank cells are ignored;
- unchanged values are ignored;
- fields absent from the current ParamDef are warned and skipped.

For a row ID that does not exist in the current regulation:

- a new row is created using the current ParamDef;
- CSV-provided values are applied;
- fields not supplied by the CSV retain current ParamDef defaults.

CSV import does not invent row deletion syntax. Row deletion remains available through normal `regulation.bin` merging.

## Priority and conflicts

Top-level source folders are processed using the same deterministic console convention as `ModsToMerge`.

Alphabetically earlier source names have higher priority because they are processed last.

If two CSV sources change the same PARAM / row ID / field to different values, ERModsMerger logs the conflict and the higher-priority source wins.

A single CSV file may not contain the same row ID more than once; duplicate row IDs are rejected as ambiguous.

## Manager

Use **Merge CSV Params** to generate `MergedMods\regulation.bin` from the current verified vanilla regulation plus the CSV changes.

The Settings panel includes **Open CSVToMerge folder** for the active profile.

The existing **Merge Mods** action remains unchanged and continues to merge normal mod folders / `regulation.bin` files.

These are separate output workflows in v1.5.0. Running **Merge CSV Params** writes a CSV-derived `MergedMods\regulation.bin`; it does not automatically combine CSV changes with an already merged modded regulation.

## Console

Interactive mode offers:

- `M` — merge normal mods;
- `C` — merge CSV PARAM files.

Automation:

```text
ERModsMerger.exe /mergecsv
```

This reads CSV files from the active profile's `CSVToMerge` folder and writes the resulting regulation to `MergedMods\regulation.bin`.

## Safety

CSV merging reuses the same hardened regulation infrastructure as normal regulation merging:

- the installed game regulation must be a known supported vanilla file;
- its SHA-256 must match the regulation manifest;
- the current version-aware ParamDefs are used;
- semantic field identities use ParamDef internal names;
- output is written transactionally and reopened/version-verified;
- an existing output may be preserved as `regulation.bin.bak`.
