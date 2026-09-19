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

        private static LOG? RegLog;

        public RegulationBin(string path, LOG? log = null)
        {
            if (log != null)
                RegLog = log;
            RegLog ??= LOG.Log("Regulation");

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
            RegLog!.AddSubLog($"Regulation version: {Version} ({RegulationVersion})");

            LOG progressLog = RegLog!.AddSubLog("Progress: 0%");
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

                // Never force a mismatched layout. Historical PARAM data-version metadata may differ
                // from the modern ParamDef when the physical row layout is still byte-for-byte compatible;
                // that case is accepted only when ParamType and exact row size still match.
                if (!RegulationParamDefCompatibility.TryApply(
                        param,
                        versionAwareDef,
                        RegulationVersion,
                        out bool toleratedDataVersionMismatch))
                {
                    string paramNameForLog = Path.GetFileNameWithoutExtension(binderFile.Name);
                    RegLog!.AddSubLog(
                        $"Skipped {paramNameForLog}: no compatible ParamDef for regulation {Version} ({RegulationVersion})",
                        LOGTYPE.WARNING);
                    skippedParams++;
                    continue;
                }

                string paramName = Path.GetFileNameWithoutExtension(binderFile.Name);
                if (toleratedDataVersionMismatch)
                {
                    RegLog!.AddSubLog(
                        $"{paramName}: accepted historical ParamDef data version {param.ParamdefDataVersion} by exact row-size match",
                        LOGTYPE.WARNING);
                }
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
            LOG progressLog = RegLog!.AddSubLog("Gathering semantic regulation changes");
            List<ParamRowToMerge> changes = RegulationMergeEngine.FindRowsToMerge(
                Params,
                vanillaParams,
                (message, type) => RegLog!.AddSubLog(message, type));

            progressLog.Message = $"Gathered {changes.Count} semantic regulation change(s) ✓";
            progressLog.Type = LOGTYPE.SUCCESS;
            progressLog.Progress = 100;
            return changes;
        }

        public void ApplyModifiedRows(
            List<ParamRowToMerge> rows,
            Dictionary<string, PARAM>? fallbackParams = null)
        {
            LOG progressLog = RegLog!.AddSubLog("Applying semantic regulation changes");

            HashSet<string> modifiedParams = RegulationMergeEngine.ApplyModifiedRows(
                Params,
                rows,
                fallbackParams,
                (message, type) => RegLog!.AddSubLog(message, type));

            foreach (string paramKey in modifiedParams)
            {
                int binderFileIndex = bnd.Files.FindIndex(file =>
                    string.Equals(Path.GetFileNameWithoutExtension(file.Name), paramKey, StringComparison.OrdinalIgnoreCase));

                if (binderFileIndex == -1)
                {
                    RegLog!.AddSubLog($"Could not find {paramKey}.param in output regulation", LOGTYPE.ERROR);
                    continue;
                }

                bnd.Files[binderFileIndex].Bytes = Params[paramKey].Write();
            }

            progressLog.Message = $"Applied changes to {modifiedParams.Count} parameter file(s) ✓";
            progressLog.Type = LOGTYPE.SUCCESS;
            progressLog.Progress = 100;
        }

        public static void MergeRegulationsV2(List<FileToMerge> regulationBinFiles, bool manualConflictResolving)
        {
            LOG mainLog = LOG.Log("Merging regulations");
            Console.WriteLine();

            string gameRegulationPath = Path.Combine(ModsMergerConfig.LoadedConfig!.GamePath, "regulation.bin");
            RegLog = mainLog.AddSubLog("Loading current vanilla regulation.bin");

            if (!File.Exists(gameRegulationPath))
            {
                RegLog!.AddSubLog(
                    $"Could not locate vanilla regulation.bin at {ModsMergerConfig.LoadedConfig.GamePath}. Please verify GamePath in ERModsMergerConfig\\config.json",
                    LOGTYPE.ERROR);
                return;
            }

            RegulationBin? currentVanilla = null;
            RegulationBin? outputRegulation = null;
            var historicalBaselines = new Dictionary<ulong, RegulationBin>();

            try
            {
                RegulationArchiveManifest manifest = RegulationArchiveManifest.Load();

                if (!TryReadVersion(gameRegulationPath, out ulong installedVersion))
                {
                    RegLog!.AddSubLog("Could not read the installed game's regulation version.", LOGTYPE.ERROR);
                    return;
                }

                if (!manifest.Contains(installedVersion))
                {
                    RegLog!.AddSubLog(
                        $"Installed regulation {Utils.ParseParamVersion(installedVersion)} ({installedVersion}) is not supported by this build. " +
                        $"Latest tested raw version: {manifest.MaxSupportedVersion}. Update ERModsMerger's regulation assets/ParamDefs before merging.",
                        LOGTYPE.ERROR);
                    return;
                }

                currentVanilla = new RegulationBin(gameRegulationPath, RegLog);

                RegLog = mainLog.AddSubLog("Loading initial output regulation from current vanilla");
                outputRegulation = new RegulationBin(gameRegulationPath);

                Dictionary<ulong, string> baselinePaths = RegulationBaselineCatalog.Build(
                    gameRegulationPath,
                    currentVanilla.RegulationVersion,
                    manifest,
                    mainLog);

                RegulationMergePreflight.Result preflight = RegulationMergePreflight.Analyze(
                    regulationBinFiles.Select(file => file.Path),
                    currentVanilla.RegulationVersion,
                    baselinePaths,
                    manifest);
                RegulationMergePreflight.Log(preflight, mainLog);

                if (!preflight.CanMerge)
                {
                    RegLog = mainLog.AddSubLog(
                        "Regulation merge aborted before output changes because preflight failed.",
                        LOGTYPE.ERROR);
                    return;
                }

                var conflictTracker = new RegulationConflictTracker();

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
                                RegLog!.AddSubLog(
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

                        foreach (RegulationConflict conflict in conflictTracker.Observe(relativePathLog, changes))
                        {
                            RegLog!.AddSubLog(
                                $"Priority conflict {conflict.ParamKey}/{conflict.RowId}/{conflict.FieldName}: " +
                                $"{conflict.PreviousSource} [{conflict.PreviousValue}] -> " +
                                $"{conflict.WinningSource} [{conflict.WinningValue}]. " +
                                "Later processed (higher-priority) mod wins.",
                                LOGTYPE.WARNING);
                        }

                        outputRegulation.ApplyModifiedRows(changes, currentVanilla.Params);
                    }
                    catch (Exception ex)
                    {
                        RegLog!.AddSubLog(
                            $"Could not merge {fileToMerge.Path}. Regulation may be incompatible: {ex.Message}",
                            LOGTYPE.ERROR);
                    }

                    Console.WriteLine();
                }

                RegLog = mainLog.AddSubLog("Saving merged regulation.bin");
                string outputPath = Path.Combine(
                    ModsMergerConfig.LoadedConfig.CurrentProfile!.MergedModsFolderPath,
                    "regulation.bin");
                outputRegulation.SaveTransactional(outputPath);
                RegLog!.AddSubLog($"Saved and verified: {outputPath}", LOGTYPE.SUCCESS);
            }
            catch (Exception ex)
            {
                RegLog!.AddSubLog($"Could not load or merge regulation.bin: {ex.Message}", LOGTYPE.ERROR);
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

        public void SaveTransactional(string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
                directory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(directory);

            string tempPath = Path.Combine(
                directory,
                $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            string backupPath = path + ".bak";

            try
            {
                Save(tempPath);

                if (!TryReadVersion(tempPath, out ulong writtenVersion) ||
                    writtenVersion != RegulationVersion)
                {
                    throw new InvalidDataException(
                        $"Transactional regulation verification failed. Expected version {RegulationVersion}, found {writtenVersion}.");
                }

                if (File.Exists(path))
                {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);

                    File.Replace(tempPath, path, backupPath, true);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        public void Dispose()
        {
            bnd.Dispose();
            Params.Clear();
            _paramdefs.Clear();
            GC.SuppressFinalize(this);
        }

    }
}
