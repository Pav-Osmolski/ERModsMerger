using ERModsMerger.Core.Formats;
using SoulsFormats;
using Xunit;

namespace ERModsMerger.Core.Tests;

public class RegulationMergeEngineTests
{
    private const string ParamKey = "TestParam";

    [Fact]
    public void IdenticalParamsProduceNoChanges()
    {
        PARAMDEF def = CreateDef("A", "B");
        PARAM vanilla = CreateParam(def, Row(def, 1, ("A", 10), ("B", 20)));
        PARAM modded = CreateParam(def, Row(def, 1, ("A", 10), ("B", 20)));

        List<ParamRowToMerge> changes = RegulationMergeEngine.FindRowsToMerge(
            Params(modded),
            Params(vanilla));

        Assert.Empty(changes);
    }

    [Fact]
    public void RowOrderDoesNotCreateFalseChanges()
    {
        PARAMDEF def = CreateDef("A");
        PARAM vanilla = CreateParam(
            def,
            Row(def, 1, ("A", 10)),
            Row(def, 2, ("A", 20)));

        PARAM modded = CreateParam(
            def,
            Row(def, 2, ("A", 20)),
            Row(def, 1, ("A", 10)));

        List<ParamRowToMerge> changes = RegulationMergeEngine.FindRowsToMerge(
            Params(modded),
            Params(vanilla));

        Assert.Empty(changes);
    }

    [Fact]
    public void ModifiedFieldIsAppliedByNameAcrossLayoutChanges()
    {
        PARAMDEF oldDef = CreateDef("A", "B");
        PARAM vanilla = CreateParam(oldDef, Row(oldDef, 1, ("A", 10), ("B", 20)));
        PARAM modded = CreateParam(oldDef, Row(oldDef, 1, ("A", 10), ("B", 99)));

        PARAMDEF currentDef = CreateDef("NewField", "A", "B");
        PARAM target = CreateParam(
            currentDef,
            Row(currentDef, 1, ("NewField", 777), ("A", 10), ("B", 20)));

        List<ParamRowToMerge> changes = RegulationMergeEngine.FindRowsToMerge(
            Params(modded),
            Params(vanilla));

        RegulationMergeEngine.ApplyModifiedRows(Params(target), changes);

        Assert.Equal(777, target.Rows[0]["NewField"].Value);
        Assert.Equal(10, target.Rows[0]["A"].Value);
        Assert.Equal(99, target.Rows[0]["B"].Value);
    }

    [Fact]
    public void AddedRowIsAppendedWhenItsIdIsHighest()
    {
        PARAMDEF def = CreateDef("A");
        PARAM vanilla = CreateParam(def, Row(def, 1, ("A", 10)));
        PARAM modded = CreateParam(
            def,
            Row(def, 1, ("A", 10)),
            Row(def, 100, ("A", 55)));

        PARAM target = CreateParam(
            def,
            Row(def, 1, ("A", 10)),
            Row(def, 50, ("A", 50)));

        List<ParamRowToMerge> changes = RegulationMergeEngine.FindRowsToMerge(
            Params(modded),
            Params(vanilla));

        RegulationMergeEngine.ApplyModifiedRows(Params(target), changes);

        Assert.Equal(new[] { 1, 50, 100 }, target.Rows.Select(row => row.ID));
        Assert.Equal(55, target.Rows.Single(row => row.ID == 100)["A"].Value);
    }

    [Fact]
    public void DeletedRowIsRemoved()
    {
        PARAMDEF def = CreateDef("A");
        PARAM vanilla = CreateParam(
            def,
            Row(def, 1, ("A", 10)),
            Row(def, 2, ("A", 20)));

        PARAM modded = CreateParam(def, Row(def, 1, ("A", 10)));
        PARAM target = CreateParam(
            def,
            Row(def, 1, ("A", 10)),
            Row(def, 2, ("A", 20)));

        List<ParamRowToMerge> changes = RegulationMergeEngine.FindRowsToMerge(
            Params(modded),
            Params(vanilla));

        RegulationMergeEngine.ApplyModifiedRows(Params(target), changes);

        Assert.DoesNotContain(target.Rows, row => row.ID == 2);
    }

