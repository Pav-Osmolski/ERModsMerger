using ERModsMerger.Core.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ERModsMerger.Core.Formats
{
    /// <summary>
    /// Indexes exact-version vanilla regulation.bin files without relying on filenames.
    /// Bundled baselines are loaded from extracted Assets/Regulations; user baselines may
    /// supplement or override historical versions under VanillaRegulations.
    /// </summary>
    internal static class RegulationBaselineCatalog
    {
        public static string BundledBaselineFolderPath => Path.Combine(
            ModsMergerConfig.LoadedConfig!.AppDataFolderPath,
            "Regulations");

        public static string UserBaselineFolderPath => Path.Combine(
            ModsMergerConfig.LoadedConfig!.AppDataFolderPath,
            "VanillaRegulations");

        public static string BaselineFolderPath => UserBaselineFolderPath;

        public static Dictionary<ulong, string> Build(
            string currentGameRegulationPath,
            ulong currentGameVersion,
            LOG mainLog)
        {
            var result = new Dictionary<ulong, string>
            {
                [currentGameVersion] = currentGameRegulationPath
            };

            Directory.CreateDirectory(BundledBaselineFolderPath);
            Directory.CreateDirectory(UserBaselineFolderPath);

            IndexFolder(BundledBaselineFolderPath, result, currentGameVersion, overwriteHistorical: false, mainLog);
            IndexFolder(UserBaselineFolderPath, result, currentGameVersion, overwriteHistorical: true, mainLog);

            mainLog.AddSubLog(
                $"Indexed {result.Count} vanilla regulation version(s). " +
                $"Bundled: {BundledBaselineFolderPath}; user overrides: {UserBaselineFolderPath}");

            return result;
        }

        private static void IndexFolder(
            string folder,
            Dictionary<ulong, string> result,
            ulong currentGameVersion,
            bool overwriteHistorical,
            LOG mainLog)
        {
            foreach (string candidate in Directory.EnumerateFiles(folder, "*.bin", SearchOption.AllDirectories)
                                                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!RegulationBin.TryReadVersion(candidate, out ulong version))
                        continue;

                    if (version == currentGameVersion)
                        continue;

                    if (result.TryGetValue(version, out string? existing))
                    {
                        if (overwriteHistorical)
                        {
                            mainLog.AddSubLog(
                                $"Baseline override for {Utils.ParseParamVersion(version)} ({version}): {candidate}",
                                LOGTYPE.WARNING);
                            result[version] = candidate;
                        }
                        else
                        {
                            mainLog.AddSubLog(
                                $"Duplicate bundled baseline for {Utils.ParseParamVersion(version)} ({version}); keeping {existing}",
                                LOGTYPE.WARNING);
                        }

                        continue;
                    }

                    result.Add(version, candidate);
                }
                catch (Exception ex)
                {
                    mainLog.AddSubLog($"Ignored baseline {candidate}: {ex.Message}", LOGTYPE.WARNING);
                }
            }
        }
    }
}
