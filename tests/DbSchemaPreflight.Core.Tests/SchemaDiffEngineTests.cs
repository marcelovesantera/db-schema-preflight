using FluentAssertions;
using DbSchemaPreflight.Core.Diff;
using DbSchemaPreflight.Core.Models;

namespace DbSchemaPreflight.Core.Tests;

public sealed class SchemaDiffEngineTests
{
    private readonly SchemaDiffEngine _engine = new();

    [Fact]
    public void Compare_TableMissingInTarget_ReturnsCriticalMissingTable()
    {
        var reference = new SchemaSnapshot
        {
            SchemaName = "REF",
            ExtractedAt = DateTime.Now,
            Tables = [new TableDefinition { Owner = "REF", Name = "TB_CUSTOMER" }]
        };
        var target = new SchemaSnapshot { SchemaName = "TGT", ExtractedAt = DateTime.Now };

        var result = _engine.Compare(reference, target);

        result.Should().HaveCount(1);
        result[0].Severity.Should().Be(DifferenceSeverity.Critical);
        result[0].Type.Should().Be(DifferenceType.MissingTable);
        result[0].TableName.Should().Be("TB_CUSTOMER");
    }

    [Fact]
    public void Compare_ExtraTableInTarget_ReturnsInfoExtraTable()
    {
        var reference = new SchemaSnapshot { SchemaName = "REF", ExtractedAt = DateTime.Now };
        var target = new SchemaSnapshot
        {
            SchemaName = "TGT",
            ExtractedAt = DateTime.Now,
            Tables = [new TableDefinition { Owner = "TGT", Name = "TB_LOG" }]
        };

        var result = _engine.Compare(reference, target);

        result.Should().HaveCount(1);
        result[0].Severity.Should().Be(DifferenceSeverity.Info);
        result[0].Type.Should().Be(DifferenceType.ExtraTable);
        result[0].TableName.Should().Be("TB_LOG");
    }

    [Fact]
    public void Compare_IdenticalSnapshots_ReturnsEmptyList()
    {
        var table = new TableDefinition { Owner = "REF", Name = "TB_ORDER" };
        var reference = new SchemaSnapshot { SchemaName = "REF", ExtractedAt = DateTime.Now, Tables = [table] };
        var target = new SchemaSnapshot { SchemaName = "TGT", ExtractedAt = DateTime.Now, Tables = [table] };

        var result = _engine.Compare(reference, target);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compare_BothSnapshotsEmpty_ReturnsEmptyList()
    {
        var reference = new SchemaSnapshot { SchemaName = "REF", ExtractedAt = DateTime.Now };
        var target = new SchemaSnapshot { SchemaName = "TGT", ExtractedAt = DateTime.Now };

        var result = _engine.Compare(reference, target);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compare_TableNamesAreCaseInsensitive_NoDifferenceReturned()
    {
        var reference = new SchemaSnapshot
        {
            SchemaName = "REF",
            ExtractedAt = DateTime.Now,
            Tables = [new TableDefinition { Owner = "REF", Name = "tb_customer" }]
        };
        var target = new SchemaSnapshot
        {
            SchemaName = "TGT",
            ExtractedAt = DateTime.Now,
            Tables = [new TableDefinition { Owner = "TGT", Name = "TB_CUSTOMER" }]
        };

        var result = _engine.Compare(reference, target);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compare_ColumnMissingInTarget_ReturnsCriticalMissingColumn()
    {
        var refTable = new TableDefinition
        {
            Owner = "REF", Name = "TB_CUSTOMER",
            Columns = [new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "EMAIL", DataType = "VARCHAR2", ColumnId = 1 }]
        };
        var tgtTable = new TableDefinition { Owner = "TGT", Name = "TB_CUSTOMER" };

        var reference = new SchemaSnapshot { SchemaName = "REF", ExtractedAt = DateTime.Now, Tables = [refTable] };
        var target = new SchemaSnapshot { SchemaName = "TGT", ExtractedAt = DateTime.Now, Tables = [tgtTable] };

        var result = _engine.Compare(reference, target);

        result.Should().ContainSingle(d =>
            d.Severity == DifferenceSeverity.Critical &&
            d.Type == DifferenceType.MissingColumn &&
            d.TableName == "TB_CUSTOMER" &&
            d.ColumnName == "EMAIL");
    }

    [Fact]
    public void Compare_ExtraColumnInTarget_ReturnsWarningExtraColumn()
    {
        var refTable = new TableDefinition { Owner = "REF", Name = "TB_CUSTOMER" };
        var tgtTable = new TableDefinition
        {
            Owner = "TGT", Name = "TB_CUSTOMER",
            Columns = [new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "INTERNAL_CODE", DataType = "NUMBER", ColumnId = 1 }]
        };

        var reference = new SchemaSnapshot { SchemaName = "REF", ExtractedAt = DateTime.Now, Tables = [refTable] };
        var target = new SchemaSnapshot { SchemaName = "TGT", ExtractedAt = DateTime.Now, Tables = [tgtTable] };

        var result = _engine.Compare(reference, target);

        result.Should().ContainSingle(d =>
            d.Severity == DifferenceSeverity.Warning &&
            d.Type == DifferenceType.ExtraColumn &&
            d.TableName == "TB_CUSTOMER" &&
            d.ColumnName == "INTERNAL_CODE");
    }

    [Fact]
    public void Compare_SameColumnsInBothTables_NoColumnDifferences()
    {
        var column = new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "ID", DataType = "NUMBER", ColumnId = 1 };
        var refTable = new TableDefinition { Owner = "REF", Name = "TB_CUSTOMER", Columns = [column] };
        var tgtTable = new TableDefinition { Owner = "TGT", Name = "TB_CUSTOMER", Columns = [column] };

        var reference = new SchemaSnapshot { SchemaName = "REF", ExtractedAt = DateTime.Now, Tables = [refTable] };
        var target = new SchemaSnapshot { SchemaName = "TGT", ExtractedAt = DateTime.Now, Tables = [tgtTable] };

        var result = _engine.Compare(reference, target);

        result.Should().BeEmpty();
    }

