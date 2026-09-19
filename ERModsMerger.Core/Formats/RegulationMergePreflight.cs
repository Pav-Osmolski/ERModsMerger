using ERModsMerger.Core.Utility;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ERModsMerger.Core.Formats
{
    internal static class RegulationMergePreflight
    {
        internal sealed record Entry(
            string Path,
            ulong? RegulationVersion,
            string DisplayVersion,
            bool IsCurrentVersion,
            bool IsSupported,
            bool HasBaseline,
            string? Error);

        internal sealed class Result
        {
            public IReadOnlyList<Entry> Entries { get; }
            public int InvalidCount => Entries.Count(entry => entry.Error != null);
            public int UnsupportedCount => Entries.Count(entry => entry.Error == null && !entry.IsSupported);
            public int MissingBaselineCount => Entries.Count(entry => entry.Error == null && entry.IsSupported && !entry.HasBaseline);
            public bool CanMerge => InvalidCount == 0 && UnsupportedCount == 0 && MissingBaselineCount == 0;

            public IReadOnlyList<ulong> RequiredHistoricalVersions => Entries
                .Where(entry => entry.RegulationVersion.HasValue && !entry.IsCurrentVersion)
                .Select(entry => entry.RegulationVersion!.Value)
                .Distinct()
                .OrderBy(version => version)
                .ToList();

            public Result(IReadOnlyList<Entry> entries)
            {
                Entries = entries;
            }
        }

        public static Result Analyze(
            IEnumerable<string> regulationPaths,
            ulong currentVersion,
            IReadOnlyDictionary<ulong, string> baselines,
            RegulationArchiveManifest manifest)
        {
            var entries = new List<Entry>();

            foreach (string path in regulationPaths.Where(File.Exists).Distinct(System.StringComparer.OrdinalIgnoreCase))
            {
                if (!RegulationBin.TryReadVersion(path, out ulong version))
                {
                    entries.Add(new Entry(
                        path, null, "Unknown", false, false, false,
                        "Could not read regulation version"));
                    continue;
                }

                bool isCurrent = version == currentVersion;
                bool isSupported = manifest.Contains(version);
                bool hasBaseline = isCurrent || baselines.ContainsKey(version);
                entries.Add(new Entry(
                    path,
                    version,
                    Utils.ParseParamVersion(version),
                    isCurrent,
                    isSupported,
                    hasBaseline,
                    null));
            }

            return new Result(entries);
        }

        public static void Log(Result result, LOG mainLog)
        {
            if (result.Entries.Count == 0)
                return;

            string required = result.RequiredHistoricalVersions.Count == 0
                ? "none"
                : string.Join(", ", result.RequiredHistoricalVersions.Select(version =>
                    $"{Utils.ParseParamVersion(version)} ({version})"));

            mainLog.AddSubLog(
                $"Regulation preflight: {result.Entries.Count} mod regulation(s); historical baselines required: {required}");

            foreach (Entry entry in result.Entries)
            {
                string fileName = Path.GetFileName(Path.GetDirectoryName(entry.Path) ?? entry.Path);

                if (entry.Error != null)
                {
                    mainLog.AddSubLog($"{fileName}: {entry.Error}", LOGTYPE.ERROR);
                    continue;
                }

                if (!entry.IsSupported)
                {
                    mainLog.AddSubLog(
                        $"{fileName}: unsupported regulation {entry.DisplayVersion} ({entry.RegulationVersion}); " +
                        "update ERModsMerger's regulation archive/ParamDefs before merging this version",
                        LOGTYPE.ERROR);
                    continue;
                }

                if (!entry.HasBaseline)
                {
                    mainLog.AddSubLog(
                        $"{fileName}: missing exact vanilla baseline for {entry.DisplayVersion} ({entry.RegulationVersion})",
                        LOGTYPE.ERROR);
                }
            }

            if (result.CanMerge)
                mainLog.AddSubLog("Regulation preflight passed ✓", LOGTYPE.SUCCESS);
            else
                mainLog.AddSubLog(
                    $"Regulation preflight failed: {result.MissingBaselineCount} missing baseline(s), " +
                    $"{result.UnsupportedCount} unsupported version(s), {result.InvalidCount} unreadable regulation(s)",
                    LOGTYPE.ERROR);
        }
    }
}
