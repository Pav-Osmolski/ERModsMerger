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

        public static Dictionary<ulong, string> Build(
            string currentGameRegulationPath,
            ulong currentGameVersion,
            RegulationArchiveManifest manifest,
            LOG mainLog)
        {
            var result = new Dictionary<ulong, string>
            {
                [currentGameVersion] = currentGameRegulationPath
            };

            Directory.CreateDirectory(BundledBaselineFolderPath);
            Directory.CreateDirectory(UserBaselineFolderPath);

            IndexFolder(
                BundledBaselineFolderPath,
                result,
                currentGameVersion,
                manifest,
                isUserOverrideFolder: false,
                mainLog);

            IndexFolder(
                UserBaselineFolderPath,
                result,
                currentGameVersion,
                manifest,
                isUserOverrideFolder: true,
                mainLog);

            mainLog.AddSubLog(
                $"Indexed {result.Count} vanilla regulation version(s). " +
                $"Bundled: {BundledBaselineFolderPath}; user overrides: {UserBaselineFolderPath}");

            return result;
        }

        private static void IndexFolder(
            string folder,
            Dictionary<ulong, string> result,
            ulong currentGameVersion,
            RegulationArchiveManifest manifest,
            bool isUserOverrideFolder,
            LOG mainLog)
        {
            foreach (string candidate in Directory.EnumerateFiles(folder, "*.bin", SearchOption.AllDirectories)
                                                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!RegulationBin.TryReadVersion(candidate, out ulong version))
                    {
                        mainLog.AddSubLog($"Ignored unreadable baseline {candidate}", LOGTYPE.WARNING);
                        continue;
                    }

                    if (version == currentGameVersion)
                        continue;

                    if (!manifest.TryGet(version, out RegulationArchiveEntry? manifestEntry))
                    {
                        mainLog.AddSubLog(
                            $"Ignored unsupported baseline {candidate}: regulation {Utils.ParseParamVersion(version)} ({version}) is not in the tested archive manifest",
                            LOGTYPE.WARNING);
                        continue;
                    }

                    if (isUserOverrideFolder)
                    {
                        string hash = RegulationArchiveManifest.ComputeSha256(candidate);
                        if (!string.Equals(hash, manifestEntry.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            mainLog.AddSubLog(
                                $"Rejected non-vanilla baseline {candidate}: SHA-256 does not match the known vanilla " +
                                $"{Utils.ParseParamVersion(version)} ({version}) regulation",
                                LOGTYPE.ERROR);
                            continue;
                        }

                        result[version] = candidate;
                        mainLog.AddSubLog(
                            $"Verified user vanilla baseline override for {Utils.ParseParamVersion(version)} ({version})");
                        continue;
                    }

                    if (!result.TryAdd(version, candidate))
                    {
                        mainLog.AddSubLog(
                            $"Duplicate bundled baseline for {Utils.ParseParamVersion(version)} ({version}); keeping {result[version]}",
                            LOGTYPE.WARNING);
                    }
                }
                catch (Exception ex)
                {
                    mainLog.AddSubLog($"Ignored baseline {candidate}: {ex.Message}", LOGTYPE.WARNING);
                }
            }
        }
    }
}