    // --- Card 3: column attribute comparisons ---

    private static (SchemaSnapshot reference, SchemaSnapshot target) BuildSnapshots(
        ColumnDefinition refCol, ColumnDefinition tgtCol)
    {
        var refTable = new TableDefinition { Owner = "REF", Name = "TB_CUSTOMER", Columns = [refCol] };
        var tgtTable = new TableDefinition { Owner = "TGT", Name = "TB_CUSTOMER", Columns = [tgtCol] };
        return (
            new SchemaSnapshot { SchemaName = "REF", ExtractedAt = DateTime.Now, Tables = [refTable] },
            new SchemaSnapshot { SchemaName = "TGT", ExtractedAt = DateTime.Now, Tables = [tgtTable] }
        );
    }

    [Fact]
    public void Compare_DataTypeMismatch_ReturnsCritical()
    {
        var (reference, target) = BuildSnapshots(
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "AMOUNT", DataType = "NUMBER", ColumnId = 1 },
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "AMOUNT", DataType = "VARCHAR2", ColumnId = 1 });

        var result = _engine.Compare(reference, target);

        result.Should().ContainSingle(d =>
            d.Severity == DifferenceSeverity.Critical &&
            d.Type == DifferenceType.DataTypeMismatch &&
            d.ReferenceValue == "NUMBER" && d.TargetValue == "VARCHAR2");
    }

    [Fact]
    public void Compare_DataTypeCaseInsensitive_NoDifference()
    {
        var (reference, target) = BuildSnapshots(
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "AMOUNT", DataType = "NUMBER", ColumnId = 1 },
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "AMOUNT", DataType = "number", ColumnId = 1 });

        var result = _engine.Compare(reference, target);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compare_DataLengthSmallerInTarget_ReturnsCritical()
    {
        var (reference, target) = BuildSnapshots(
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "NAME", DataType = "VARCHAR2", DataLength = 100, ColumnId = 1 },
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "NAME", DataType = "VARCHAR2", DataLength = 50, ColumnId = 1 });

        var result = _engine.Compare(reference, target);

        result.Should().ContainSingle(d =>
            d.Severity == DifferenceSeverity.Critical &&
            d.Type == DifferenceType.DataLengthSmaller &&
            d.ReferenceValue == "100" && d.TargetValue == "50");
    }

    [Fact]
    public void Compare_DataLengthLargerInTarget_ReturnsWarning()
    {
        var (reference, target) = BuildSnapshots(
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "NAME", DataType = "VARCHAR2", DataLength = 100, ColumnId = 1 },
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "NAME", DataType = "VARCHAR2", DataLength = 200, ColumnId = 1 });

        var result = _engine.Compare(reference, target);

        result.Should().ContainSingle(d =>
            d.Severity == DifferenceSeverity.Warning &&
            d.Type == DifferenceType.DataLengthLarger &&
            d.ReferenceValue == "100" && d.TargetValue == "200");
    }

    [Fact]
    public void Compare_DataLengthNullInBoth_NoDifference()
    {
        var (reference, target) = BuildSnapshots(
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "AMOUNT", DataType = "NUMBER", DataLength = null, ColumnId = 1 },
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "AMOUNT", DataType = "NUMBER", DataLength = null, ColumnId = 1 });

        var result = _engine.Compare(reference, target);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compare_PrecisionMismatch_ReturnsWarning()
    {
        var (reference, target) = BuildSnapshots(
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "AMOUNT", DataType = "NUMBER", DataPrecision = 10, ColumnId = 1 },
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "AMOUNT", DataType = "NUMBER", DataPrecision = null, ColumnId = 1 });

        var result = _engine.Compare(reference, target);

        result.Should().ContainSingle(d =>
            d.Severity == DifferenceSeverity.Warning &&
            d.Type == DifferenceType.PrecisionMismatch);
    }

    [Fact]
    public void Compare_ScaleMismatch_ReturnsWarning()
    {
        var (reference, target) = BuildSnapshots(
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "AMOUNT", DataType = "NUMBER", DataScale = 2, ColumnId = 1 },
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "AMOUNT", DataType = "NUMBER", DataScale = 4, ColumnId = 1 });

        var result = _engine.Compare(reference, target);

        result.Should().ContainSingle(d =>
            d.Severity == DifferenceSeverity.Warning &&
            d.Type == DifferenceType.ScaleMismatch);
    }

    [Fact]
    public void Compare_NullabilityMismatch_ReturnsWarning()
    {
        var (reference, target) = BuildSnapshots(
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "EMAIL", DataType = "VARCHAR2", Nullable = false, ColumnId = 1 },
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "EMAIL", DataType = "VARCHAR2", Nullable = true, ColumnId = 1 });

        var result = _engine.Compare(reference, target);

        result.Should().ContainSingle(d =>
            d.Severity == DifferenceSeverity.Warning &&
            d.Type == DifferenceType.NullabilityMismatch);
    }

    [Fact]
    public void Compare_DefaultValueMismatch_ReturnsWarning()
    {
        var (reference, target) = BuildSnapshots(
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "STATUS", DataType = "VARCHAR2", DataDefault = "0", ColumnId = 1 },
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "STATUS", DataType = "VARCHAR2", DataDefault = null, ColumnId = 1 });

        var result = _engine.Compare(reference, target);

        result.Should().ContainSingle(d =>
            d.Severity == DifferenceSeverity.Warning &&
            d.Type == DifferenceType.DefaultValueMismatch);
    }

    [Fact]
    public void Compare_DefaultValueNullInBoth_NoDifference()
    {
        var (reference, target) = BuildSnapshots(
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "STATUS", DataType = "VARCHAR2", DataDefault = null, ColumnId = 1 },
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "STATUS", DataType = "VARCHAR2", DataDefault = null, ColumnId = 1 });

        var result = _engine.Compare(reference, target);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compare_DefaultValueTrimmed_NoDifference()
    {
        var (reference, target) = BuildSnapshots(
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "STATUS", DataType = "VARCHAR2", DataDefault = " 0 ", ColumnId = 1 },
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "STATUS", DataType = "VARCHAR2", DataDefault = "0", ColumnId = 1 });

        var result = _engine.Compare(reference, target);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compare_DefaultValueCaseInsensitiveEqual_NoDifference()
    {
        var (reference, target) = BuildSnapshots(
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "CREATED", DataType = "DATE", DataDefault = "SYSDATE", ColumnId = 1 },
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "CREATED", DataType = "DATE", DataDefault = "sysdate", ColumnId = 1 });

        var result = _engine.Compare(reference, target);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compare_DataLengthNullInTargetOnly_NoLengthDifference()
    {
        // The engine only compares DataLength when both sides have a value.
        // If one side is null, no DataLength diff is emitted (DataType would
        // catch a real incompatibility separately).
        var (reference, target) = BuildSnapshots(
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "NAME", DataType = "VARCHAR2", DataLength = 100, ColumnId = 1 },
            new ColumnDefinition { TableName = "TB_CUSTOMER", Name = "NAME", DataType = "VARCHAR2", DataLength = null, ColumnId = 1 });

        var result = _engine.Compare(reference, target);

        result.Should().NotContain(d =>
            d.Type == DifferenceType.DataLengthSmaller ||
            d.Type == DifferenceType.DataLengthLarger);
    }
}
