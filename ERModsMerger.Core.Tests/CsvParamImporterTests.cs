using ERModsMerger.Core.Formats;
using ERModsMerger.Core.Utility;
using SoulsFormats;
using Xunit;

namespace ERModsMerger.Core.Tests;

public class CsvParamImporterTests
{
    private const string ParamKey = "TestParam";

    [Theory]
    [InlineData("ID,Name,A", ',')]
    [InlineData("ID;Name;A", ';')]
    [InlineData("ID\tName\tA", '\t')]
    public void DetectsSmithboxSeparators(string header, char expected)
    {
        Assert.Equal(expected, CsvParamImporter.DetectSeparator(header));
    }

    [Fact]
    public void ExistingRowOnlyChangesFieldsPresentInCsv()
    {
        PARAMDEF def = CreateDef("A", "B");
        PARAM target = CreateParam(def, Row(def, 1, ("A", 10), ("B", 77)));
        string path = WriteCsv("ID,Name,A\n1,Row 1,99\n");

        try
        {
            List<ParamRowToMerge> changes = CsvParamImporter.Parse(path, ParamKey, target);
            ParamRowToMerge change = Assert.Single(changes);

            Assert.Equal(RowChangeType.Modified, change.ChangeType);
            ParamCellChange cell = Assert.Single(change.Cells);
            Assert.Equal("A", cell.Identity.FieldName);
            Assert.Equal(99, cell.Value);

            RegulationMergeEngine.ApplyModifiedRows(Params(target), changes);

            Assert.Equal(99, target.Rows[0]["A"].Value);
            Assert.Equal(77, target.Rows[0]["B"].Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddedRowUsesCurrentParamDefDefaultsForUnspecifiedFields()
    {
        PARAMDEF def = CreateDef("A", "CurrentOnly");
        PARAM target = CreateParam(def, Row(def, 1, ("A", 10), ("CurrentOnly", 77)));
        string path = WriteCsv("ID;Name;A\n50;New Row;5\n");

        try
        {
            List<ParamRowToMerge> changes = CsvParamImporter.Parse(path, ParamKey, target);
            ParamRowToMerge change = Assert.Single(changes);

            Assert.Equal(RowChangeType.Added, change.ChangeType);

            RegulationMergeEngine.ApplyModifiedRows(Params(target), changes);

            PARAM.Row added = Assert.Single(target.Rows, row => row.ID == 50);
            Assert.Equal(5, added["A"].Value);
            Assert.Equal(0, added["CurrentOnly"].Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void BlankCellDoesNotOverwriteExistingValue()
    {
        PARAMDEF def = CreateDef("A", "B");
        PARAM target = CreateParam(def, Row(def, 1, ("A", 10), ("B", 20)));
        string path = WriteCsv("ID,Name,A,B\n1,Row 1,99,\n");

        try
        {
            List<ParamRowToMerge> changes = CsvParamImporter.Parse(path, ParamKey, target);
            ParamRowToMerge change = Assert.Single(changes);

            Assert.Single(change.Cells);
            Assert.Equal("A", change.Cells[0].Identity.FieldName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnknownColumnIsWarnedAndIgnored()
    {
        PARAMDEF def = CreateDef("A");
        PARAM target = CreateParam(def, Row(def, 1, ("A", 10)));
        string path = WriteCsv("ID,Name,RemovedField,A\n1,Row 1,123,99\n");
        var warnings = new List<string>();

        try
        {
            List<ParamRowToMerge> changes = CsvParamImporter.Parse(
                path,
                ParamKey,
                target,
                (message, type) =>
                {
                    if (type == LOGTYPE.WARNING)
                        warnings.Add(message);
                });

            Assert.Contains(warnings, warning => warning.Contains("RemovedField"));
            ParamRowToMerge change = Assert.Single(changes);
            Assert.Single(change.Cells);
            Assert.Equal("A", change.Cells[0].Identity.FieldName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SmithboxEmptyNamePlaceholderColumnIsAccepted()
    {
        PARAMDEF def = CreateDef("A");
        PARAM target = CreateParam(def, Row(def, 1, ("A", 10)));
        string path = WriteCsv("ID,,A\n1,,99\n");

        try
        {
            List<ParamRowToMerge> changes = CsvParamImporter.Parse(path, ParamKey, target);
            ParamRowToMerge change = Assert.Single(changes);

            ParamCellChange cell = Assert.Single(change.Cells);
            Assert.Equal("A", cell.Identity.FieldName);
            Assert.Equal(99, cell.Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddedRowWithOnlyNameIsPersistedWithParamDefDefaults()
    {
        PARAMDEF def = CreateDef("A");
        PARAM target = CreateParam(def, Row(def, 1, ("A", 10)));
        string path = WriteCsv("ID,Name\n50,New Row\n");

        try
        {
            List<ParamRowToMerge> changes = CsvParamImporter.Parse(path, ParamKey, target);
            ParamRowToMerge change = Assert.Single(changes);

            Assert.Equal(RowChangeType.Added, change.ChangeType);
            Assert.Empty(change.Cells);

            HashSet<string> modified = RegulationMergeEngine.ApplyModifiedRows(Params(target), changes);

            Assert.Contains(ParamKey, modified);
            PARAM.Row added = Assert.Single(target.Rows, row => row.ID == 50);
            Assert.Equal("New Row", added.Name);
            Assert.Equal(0, added["A"].Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DuplicateRowIdsAreRejected()
    {
        PARAMDEF def = CreateDef("A");
        PARAM target = CreateParam(def, Row(def, 1, ("A", 10)));
        string path = WriteCsv("ID,Name,A\n1,First,20\n1,Second,30\n");

        try
        {
            InvalidDataException ex = Assert.Throws<InvalidDataException>(
                () => CsvParamImporter.Parse(path, ParamKey, target));

            Assert.Contains("duplicate row ID 1", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteCsv(string contents)
    {
        string path = Path.Combine(Path.GetTempPath(), $"ermm-csv-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, contents);
        return path;
    }

    private static Dictionary<string, PARAM> Params(PARAM param)
        => new(StringComparer.OrdinalIgnoreCase) { [ParamKey] = param };

    private static PARAMDEF CreateDef(params string[] fields)
    {
        var def = new PARAMDEF
        {
            ParamType = "TEST_PARAM_ST",
            DataVersion = 1,
            FormatVersion = 203
        };

        foreach (string name in fields)
            def.Fields.Add(new PARAMDEF.Field(def, PARAMDEF.DefType.s32, name));

        return def;
    }

    private static PARAM CreateParam(PARAMDEF def, params PARAM.Row[] rows)
    {
        return new PARAM
        {
            ParamType = def.ParamType,
            ParamdefDataVersion = def.DataVersion,
            Rows = rows.ToList()
        };
    }

    private static PARAM.Row Row(PARAMDEF def, int id, params (string Field, int Value)[] values)
    {
        var row = new PARAM.Row(id, $"Row {id}", def);

        foreach ((string field, int value) in values)
            row[field].Value = value;

        return row;
    }
}
