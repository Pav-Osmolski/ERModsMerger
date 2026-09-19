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

        foreach (ManifestRegulation regulation in manifest.Regulations)
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

                Assert.False(
                    string.IsNullOrWhiteSpace(param.ParamType),
                    $"{regulation.AssetFolder}/{paramName}: PARAM has no ParamType.");

                Assert.True(
                    paramDefs.TryGetValue(param.ParamType, out PARAMDEF? versionAwareDef),
                    $"{regulation.AssetFolder}/{paramName}: missing ParamDef for {param.ParamType}.");

                PARAMDEF effectiveDef = versionAwareDef!.VersionAware
                    ? versionAwareDef.GetFilteredParamdefForRegulationVersion(rawVersion)
                    : versionAwareDef;

                Assert.True(
                    param.ApplyParamdefCarefully(effectiveDef),
                    $"{regulation.AssetFolder}/{paramName}: ParamDef mismatch for {param.ParamType}, " +
                    $"regulation {rawVersion}, data version {param.ParamdefDataVersion}, detected row size {param.DetectedSize}.");
            }
        }
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
