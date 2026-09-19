using ERModsMerger.Core.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ERModsMerger.Core.Formats
{
    public sealed class RegulationStatusSummary
    {
        public string Message { get; init; } = string.Empty;
        public string Details { get; init; } = string.Empty;
        public bool IsReady { get; init; }
        public bool HasWarnings { get; init; }
        public int RegulationModCount { get; init; }
        public int MigrationCount { get; init; }
    }

    public static class RegulationStatusService
    {
        public static RegulationStatusSummary Analyze(IEnumerable<string> regulationPaths)
        {
            List<string> paths = regulationPaths
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (paths.Count == 0)
            {
                return new RegulationStatusSummary
                {
                    Message = "No regulation.bin mods selected.",
                    IsReady = true
                };
            }

            try
            {
                RegulationArchiveManifest manifest = RegulationArchiveManifest.Load();
                string gamePath = Path.Combine(ModsMergerConfig.LoadedConfig!.GamePath, "regulation.bin");

                if (!RegulationBin.TryReadVersion(gamePath, out ulong currentVersion))
                {
                    return Error(paths.Count, "Installed regulation.bin could not be read.");
                }

                if (!manifest.Contains(currentVersion))
                {
                    return Error(
                        paths.Count,
                        $"Installed regulation {Utils.ParseParamVersion(currentVersion)} ({currentVersion}) is newer or unsupported.");
                }

                var versions = new List<ulong>();
                int unreadable = 0;
                int unsupported = 0;
                int missingBaselines = 0;

                foreach (string path in paths)
                {
                    if (!RegulationBin.TryReadVersion(path, out ulong version))
                    {
                        unreadable++;
                        continue;
                    }

                    versions.Add(version);

                    if (!manifest.TryGet(version, out RegulationArchiveEntry? entry))
                    {
                        unsupported++;
                        continue;
                    }

                    if (version != currentVersion && !HasBaseline(version, entry))
                        missingBaselines++;
                }

                int migrations = versions.Count(version => version != currentVersion);
                string modVersions = versions.Count == 0
                    ? "none"
                    : string.Join(
                        ", ",
                        versions.Distinct().OrderBy(version => version)
                            .Select(version => Utils.ParseParamVersion(version)));

                bool ready = unreadable == 0 && unsupported == 0 && missingBaselines == 0;
                string state = ready ? "Ready" : "Blocked";
                string message =
                    $"Regulation: {state} | Game {Utils.ParseParamVersion(currentVersion)} | " +
                    $"Mods {modVersions} | Migrations {migrations}/{paths.Count}";

                if (!ready)
                    message += $" | Missing {missingBaselines}, unsupported {unsupported}, unreadable {unreadable}";

                return new RegulationStatusSummary
                {
                    Message = message,
                    Details =
                        $"Raw game version: {currentVersion}. Exact historical baselines are required for every migrated mod. " +
                        "Field-level priority conflicts are reported in the merge log.",
                    IsReady = ready,
                    HasWarnings = migrations > 0,
                    RegulationModCount = paths.Count,
                    MigrationCount = migrations
                };
            }
            catch (Exception ex)
            {
                return Error(paths.Count, $"Regulation preflight unavailable: {ex.Message}");
            }
        }

        private static RegulationStatusSummary Error(int count, string message)
        {
            return new RegulationStatusSummary
            {
                Message = "Regulation: Blocked | " + message,
                Details = message,
                IsReady = false,
                RegulationModCount = count
            };
        }

        private static bool HasBaseline(ulong version, RegulationArchiveEntry entry)
        {
            string bundled = Path.Combine(
                RegulationBaselineCatalog.BundledBaselineFolderPath,
                entry.AssetFolder,
                "regulation.bin");

            if (File.Exists(bundled))
                return true;

            if (!Directory.Exists(RegulationBaselineCatalog.UserBaselineFolderPath))
                return false;

            foreach (string candidate in Directory.EnumerateFiles(
                         RegulationBaselineCatalog.UserBaselineFolderPath,
                         "*.bin",
                         SearchOption.AllDirectories))
            {
                if (!RegulationBin.TryReadVersion(candidate, out ulong candidateVersion) ||
                    candidateVersion != version)
                {
                    continue;
                }

                string hash = RegulationArchiveManifest.ComputeSha256(candidate);
                if (string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
