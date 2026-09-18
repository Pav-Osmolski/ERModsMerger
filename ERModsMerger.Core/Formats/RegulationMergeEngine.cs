using ERModsMerger.Core.Utility;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ERModsMerger.Core.Formats
{
    internal static class RegulationMergeEngine
    {
        public static List<ParamRowToMerge> FindRowsToMerge(
            Dictionary<string, PARAM> moddedParams,
            Dictionary<string, PARAM> vanillaParams,
            Action<string, LOGTYPE>? log = null)
        {
            var rowsToMerge = new List<ParamRowToMerge>();

            foreach ((string paramKey, PARAM moddedParam) in moddedParams)
            {
                if (!vanillaParams.TryGetValue(paramKey, out PARAM? vanillaParam))
                {
                    log?.Invoke($"Skipped {paramKey}: it is not present in the exact-version vanilla baseline", LOGTYPE.WARNING);
                    continue;
                }

                Dictionary<int, List<PARAM.Row>> vanillaRows = vanillaParam.Rows
                    .GroupBy(row => row.ID)
                    .ToDictionary(group => group.Key, group => group.ToList());

                Dictionary<int, List<PARAM.Row>> moddedRows = moddedParam.Rows
                    .GroupBy(row => row.ID)
                    .ToDictionary(group => group.Key, group => group.ToList());

                foreach (PARAM.Row moddedRow in moddedParam.Rows)
                {
                    if (moddedRows[moddedRow.ID].Count != 1)
                    {
                        log?.Invoke($"Skipped duplicate row ID {moddedRow.ID} in {paramKey}", LOGTYPE.WARNING);
                        continue;
                    }

                    if (!vanillaRows.TryGetValue(moddedRow.ID, out List<PARAM.Row>? baselineMatches))
                    {
                        var added = new ParamRowToMerge(paramKey, moddedRow.ID, moddedRow.Name ?? string.Empty, RowChangeType.Added);
                        AddAllCells(added, moddedRow);
                        rowsToMerge.Add(added);
                        continue;
                    }

                    if (baselineMatches.Count != 1)
                    {
                        log?.Invoke($"Skipped ambiguous vanilla row ID {moddedRow.ID} in {paramKey}", LOGTYPE.WARNING);
                        continue;
                    }

                    PARAM.Row vanillaRow = baselineMatches[0];
                    ParamRowToMerge? modified = null;

                    for (int cellIndex = 0; cellIndex < moddedRow.Cells.Count; cellIndex++)
                    {
                        PARAM.Cell moddedCell = moddedRow.Cells[cellIndex];
                        CellIdentity identity = GetCellIdentity(moddedRow, cellIndex);

                        if (!TryGetCell(vanillaRow, identity, out PARAM.Cell? vanillaCell) ||
                            !Utils.AdvancedEquals(moddedCell.Value, vanillaCell.Value))
                        {
                            modified ??= new ParamRowToMerge(
                                paramKey,
                                moddedRow.ID,
                                moddedRow.Name ?? string.Empty,
                                RowChangeType.Modified);

                            modified.Cells.Add(new ParamCellChange(identity, CloneCellValue(moddedCell.Value)));
                        }
                    }

                    if (modified != null && modified.Cells.Count > 0)
                        rowsToMerge.Add(modified);
                }

                foreach (PARAM.Row vanillaRow in vanillaParam.Rows)
                {
                    if (vanillaRows[vanillaRow.ID].Count == 1 && !moddedRows.ContainsKey(vanillaRow.ID))
                    {
                        rowsToMerge.Add(new ParamRowToMerge(
                            paramKey,
                            vanillaRow.ID,
                            vanillaRow.Name ?? string.Empty,
                            RowChangeType.Deleted));
                    }
                }
            }

            return rowsToMerge;
        }

        public static HashSet<string> ApplyModifiedRows(
            Dictionary<string, PARAM> targetParams,
            List<ParamRowToMerge> rows,
            Action<string, LOGTYPE>? log = null)
        {
            var modifiedParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ParamRowToMerge change in rows)
            {
                if (!targetParams.TryGetValue(change.ParamKey, out PARAM? targetParam))
                {
                    log?.Invoke(
                        $"Skipped {change.ParamKey}:{change.RowID}: parameter no longer exists in the current regulation",
                        LOGTYPE.WARNING);
                    continue;
                }

                List<PARAM.Row> targetMatches = targetParam.Rows.Where(row => row.ID == change.RowID).ToList();
                if (targetMatches.Count > 1)
                {
                    log?.Invoke(
                        $"Skipped {change.ParamKey}:{change.RowID}: duplicate target row IDs are ambiguous",
                        LOGTYPE.WARNING);
                    continue;
                }

                PARAM.Row? targetRow = targetMatches.FirstOrDefault();

                if (change.ChangeType == RowChangeType.Deleted)
                {
                    if (targetRow != null)
                    {
                        targetParam.Rows.Remove(targetRow);
                        modifiedParams.Add(change.ParamKey);
                    }
                    continue;
                }

                if (change.ChangeType == RowChangeType.Added)
                {
                    if (targetRow == null)
                    {
                        PARAMDEF? targetDef = targetParam.AppliedParamdef ?? targetParam.Rows.FirstOrDefault()?.Def;
                        if (targetDef == null)
                        {
                            log?.Invoke(
                                $"Skipped added row {change.ParamKey}:{change.RowID}: no target ParamDef is available",
                                LOGTYPE.WARNING);
                            continue;
                        }

                        targetRow = new PARAM.Row(change.RowID, change.Name, targetDef);
                        InsertRowSorted(targetParam.Rows, targetRow);
                    }

                    if (ApplyCells(change, targetRow, log) > 0)
                        modifiedParams.Add(change.ParamKey);
                    continue;
                }

                if (targetRow == null)
                {
                    log?.Invoke(
                        $"Skipped modified row {change.ParamKey}:{change.RowID}: row no longer exists in the current regulation",
                        LOGTYPE.WARNING);
                    continue;
                }

                if (ApplyCells(change, targetRow, log) > 0)
                    modifiedParams.Add(change.ParamKey);
            }

            return modifiedParams;
        }

        private static int ApplyCells(
            ParamRowToMerge change,
            PARAM.Row targetRow,
            Action<string, LOGTYPE>? log)
        {
            int applied = 0;
            foreach (ParamCellChange cellChange in change.Cells)
            {
                if (!TryGetCell(targetRow, cellChange.Identity, out PARAM.Cell? targetCell))
                {
                    log?.Invoke(
                        $"Skipped removed field {change.ParamKey}:{change.RowID}:{cellChange.Identity.FieldName}",
                        LOGTYPE.WARNING);
                    continue;
                }

                try
                {
                    targetCell.Value = CloneCellValue(cellChange.Value);
                    applied++;
                }
                catch (Exception ex)
                {
                    log?.Invoke(
                        $"Could not apply {change.ParamKey}:{change.RowID}:{cellChange.Identity.FieldName} ({ex.Message})",
                        LOGTYPE.WARNING);
                }
            }

            return applied;
        }

        private static void AddAllCells(ParamRowToMerge change, PARAM.Row row)
        {
            for (int i = 0; i < row.Cells.Count; i++)
            {
                PARAM.Cell cell = row.Cells[i];
                change.Cells.Add(new ParamCellChange(GetCellIdentity(row, i), CloneCellValue(cell.Value)));
            }
        }

        private static CellIdentity GetCellIdentity(PARAM.Row row, int index)
        {
            string fieldName = row.Cells[index].Def.InternalName ?? string.Empty;
            int occurrence = 0;

            for (int i = 0; i < index; i++)
            {
                if (string.Equals(row.Cells[i].Def.InternalName, fieldName, StringComparison.Ordinal))
                    occurrence++;
            }

            return new CellIdentity(fieldName, occurrence, index);
        }

        private static bool TryGetCell(PARAM.Row row, CellIdentity identity, out PARAM.Cell? cell)
        {
            int occurrence = 0;
            for (int i = 0; i < row.Cells.Count; i++)
            {
                PARAM.Cell candidate = row.Cells[i];
                if (!string.Equals(candidate.Def.InternalName ?? string.Empty, identity.FieldName, StringComparison.Ordinal))
                    continue;

                if (occurrence == identity.Occurrence)
                {
                    cell = candidate;
                    return true;
                }

                occurrence++;
            }

            if (identity.SourceIndex >= 0 && identity.SourceIndex < row.Cells.Count)
            {
                PARAM.Cell candidate = row.Cells[identity.SourceIndex];
                if (string.Equals(candidate.Def.InternalName ?? string.Empty, identity.FieldName, StringComparison.Ordinal))
                {
                    cell = candidate;
                    return true;
                }
            }

            cell = null;
            return false;
        }

        private static object CloneCellValue(object value)
        {
            return value is byte[] bytes ? bytes.ToArray() : value;
        }

        private static void InsertRowSorted(List<PARAM.Row> rows, PARAM.Row row)
        {
            int index = rows.FindIndex(existing => existing.ID > row.ID);
            if (index >= 0)
                rows.Insert(index, row);
            else
                rows.Add(row);
        }
    }

    internal enum RowChangeType
    {
        Modified,
        Added,
        Deleted
    }

    internal readonly record struct CellIdentity(string FieldName, int Occurrence, int SourceIndex);

    internal sealed class ParamCellChange
    {
        public CellIdentity Identity { get; }
        public object Value { get; }

        public ParamCellChange(CellIdentity identity, object value)
        {
            Identity = identity;
            Value = value;
        }
    }

    internal sealed class ParamRowToMerge
    {
        public string ParamKey { get; }
        public int RowID { get; }
        public string Name { get; }
        public RowChangeType ChangeType { get; }
        public List<ParamCellChange> Cells { get; }

        public ParamRowToMerge(string paramKey, int rowID, string name, RowChangeType changeType)
        {
            ParamKey = paramKey;
            RowID = rowID;
            Name = name;
            ChangeType = changeType;
            Cells = new List<ParamCellChange>();
        }
    }
}
