using System;
using System.Collections.Generic;
using System.IO;

namespace ERModsMerger.Core.Formats
{
    /// <summary>
    /// Indexes exact-version vanilla regulation.bin files without relying on filenames.
    /// Historical game data is intentionally not redistributed with ERModsMerger.
    /// </summary>
    internal static class RegulationBaselineCatalog
    {
        public static string BaselineFolderPath => Path.Combine(
            ModsMergerConfig.LoadedConfig!.AppDataFolderPath,
            "VanillaRegulations");

        public static Dictionary<ulong, string> Build(
            string currentGameRegulationPath,
            ulong currentGameVersion,
            LOG mainLog)
        {
            var result = new Dictionary<ulong, string>
            {
                [currentGameVersion] = currentGameRegulationPath
            };

            Directory.CreateDirectory(BaselineFolderPath);
            string currentFullPath = Path.GetFullPath(currentGameRegulationPath);

            foreach (string candidate in Directory.EnumerateFiles(
                         BaselineFolderPath,
                         "*.bin",
                         SearchOption.AllDirectories))
            {
                try
                {
                    if (string.Equals(Path.GetFullPath(candidate), currentFullPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!RegulationBin.TryReadVersion(candidate, out ulong version))
                        continue;

                    // Current installed vanilla always wins for the active version. For historical
                    // duplicates, keep the first file found; the raw BND version is the authority.
                    result.TryAdd(version, candidate);
                }
                catch
                {
                    // Ignore unrelated/corrupt .bin files in the baseline directory.
                }
            }

            mainLog.AddSubLog(
                $"Indexed {result.Count} vanilla regulation version(s). Historical baseline folder: {BaselineFolderPath}");
            return result;
        }
    }
}
