using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace ERModsMerger.Core.Formats
{
    internal sealed class RegulationArchiveManifest
    {
        public string ArchiveVersion { get; set; } = string.Empty;
        public List<RegulationArchiveEntry> Regulations { get; set; } = new();

        public ulong MaxSupportedVersion => Regulations.Count == 0
            ? 0
            : Regulations.Max(entry => entry.RawVersion);

        public static RegulationArchiveManifest Load()
        {
            string path = Path.Combine(
                ModsMergerConfig.LoadedConfig!.AppDataFolderPath,
                "Regulations",
                "manifest.json");

            if (!File.Exists(path))
                throw new FileNotFoundException("Regulation archive manifest is missing.", path);

            RegulationArchiveManifest? manifest = JsonSerializer.Deserialize<RegulationArchiveManifest>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (manifest == null || manifest.Regulations.Count == 0)
                throw new InvalidDataException($"Regulation archive manifest is empty or invalid: {path}");

            return manifest;
        }

        public bool TryGet(ulong rawVersion, out RegulationArchiveEntry entry)
        {
            entry = Regulations.FirstOrDefault(item => item.RawVersion == rawVersion)!;
            return entry != null;
        }

        public bool Contains(ulong rawVersion)
            => Regulations.Any(entry => entry.RawVersion == rawVersion);

        public static string ComputeSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }

    internal sealed class RegulationArchiveEntry
    {
        public string ArchiveLabel { get; set; } = string.Empty;
        public string AssetFolder { get; set; } = string.Empty;
        public ulong RawVersion { get; set; }
        public long Size { get; set; }
        public string Sha256 { get; set; } = string.Empty;
    }
}
