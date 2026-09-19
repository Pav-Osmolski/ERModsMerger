using ERModsMerger.Core.Formats;
using SoulsFormats;
using Xunit;

namespace ERModsMerger.Core.Tests;

public class RegulationEndToEndTests
{
    private static readonly string[] RepresentativeVersions =
    [
        "1.00.0",
        "1.01.0",
        "1.02.0",
        "1.03.0",
        "1.07.0",
        "1.11.0",
        "1.12.0",
        "1.12.1",
        "1.16.1",
        "1.17.0",
        "1.17.1"
    ];

    [Fact]
    public void RepresentativeHistoricalRegulationsMigrateEndToEnd()
    {
        string repoRoot = FindRepositoryRoot();
        string assetsRoot = Path.Combine(repoRoot, "Assets");
        string currentPath = Path.Combine(assetsRoot, "Regulations", "1.17.1", "regulation.bin");
        string tempRoot = Path.Combine(Path.GetTempPath(), "ERModsMerger-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        ModsMergerConfig? previousConfig = ModsMergerConfig.LoadedConfig;
        ModsMergerConfig.LoadedConfig = new ModsMergerConfig
        {
            AppDataFolderPath = assetsRoot
        };

        try
        {
            using var currentVanilla = new RegulationBin(currentPath);

            foreach (string version in RepresentativeVersions)
            {
                string sourcePath = Path.Combine(assetsRoot, "Regulations", version, "regulation.bin");
                Assert.True(File.Exists(sourcePath), $"Missing test baseline {version}");

                using var sourceVanilla = new RegulationBin(sourcePath);
                using var modWorkingCopy = new RegulationBin(sourcePath);
                using var output = new RegulationBin(currentPath);

                Candidate candidate = FindSharedIntegralCell(sourceVanilla, currentVanilla);
                object changedValue = ChangeValue(candidate.SourceCell.Value);

                var change = new ParamRowToMerge(
                    candidate.ParamKey,
                    candidate.RowId,
                    candidate.RowName,
                    RowChangeType.Modified);
                change.Cells.Add(new ParamCellChange(
                    new CellIdentity(candidate.FieldName, candidate.Occurrence, candidate.SourceIndex),
                    changedValue));

                modWorkingCopy.ApplyModifiedRows([change]);

                string modPath = Path.Combine(tempRoot, $"{version}-mod.bin");
                modWorkingCopy.SaveTransactional(modPath);

                using var reloadedMod = new RegulationBin(modPath);
                List<ParamRowToMerge> delta = reloadedMod.FindRowsToMerge(sourceVanilla.Params);

                Assert.Contains(delta, row =>
                    row.ParamKey == candidate.ParamKey &&
                    row.RowID == candidate.RowId &&
                    row.Cells.Any(cell =>
                        cell.Identity.FieldName == candidate.FieldName &&
                        ValuesEqual(cell.Value, changedValue)));

                output.ApplyModifiedRows(delta, currentVanilla.Params);

                string outputPath = Path.Combine(tempRoot, $"{version}-merged.bin");
                output.SaveTransactional(outputPath);

                using var reloadedOutput = new RegulationBin(outputPath);
                PARAM.Row resultRow = Assert.Single(
                    reloadedOutput.Params[candidate.ParamKey].Rows.Where(row => row.ID == candidate.RowId));

                PARAM.Cell resultCell = Assert.Single(
                    resultRow.Cells.Where(cell =>
                        string.Equals(
                            cell.Def.InternalName,
                            candidate.FieldName,
                            StringComparison.Ordinal)));

                Assert.True(
                    ValuesEqual(resultCell.Value, changedValue),
                    $"{version}: {candidate.ParamKey}/{candidate.RowId}/{candidate.FieldName} did not survive end-to-end migration.");
            }
        }
        finally
        {
            ModsMergerConfig.LoadedConfig = previousConfig;
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static Candidate FindSharedIntegralCell(RegulationBin source, RegulationBin current)
    {
        foreach ((string paramKey, PARAM sourceParam) in source.Params.OrderBy(pair => pair.Key))
        {
            if (!current.Params.TryGetValue(paramKey, out PARAM? currentParam))
                continue;

            foreach (PARAM.Row sourceRow in sourceParam.Rows)
            {
                List<PARAM.Row> currentRows = currentParam.Rows.Where(row => row.ID == sourceRow.ID).ToList();
                if (currentRows.Count != 1)
                    continue;

                PARAM.Row currentRow = currentRows[0];

                for (int sourceIndex = 0; sourceIndex < sourceRow.Cells.Count; sourceIndex++)
                {
                    PARAM.Cell sourceCell = sourceRow.Cells[sourceIndex];
                    string fieldName = sourceCell.Def.InternalName ?? string.Empty;
                    if (fieldName.Length == 0 || !IsIntegral(sourceCell.Value))
                        continue;

                    List<PARAM.Cell> currentCells = currentRow.Cells
                        .Where(cell => string.Equals(cell.Def.InternalName, fieldName, StringComparison.Ordinal))
                        .ToList();

                    if (currentCells.Count != 1 || currentCells[0].Value.GetType() != sourceCell.Value.GetType())
                        continue;

                    int occurrence = sourceRow.Cells
                        .Take(sourceIndex)
                        .Count(cell => string.Equals(cell.Def.InternalName, fieldName, StringComparison.Ordinal));

                    return new Candidate(
                        paramKey,
                        sourceRow.ID,
                        sourceRow.Name ?? string.Empty,
                        fieldName,
                        occurrence,
                        sourceIndex,
                        sourceCell);
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not find a shared integral PARAM cell between {source.Version} and {current.Version}.");
    }

    private static bool IsIntegral(object value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong;

    private static object ChangeValue(object value)
    {
        return value switch
        {
            byte v => v == byte.MaxValue ? (byte)(v - 1) : (byte)(v + 1),
            sbyte v => v == sbyte.MaxValue ? (sbyte)(v - 1) : (sbyte)(v + 1),
            short v => v == short.MaxValue ? (short)(v - 1) : (short)(v + 1),
            ushort v => v == ushort.MaxValue ? (ushort)(v - 1) : (ushort)(v + 1),
            int v => v == int.MaxValue ? v - 1 : v + 1,
            uint v => v == uint.MaxValue ? v - 1 : v + 1,
            long v => v == long.MaxValue ? v - 1 : v + 1,
            ulong v => v == ulong.MaxValue ? v - 1 : v + 1,
            _ => throw new InvalidOperationException($"Unsupported test value type {value.GetType().Name}")
        };
    }

    private static bool ValuesEqual(object left, object right)
        => left.Equals(right);

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

    private sealed record Candidate(
        string ParamKey,
        int RowId,
        string RowName,
        string FieldName,
        int Occurrence,
        int SourceIndex,
        PARAM.Cell SourceCell);
}
