using ERModsMerger.Core.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ERModsMerger.Core.Formats
{
    public static class CsvParamMerger
    {
        public static bool MergeFolder()
        {
            ModsMergerConfig config = ModsMergerConfig.LoadedConfig
                ?? throw new InvalidOperationException("Configuration is not loaded.");
            ProfileConfig profile = config.CurrentProfile
                ?? throw new InvalidOperationException("No profile is selected.");

            LOG mainLog = LOG.Log("Merging CSV params");
            string gameRegulationPath = Path.Combine(config.GamePath, "regulation.bin");

            if (!File.Exists(gameRegulationPath))
            {
                mainLog.AddSubLog($"Could not locate vanilla regulation.bin at {config.GamePath}", LOGTYPE.ERROR);
                return false;
            }

            string[] csvFiles = Directory.Exists(profile.CSVToMergeFolderPath)
                ? Directory.GetFiles(profile.CSVToMergeFolderPath, "*.csv", SearchOption.AllDirectories)
                : [];

            if (csvFiles.Length == 0)
            {
                mainLog.AddSubLog(
                    $"No CSV files found under {profile.CSVToMergeFolderPath}",
                    LOGTYPE.ERROR);
                return false;
            }

            RegulationArchiveManifest manifest = RegulationArchiveManifest.Load();
            if (!RegulationBin.TryReadVersion(gameRegulationPath, out ulong installedVersion) ||
                !manifest.TryGet(installedVersion, out RegulationArchiveEntry? installedEntry))
            {
                mainLog.AddSubLog(
                    "Installed regulation version is unreadable or unsupported by this build.",
                    LOGTYPE.ERROR);
                return false;
            }

            string installedHash = RegulationArchiveManifest.ComputeSha256(gameRegulationPath);
            if (!string.Equals(installedHash, installedEntry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                mainLog.AddSubLog(
                    "Installed regulation.bin is not the known vanilla file. Verify Elden Ring game files before merging CSV params.",
                    LOGTYPE.ERROR);
                return false;
            }

            using var currentVanilla = new RegulationBin(gameRegulationPath, mainLog);
            using var output = new RegulationBin(gameRegulationPath, mainLog);
            var conflicts = new RegulationConflictTracker();

            List<CsvSource> sources = DiscoverSources(profile.CSVToMergeFolderPath, csvFiles);
            int appliedRows = 0;
            int appliedFiles = 0;

            foreach (CsvSource source in sources)
            {
                foreach (string csvPath in source.Files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    string paramKey = Path.GetFileNameWithoutExtension(csvPath);
                    if (!output.Params.TryGetValue(paramKey, out SoulsFormats.PARAM? targetParam))
                    {
                        mainLog.AddSubLog(
                            $"{source.Name}: skipped {Path.GetFileName(csvPath)} because PARAM '{paramKey}' is not present in the current regulation",
                            LOGTYPE.WARNING);
                        continue;
                    }

                    try
                    {
                        // Parse against the evolving output, not untouched vanilla. This keeps full
                        // Smithbox row exports semantic (unchanged fields stay omitted) while allowing
                        // a higher-priority CSV to explicitly restore a value changed by a lower source.
                        List<ParamRowToMerge> changes = CsvParamImporter.Parse(
                            csvPath,
                            paramKey,
                            targetParam,
                            (message, type) => mainLog.AddSubLog($"{source.Name}: {message}", type));

                        foreach (RegulationConflict conflict in conflicts.Observe(source.Name, changes))
                        {
                            mainLog.AddSubLog(
                                $"CSV priority conflict {conflict.ParamKey}/{conflict.RowId}/{conflict.FieldName}: " +
                                $"{conflict.PreviousSource} [{conflict.PreviousValue}] -> " +
                                $"{conflict.WinningSource} [{conflict.WinningValue}]. Higher-priority CSV source wins.",
                                LOGTYPE.WARNING);
                        }

                        output.ApplyModifiedRows(changes, currentVanilla.Params);
                        appliedRows += changes.Count;
                        appliedFiles++;

                        mainLog.AddSubLog(
                            $"{source.Name}: imported {changes.Count} changed/added row(s) from {Path.GetFileName(csvPath)}",
                            LOGTYPE.SUCCESS);
                    }
                    catch (Exception ex)
                    {
                        mainLog.AddSubLog(
                            $"{source.Name}: could not import {Path.GetFileName(csvPath)}: {ex.Message}",
                            LOGTYPE.ERROR);
                        return false;
                    }
                }
            }

            if (appliedFiles == 0 || appliedRows == 0)
            {
                mainLog.AddSubLog("CSV merge produced no PARAM changes.", LOGTYPE.WARNING);
                return false;
            }

            Directory.CreateDirectory(profile.MergedModsFolderPath);
            string outputPath = Path.Combine(profile.MergedModsFolderPath, "regulation.bin");
            output.SaveTransactional(outputPath);

            mainLog.AddSubLog(
                $"CSV merge complete: {appliedFiles} CSV file(s), {appliedRows} changed/added row(s). Saved and verified: {outputPath}",
                LOGTYPE.SUCCESS);

            return true;
        }

        private static List<CsvSource> DiscoverSources(string root, IEnumerable<string> files)
        {
            return files
                .GroupBy(path => GetSourceName(root, path), StringComparer.OrdinalIgnoreCase)
                // Same low-to-high processing convention as the console ModsToMerge path:
                // alphabetically earlier source names are processed last and therefore win.
                .OrderByDescending(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new CsvSource(group.Key, group.ToList()))
                .ToList();
        }

        private static string GetSourceName(string root, string path)
        {
            string relative = Path.GetRelativePath(root, path);
            string[] parts = relative.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

            return parts.Length > 1
                ? parts[0]
                : Path.GetFileNameWithoutExtension(path);
        }

        private sealed record CsvSource(string Name, List<string> Files);
    }
}
