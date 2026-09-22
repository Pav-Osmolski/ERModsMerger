# Feedback and Roadmap

This document is for current limitations and possible future work. It is **not** a changelog; completed release history belongs in [ChangeLog.md](ChangeLog.md).

## Current limitations

- **Mod merging and CSV PARAM merging are separate workflows.** Each operation starts from the verified current vanilla regulation and writes its own `MergedMods/regulation.bin`.
- **CSV input does not represent row deletion.** CSV merging currently supports field changes and row additions.
- **Only `regulation.bin` conflicts are merged semantically.** Other conflicting game files such as map, event, animation or parts files still resolve by mod/file priority.
- **Unknown future Elden Ring regulation versions fail closed.** A new game update may require the vanilla baseline/manifest and compatibility data to be updated before merging is allowed.
- **CSV files are matched to PARAMs by filename.** Export names must therefore remain clear and unambiguous.

## Possible future improvements

These are ideas, not commitments or scheduled work:

- Optional combined workflow that layers normal mod regulation changes and CSV PARAM changes in one output operation.
- Clearer manager status for which merge mode most recently produced `MergedMods/regulation.bin`.
- More detailed conflict review/filtering in the manager UI.
- Additional CSV quality-of-life features if they remain compatible with Smithbox exports and deterministic merging.

## Feedback

For now, feedback can be raised through pull requests or discussion around the repository/releases. This document should only contain items that are still relevant to the current codebase.

When GitHub Issues or Discussions are enabled, active bug reports and feature requests should move there rather than duplicating them here.