    [Fact]
    public void AddedRowKeepsFieldsIntroducedByNewerGameVersion()
    {
        PARAMDEF oldDef = CreateDef("A");
        PARAM vanilla = CreateParam(oldDef);
        PARAM modded = CreateParam(oldDef, Row(oldDef, 10, ("A", 5)));

        PARAMDEF currentDef = CreateDef("A", "CurrentOnly");
        PARAM target = CreateParam(
            currentDef,
            Row(currentDef, 10, ("A", 1), ("CurrentOnly", 77)));

        List<ParamRowToMerge> changes = RegulationMergeEngine.FindRowsToMerge(
            Params(modded),
            Params(vanilla));

        RegulationMergeEngine.ApplyModifiedRows(Params(target), changes);

        Assert.Equal(5, target.Rows[0]["A"].Value);
        Assert.Equal(77, target.Rows[0]["CurrentOnly"].Value);
    }

    [Fact]
    public void RemovedFieldIsSkippedWithoutShiftingOtherFields()
    {
        PARAMDEF oldDef = CreateDef("A", "RemovedField");
        PARAM vanilla = CreateParam(oldDef, Row(oldDef, 1, ("A", 10), ("RemovedField", 20)));
        PARAM modded = CreateParam(oldDef, Row(oldDef, 1, ("A", 10), ("RemovedField", 99)));

        PARAMDEF currentDef = CreateDef("A");
        PARAM target = CreateParam(currentDef, Row(currentDef, 1, ("A", 10)));

        List<ParamRowToMerge> changes = RegulationMergeEngine.FindRowsToMerge(
            Params(modded),
            Params(vanilla));

        HashSet<string> modified = RegulationMergeEngine.ApplyModifiedRows(Params(target), changes);

        Assert.Empty(modified);
        Assert.Equal(10, target.Rows[0]["A"].Value);
    }

    [Fact]
    public void DuplicateTargetRowIdsAreSkippedConservatively()
    {
        PARAMDEF def = CreateDef("A");
        PARAM vanilla = CreateParam(def, Row(def, 1, ("A", 10)));
        PARAM modded = CreateParam(def, Row(def, 1, ("A", 99)));

        PARAM target = CreateParam(
            def,
            Row(def, 1, ("A", 10)),
            Row(def, 1, ("A", 11)));

        List<ParamRowToMerge> changes = RegulationMergeEngine.FindRowsToMerge(
            Params(modded),
            Params(vanilla));

        HashSet<string> modified = RegulationMergeEngine.ApplyModifiedRows(Params(target), changes);

        Assert.Empty(modified);
        Assert.Equal(new[] { 10, 11 }, target.Rows.Select(row => (int)row["A"].Value));
    }


    [Fact]
    public void HigherPriorityEditRestoresRowDeletedByLowerPriorityMod()
    {
        PARAMDEF def = CreateDef("A");
        PARAM currentVanilla = CreateParam(def, Row(def, 1, ("A", 10)));
        PARAM target = CreateParam(def, Row(def, 1, ("A", 10)));

        var deletion = new ParamRowToMerge(ParamKey, 1, "Row 1", RowChangeType.Deleted);
        var edit = new ParamRowToMerge(ParamKey, 1, "Row 1", RowChangeType.Modified);
        edit.Cells.Add(new ParamCellChange(new CellIdentity("A", 0, 0), 99));

        RegulationMergeEngine.ApplyModifiedRows(Params(target), [deletion], Params(currentVanilla));
        Assert.Empty(target.Rows);

        RegulationMergeEngine.ApplyModifiedRows(Params(target), [edit], Params(currentVanilla));

        Assert.Single(target.Rows);
        Assert.Equal(99, target.Rows[0]["A"].Value);
    }

    [Fact]
    public void ConflictTrackerReportsDifferentHigherPriorityValue()
    {
        var lower = new ParamRowToMerge(ParamKey, 1, "Row 1", RowChangeType.Modified);
        lower.Cells.Add(new ParamCellChange(new CellIdentity("A", 0, 0), 10));

        var higher = new ParamRowToMerge(ParamKey, 1, "Row 1", RowChangeType.Modified);
        higher.Cells.Add(new ParamCellChange(new CellIdentity("A", 0, 0), 20));

        var tracker = new RegulationConflictTracker();
        Assert.Empty(tracker.Observe("Lower", [lower]));

        RegulationConflict conflict = Assert.Single(tracker.Observe("Higher", [higher]));
        Assert.Equal("A", conflict.FieldName);
        Assert.Equal("Lower", conflict.PreviousSource);
        Assert.Equal("Higher", conflict.WinningSource);
        Assert.Equal("10", conflict.PreviousValue);
        Assert.Equal("20", conflict.WinningValue);
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
