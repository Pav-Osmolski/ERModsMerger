using ERModsMerger.Core.Utility;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ERModsMerger.Core.Formats
{
    /// <summary>
    /// Reads, compares and writes Elden Ring regulation.bin files.
    ///
    /// Multi-version merging is deliberately implemented as a three-way merge:
    ///   old/new mod -> vanilla from the mod's exact regulation version -> current vanilla.
    /// This prevents official FromSoftware changes between regulation versions from being
    /// mistaken for mod edits.
    /// </summary>
    class RegulationBin : IDisposable
    {
        private readonly BND4 bnd;
        private readonly Dictionary<string, PARAMDEF> _paramdefs;

        public Dictionary<string, PARAM> Params { get; }
        public ulong RegulationVersion { get; }
        public string Version => Utils.ParseParamVersion(RegulationVersion);

        private static LOG RegLog = null!;

        public RegulationBin(string path)
        {
            Params = new Dictionary<string, PARAM>(StringComparer.OrdinalIgnoreCase);
            bnd = SFUtil.DecryptERRegulation(path);
            RegulationVersion = Convert.ToUInt64(bnd.Version);
            _paramdefs = LoadParamDefs();
            Load();
        }

        /// <summary>
        /// Reads only the raw regulation version from a regulation.bin without loading PARAM data.
        /// Used while indexing historical vanilla baselines.
        /// </summary>
        public static bool TryReadVersion(string path, out ulong version)
        {
            try
            {
                using BND4 regulation = SFUtil.DecryptERRegulation(path);
                version = Convert.ToUInt64(regulation.Version);
                return true;
            }
            catch
            {
                version = 0;
                return false;
            }
        }

        private Dictionary<string, PARAMDEF> LoadParamDefs()
        {
            var paramdefs = new Dictionary<string, PARAMDEF>(StringComparer.OrdinalIgnoreCase);
            string paramDefsPath = Path.Combine(ModsMergerConfig.LoadedConfig!.AppDataFolderPath, "ParamDefs");

            foreach (string file in Directory.GetFiles(paramDefsPath, "*.xml"))
            {
                // Read all FirstVersion/RemovedVersion metadata. We then filter the definition
                // to this regulation's exact raw version before applying it to a PARAM.
                PARAMDEF paramdef = PARAMDEF.XmlDeserialize(file, true);
                paramdefs[paramdef.ParamType] = paramdef;
            }

            return paramdefs;
        }

        private void Load()
        {
            RegLog.AddSubLog($"Regulation version: {Version} ({RegulationVersion})");

            LOG progressLog = RegLog.AddSubLog("Progress: 0%");
            int loadedParams = 0;
            int skippedParams = 0;

            for (int i = 0; i < bnd.Files.Count; i++)
            {
                double progress = i / (double)bnd.Files.Count * 100;
                progressLog.Message = $"Progress: {Math.Round(progress, 0)}%";
                progressLog.Progress = progress;

                var binderFile = bnd.Files[i];
                if (!binderFile.Name.EndsWith(".param", StringComparison.OrdinalIgnoreCase))
                    continue;

                PARAM param = PARAM.ReadIgnoreCompression(binderFile.Bytes);
                if (param.ParamType == null || !_paramdefs.TryGetValue(param.ParamType, out PARAMDEF? versionAwareDef))
                {
                    skippedParams++;
                    continue;
                }

                PARAMDEF effectiveDef = versionAwareDef.VersionAware
                    ? versionAwareDef.GetFilteredParamdefForRegulationVersion(RegulationVersion)
                    : versionAwareDef;

                // Never force a mismatched ParamDef. A forced definition can appear to work while
                // silently shifting every later field, which is disastrous for cross-version merges.
                if (!param.ApplyParamdefCarefully(effectiveDef))
                {
                    string paramNameForLog = Path.GetFileNameWithoutExtension(binderFile.Name);
                    RegLog.AddSubLog(
                        $"Skipped {paramNameForLog}: no compatible ParamDef for regulation {Version} ({RegulationVersion})",
                        LOGTYPE.WARNING);
                    skippedParams++;
                    continue;
                }

                string paramName = Path.GetFileNameWithoutExtension(binderFile.Name);
                Params[paramName] = param;
                loadedParams++;
            }

            progressLog.Message = $"Progress: 100% - Loaded {loadedParams} params" +
                                  (skippedParams > 0 ? $", skipped {skippedParams}" : string.Empty) + " ✓";
            progressLog.Type = skippedParams == 0 ? LOGTYPE.SUCCESS : LOGTYPE.WARNING;
            progressLog.Progress = 100;
        }

        /// <summary>
        /// Produces a semantic delta between this regulation and its exact-version vanilla baseline.
        /// Rows are matched by ID and fields by internal name instead of array position.
        /// </summary>
        public List<ParamRowToMerge> FindRowsToMerge(Dictionary<string, PARAM> vanillaParams)
        {
            var rowsToMerge = new List<ParamRowToMerge>();
            LOG progressLog = RegLog.AddSubLog("Gathering rows to merge - Progress: 0%");
            double counter = 0;
            double max = Math.Max(Params.Count, 1);

            foreach ((string paramKey, PARAM moddedParam) in Params)
            {
                double progress = counter / max * 100;
                progressLog.Message = $"Gathering rows to merge - Progress: {Math.Round(progress, 0)}%";
                progressLog.Progress = progress;
                counter++;

                if (!vanillaParams.TryGetValue(paramKey, out PARAM? vanillaParam))
                {
                    RegLog.AddSubLog($"Skipped {paramKey}: it is not present in the exact-version vanilla baseline", LOGTYPE.WARNING);
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
                        RegLog.AddSubLog($"Skipped duplicate row ID {moddedRow.ID} in {paramKey}", LOGTYPE.WARNING);
                        continue;
                    }

                    if (!vanillaRows.TryGetValue(moddedRow.ID, out List<PARAM.Row>? baselineMatches))
                    {
                        ParamRowToMerge added = new ParamRowToMerge(paramKey, moddedRow.ID, moddedRow.Name ?? string.Empty, RowChangeType.Added);
                        AddAllCells(added, moddedRow);
                        rowsToMerge.Add(added);
                        continue;
                    }

                    if (baselineMatches.Count != 1)
                    {
                        RegLog.AddSubLog($"Skipped ambiguous vanilla row ID {moddedRow.ID} in {paramKey}", LOGTYPE.WARNING);
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
                            modified ??= new ParamRowToMerge(paramKey, moddedRow.ID, moddedRow.Name ?? string.Empty, RowChangeType.Modified);
                            modified.Cells.Add(new ParamCellChange(identity, CloneCellValue(moddedCell.Value)));
                        }
                    }

                    if (modified != null && modified.Cells.Count > 0)
                        rowsToMerge.Add(modified);
                }

                // The original merger's index-based walk could turn every row after a deletion into
                // a false "new row". Detect deletions explicitly by ID instead.
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

            progressLog.Message = "Gathering rows to merge - Progress: 100% ✓";
            progressLog.Type = LOGTYPE.SUCCESS;
            progressLog.Progress = 100;
            return rowsToMerge;
        }

        public void ApplyModifiedRows(List<ParamRowToMerge> rows)
        {
            LOG progressLog = RegLog.AddSubLog("Merging modified rows - Progress: 0%");
            double counter = 0;
            double max = Math.Max(rows.Count, 1);
            var modifiedParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ParamRowToMerge change in rows)
            {
                double progress = counter / max * 100;
                progressLog.Message = $"Merging modified rows - Progress: {Math.Round(progress, 0)}%";
                progressLog.Progress = progress;
                counter++;

                if (!Params.TryGetValue(change.ParamKey, out PARAM? targetParam))
                {
                    RegLog.AddSubLog(
                        $"Skipped {change.ParamKey}:{change.RowID}: parameter no longer exists in the current regulation",
                        LOGTYPE.WARNING);
                    continue;
                }

                PARAM.Row? targetRow = targetParam.Rows.FirstOrDefault(row => row.ID == change.RowID);

                if (change.ChangeType == RowChangeType.Deleted)
                {
                    int removed = targetParam.Rows.RemoveAll(row => row.ID == change.RowID);
                    if (removed > 0)
                        modifiedParams.Add(change.ParamKey);
                    continue;
                }

                if (change.ChangeType == RowChangeType.Added)
                {
                    if (targetRow == null)
                    {
                        targetRow = new PARAM.Row(change.RowID, change.Name, targetParam.AppliedParamdef);
                        InsertRowSorted(targetParam.Rows, targetRow);
                    }

                    // If a later official patch introduced the same ID, retain any fields introduced
                    // by that later patch and overwrite only fields that existed in the old mod row.
                    ApplyCells(change, targetRow);
                    modifiedParams.Add(change.ParamKey);
                    continue;
                }

                if (targetRow == null)
                {
                    RegLog.AddSubLog(
                        $"Skipped modified row {change.ParamKey}:{change.RowID}: row no longer exists in the current regulation",
                        LOGTYPE.WARNING);
                    continue;
                }

                if (ApplyCells(change, targetRow) > 0)
                    modifiedParams.Add(change.ParamKey);
            }

            foreach (string paramKey in modifiedParams)
            {
                int binderFileIndex = bnd.Files.FindIndex(file =>
                    string.Equals(Path.GetFileNameWithoutExtension(file.Name), paramKey, StringComparison.OrdinalIgnoreCase));

                if (binderFileIndex == -1)
                {
                    RegLog.AddSubLog($"Could not find {paramKey}.param in output regulation", LOGTYPE.ERROR);
                    continue;
                }

                bnd.Files[binderFileIndex].Bytes = Params[paramKey].Write();
            }

            progressLog.Message = "Merging modified rows - Progress: 100% ✓";
            progressLog.Type = LOGTYPE.SUCCESS;
            progressLog.Progress = 100;
        }

        private static int ApplyCells(ParamRowToMerge change, PARAM.Row targetRow)
        {
            int applied = 0;
            foreach (ParamCellChange cellChange in change.Cells)
            {
                if (!TryGetCell(targetRow, cellChange.Identity, out PARAM.Cell? targetCell))
                {
                    RegLog.AddSubLog(
                        $"Skipped removed field {change.ParamKey}:{change.RowID}:{cellChange.Identity.FieldName}",
                        LOGTYPE.WARNING);
                    continue;
                }

                try
                {
                    // PARAM.Cell.Value performs the appropriate primitive conversion for the target field.
                    targetCell.Value = CloneCellValue(cellChange.Value);
                    applied++;
                }
                catch (Exception ex)
                {
                    RegLog.AddSubLog(
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

            // Fallback for unusual ParamDefs with blank/duplicate names that kept their position.
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

        public static void MergeRegulationsV2(List<FileToMerge> regulationBinFiles, bool manualConflictResolving)
        {
            LOG mainLog = LOG.Log("Merging regulations");
            Console.WriteLine();

            string gameRegulationPath = Path.Combine(ModsMergerConfig.LoadedConfig!.GamePath, "regulation.bin");
            RegLog = mainLog.AddSubLog("Loading current vanilla regulation.bin");

            if (!File.Exists(gameRegulationPath))
            {
                RegLog.AddSubLog(
                    $"Could not locate vanilla regulation.bin at {ModsMergerConfig.LoadedConfig.GamePath}. Please verify GamePath in ERModsMergerConfig\\config.json",
                    LOGTYPE.ERROR);
                return;
            }

            RegulationBin? currentVanilla = null;
            RegulationBin? outputRegulation = null;
            var historicalBaselines = new Dictionary<ulong, RegulationBin>();

            try
            {
                currentVanilla = new RegulationBin(gameRegulationPath);

                RegLog = mainLog.AddSubLog("Loading initial output regulation from current vanilla");
                outputRegulation = new RegulationBin(gameRegulationPath);

                Dictionary<ulong, string> baselinePaths = RegulationBaselineCatalog.Build(
                    gameRegulationPath,
                    currentVanilla.RegulationVersion,
                    mainLog);

                RegulationMergePreflight.Result preflight = RegulationMergePreflight.Analyze(
                    regulationBinFiles.Select(file => file.Path),
                    currentVanilla.RegulationVersion,
                    baselinePaths);
                RegulationMergePreflight.Log(preflight, mainLog);

                foreach (FileToMerge fileToMerge in regulationBinFiles)
                {
                    if (!File.Exists(fileToMerge.Path))
                        continue;

                    string relativePathLog = GetRelativeModLogName(fileToMerge);
                    RegLog = mainLog.AddSubLog($"Loading {relativePathLog}");

                    try
                    {
                        using var moddedRegulation = new RegulationBin(fileToMerge.Path);

                        RegulationBin baseline;
                        if (moddedRegulation.RegulationVersion == currentVanilla.RegulationVersion)
                        {
                            baseline = currentVanilla;
                        }
                        else
                        {
                            if (!baselinePaths.TryGetValue(moddedRegulation.RegulationVersion, out string? baselinePath))
                            {
                                RegLog.AddSubLog(
                                    $"Cannot merge regulation {moddedRegulation.Version} ({moddedRegulation.RegulationVersion}): " +
                                    $"its exact vanilla baseline is missing. Restore the bundled Assets/Regulations entry or put a vanilla regulation.bin " +
                                    $"for this version anywhere under {RegulationBaselineCatalog.UserBaselineFolderPath}",
                                    LOGTYPE.ERROR);
                                continue;
                            }

                            if (!historicalBaselines.TryGetValue(moddedRegulation.RegulationVersion, out RegulationBin? loadedBaseline))
                            {
                                RegLog = mainLog.AddSubLog(
                                    $"Loading vanilla baseline {moddedRegulation.Version} ({moddedRegulation.RegulationVersion})");
                                loadedBaseline = new RegulationBin(baselinePath);
                                historicalBaselines.Add(moddedRegulation.RegulationVersion, loadedBaseline);
                            }

                            baseline = loadedBaseline;
                            RegLog = mainLog.AddSubLog(
                                $"Migrating {relativePathLog} from {moddedRegulation.Version} to {currentVanilla.Version}");
                        }

                        List<ParamRowToMerge> changes = moddedRegulation.FindRowsToMerge(baseline.Params);
                        outputRegulation.ApplyModifiedRows(changes);
                    }
                    catch (Exception ex)
                    {
                        RegLog.AddSubLog(
                            $"Could not merge {fileToMerge.Path}. Regulation may be incompatible: {ex.Message}",
                            LOGTYPE.ERROR);
                    }

                    Console.WriteLine();
                }

                RegLog = mainLog.AddSubLog("Saving merged regulation.bin");
                string outputPath = Path.Combine(
                    ModsMergerConfig.LoadedConfig.CurrentProfile!.MergedModsFolderPath,
                    "regulation.bin");
                outputRegulation.Save(outputPath);
                RegLog.AddSubLog($"Saved in: {outputPath}", LOGTYPE.SUCCESS);
            }
            catch (Exception ex)
            {
                RegLog.AddSubLog($"Could not load or merge regulation.bin: {ex.Message}", LOGTYPE.ERROR);
            }
            finally
            {
                foreach (RegulationBin baseline in historicalBaselines.Values)
                    baseline.Dispose();
                outputRegulation?.Dispose();
                currentVanilla?.Dispose();
            }
        }

        // Kept for callers that still reference the old entry point. V2 is now the canonical implementation.
        public static void MergeRegulationsV1(List<FileToMerge> regulationBinFiles, bool manualConflictResolving)
        {
            MergeRegulationsV2(regulationBinFiles, manualConflictResolving);
        }

        private static string GetRelativeModLogName(FileToMerge file)
        {
            string directoryName = Path.GetFileName(Path.GetDirectoryName(file.Path) ?? string.Empty);
            if (string.IsNullOrWhiteSpace(directoryName))
                directoryName = Path.GetFileName(file.Path);
            return $"{directoryName} : {file.ModRelativePath}";
        }

        public void Save(string path)
        {
            SFUtil.EncryptERRegulation(path, bnd);
        }

        public void Dispose()
        {
            bnd.Dispose();
            Params.Clear();
            _paramdefs.Clear();
            GC.SuppressFinalize(this);
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
}
