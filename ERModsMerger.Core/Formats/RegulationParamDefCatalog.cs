using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ERModsMerger.Core.Formats
{
    /// <summary>
    /// Caches parsed version-aware ParamDef XML documents for the lifetime of the process.
    /// RegulationBin instances receive shallow dictionary copies so disposing one instance
    /// cannot mutate the shared catalog.
    /// </summary>
    internal static class RegulationParamDefCatalog
    {
        private static readonly object Sync = new();
        private static readonly Dictionary<string, CacheEntry> Cache =
            new(StringComparer.OrdinalIgnoreCase);

        public static Dictionary<string, PARAMDEF> Load(string paramDefsPath)
        {
            string fullPath = Path.GetFullPath(paramDefsPath);
            string[] files = Directory.GetFiles(fullPath, "*.xml")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            string fingerprint = BuildFingerprint(files);

            lock (Sync)
            {
                if (Cache.TryGetValue(fullPath, out CacheEntry? cached) &&
                    string.Equals(cached.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    return new Dictionary<string, PARAMDEF>(
                        cached.ParamDefs,
                        StringComparer.OrdinalIgnoreCase);
                }

                var parsed = new Dictionary<string, PARAMDEF>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (string file in files)
                {
                    PARAMDEF paramdef = PARAMDEF.XmlDeserialize(file, true);
                    parsed[paramdef.ParamType] = paramdef;
                }

                Cache[fullPath] = new CacheEntry(fingerprint, parsed);

                return new Dictionary<string, PARAMDEF>(
                    parsed,
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        private static string BuildFingerprint(IEnumerable<string> files)
        {
            return string.Join(
                "|",
                files.Select(path =>
                {
                    FileInfo info = new(path);
                    return $"{info.Name}:{info.Length}:{info.LastWriteTimeUtc.Ticks}";
                }));
        }

        private sealed record CacheEntry(
            string Fingerprint,
            Dictionary<string, PARAMDEF> ParamDefs);
    }
}
