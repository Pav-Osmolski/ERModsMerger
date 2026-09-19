using ERModsMerger.Core.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ERModsMerger.Core.Formats
{
    internal sealed class RegulationConflictTracker
    {
        private readonly Dictionary<CellKey, CellState> _cells = new();
        private readonly Dictionary<RowKey, RowState> _rows = new();

        public IReadOnlyList<RegulationConflict> Observe(string source, IEnumerable<ParamRowToMerge> changes)
        {
            var conflicts = new List<RegulationConflict>();

            foreach (ParamRowToMerge change in changes)
            {
                var rowKey = new RowKey(change.ParamKey, change.RowID);

                if (_rows.TryGetValue(rowKey, out RowState? previousRow) &&
                    (previousRow.ChangeType == RowChangeType.Deleted || change.ChangeType == RowChangeType.Deleted) &&
                    previousRow.ChangeType != change.ChangeType)
                {
                    conflicts.Add(new RegulationConflict(
                        change.ParamKey,
                        change.RowID,
                        "<row>",
                        previousRow.Source,
                        source,
                        previousRow.ChangeType.ToString(),
                        change.ChangeType.ToString()));
                }

                _rows[rowKey] = new RowState(source, change.ChangeType);

                if (change.ChangeType == RowChangeType.Deleted)
                    continue;

                foreach (ParamCellChange cell in change.Cells)
                {
                    var key = new CellKey(
                        change.ParamKey,
                        change.RowID,
                        cell.Identity.FieldName,
                        cell.Identity.Occurrence);

                    if (_cells.TryGetValue(key, out CellState? previous) &&
                        !Utils.AdvancedEquals(previous.Value, cell.Value))
                    {
                        conflicts.Add(new RegulationConflict(
                            change.ParamKey,
                            change.RowID,
                            cell.Identity.FieldName,
                            previous.Source,
                            source,
                            FormatValue(previous.Value),
                            FormatValue(cell.Value)));
                    }

                    _cells[key] = new CellState(source, CloneValue(cell.Value));
                }
            }

            return conflicts;
        }

        private static object CloneValue(object value)
            => value is byte[] bytes ? bytes.ToArray() : value;

        private static string FormatValue(object value)
        {
            if (value is byte[] bytes)
                return Convert.ToHexString(bytes);
            return value?.ToString() ?? "<null>";
        }

        private readonly record struct CellKey(string ParamKey, int RowId, string FieldName, int Occurrence);
        private readonly record struct RowKey(string ParamKey, int RowId);
        private sealed record CellState(string Source, object Value);
        private sealed record RowState(string Source, RowChangeType ChangeType);
    }

    internal sealed record RegulationConflict(
        string ParamKey,
        int RowId,
        string FieldName,
        string PreviousSource,
        string WinningSource,
        string PreviousValue,
        string WinningValue);
}
