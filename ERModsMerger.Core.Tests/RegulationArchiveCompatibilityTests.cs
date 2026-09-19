using ERModsMerger.Core.Formats;
using SoulsFormats;
using System.Text.Json;
using Xunit;

namespace ERModsMerger.Core.Tests;

public class RegulationArchiveCompatibilityTests
{
    [Fact]
    public void EveryBundledRegulationMatchesManifestAndParamDefs()
    {
        string repoRoot = FindRepositoryRoot();
        string manifestPath = Path.Combine(repoRoot, "Assets", "Regulations", "manifest.json");
        string paramDefsPath = Path.Combine(repoRoot, "Assets", "ParamDefs");

        Manifest manifest = JsonSerializer.Deserialize<Manifest>(
            File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(34, manifest.Regulations.Count);

        Dictionary<string, PARAMDEF> paramDefs = Directory
            .EnumerateFiles(paramDefsPath, "*.xml")
            .Select(path => PARAMDEF.XmlDeserialize(path, true))
            .ToDictionary(def => def.ParamType, StringComparer.OrdinalIgnoreCase);

        var compatibilityFailures = new List<string>();

        IReadOnlyList<ManifestRegulation> regulations = string.Equals(
                Environment.GetEnvironmentVariable("ERMM_FULL_REGULATION_MATRIX"),
                "1",
                StringComparison.Ordinal)
            ? manifest.Regulations
            : manifest.Regulations
                .Where(regulation =>
                    regulation.AssetFolder is "1.00.0" or "1.12.1" or "1.16.1")
                .ToList();

        Assert.Equal(
            string.Equals(
                Environment.GetEnvironmentVariable("ERMM_FULL_REGULATION_MATRIX"),
                "1",
                StringComparison.Ordinal)
                ? 34
                : 3,
            regulations.Count);

        foreach (ManifestRegulation regulation in regulations)
        {
            string path = Path.Combine(
                repoRoot,
                "Assets",
                "Regulations",
                regulation.AssetFolder,
                "regulation.bin");

            Assert.True(File.Exists(path), $"Missing regulation asset: {path}");

            using BND4 bnd = SFUtil.DecryptERRegulation(path);
            ulong rawVersion = Convert.ToUInt64(bnd.Version);

            Assert.Equal(
                regulation.RawVersion,
                rawVersion);

            foreach (var binderFile in bnd.Files)
            {
                if (!binderFile.Name.EndsWith(".param", StringComparison.OrdinalIgnoreCase))
                    continue;

                PARAM param = PARAM.ReadIgnoreCompression(binderFile.Bytes);
                string paramName = Path.GetFileNameWithoutExtension(binderFile.Name);

                if (string.IsNullOrWhiteSpace(param.ParamType))
                {
                    compatibilityFailures.Add(
                        $"{regulation.AssetFolder}/{paramName}: PARAM has no ParamType.");
                    continue;
                }

                if (!paramDefs.TryGetValue(param.ParamType, out PARAMDEF? versionAwareDef))
                {
                    compatibilityFailures.Add(
                        $"{regulation.AssetFolder}/{paramName}: missing ParamDef for {param.ParamType}.");
                    continue;
                }

                if (!RegulationParamDefCompatibility.TryApply(
                        param,
                        versionAwareDef,
                        rawVersion,
                        out _))
                {
                    PARAMDEF effectiveDef = versionAwareDef.VersionAware
                        ? versionAwareDef.GetFilteredParamdefForRegulationVersion(rawVersion)
                        : versionAwareDef;

                    compatibilityFailures.Add(
                        $"{regulation.AssetFolder}/{paramName}: ParamDef mismatch for {param.ParamType}, " +
                        $"regulation {rawVersion}, PARAM data version {param.ParamdefDataVersion}, " +
                        $"ParamDef data version {effectiveDef.DataVersion}, detected row size {param.DetectedSize}, " +
                        $"ParamDef row size {effectiveDef.GetRowSize()}.");
                }
            }
        }

        Assert.True(
            compatibilityFailures.Count == 0,
            "Regulation compatibility failures:" + Environment.NewLine +
            string.Join(Environment.NewLine, compatibilityFailures));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Assets", "Regulations", "manifest.json")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root from {AppContext.BaseDirectory}.");
    }

    private sealed class Manifest
    {
        public List<ManifestRegulation> Regulations { get; set; } = [];
    }

    private sealed class ManifestRegulation
    {
        public string AssetFolder { get; set; } = string.Empty;
        public ulong RawVersion { get; set; }
    }
}
