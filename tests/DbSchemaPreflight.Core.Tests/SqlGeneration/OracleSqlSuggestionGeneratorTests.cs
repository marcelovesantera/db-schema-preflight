using System.Reflection;
using FluentAssertions;
using DbSchemaPreflight.Core.Models;
using DbSchemaPreflight.Oracle.SqlGeneration;

namespace DbSchemaPreflight.Core.Tests.SqlGeneration;

public sealed class OracleSqlSuggestionGeneratorTests
{
    private readonly OracleSqlSuggestionGenerator _generator = new(new OracleDataTypeFormatter());

    // ── factory helpers ──────────────────────────────────────────────────────

    private static SchemaSnapshot Snapshot(string schema, params TableDefinition[] tables) =>
        new() { SchemaName = schema, Tables = tables.ToList() };

    private static TableDefinition Table(string name, params ColumnDefinition[] columns) =>
        new() { Owner = "REF", Name = name, Columns = columns.ToList() };

    private static ColumnDefinition Col(
        string tableName,
        string name,
        string dataType,
        int columnId     = 1,
        int? length      = null,
        int? precision   = null,
        int? scale       = null,
        bool nullable    = true,
        string? def      = null) =>
        new()
        {
            TableName      = tableName,
            Name           = name,
            DataType       = dataType,
            ColumnId       = columnId,
            DataLength     = length,
            DataPrecision  = precision,
            DataScale      = scale,
            Nullable       = nullable,
            DataDefault    = def
        };

    private static SchemaDifference Diff(
        DifferenceType type,
        string         tableName,
        string?        columnName      = null,
        string?        referenceValue  = null,
        string?        targetValue     = null) =>
        new()
        {
            Type           = type,
            Severity       = DifferenceSeverity.Critical,
            TableName      = tableName,
            ColumnName     = columnName,
            Message        = string.Empty,
            ReferenceValue = referenceValue,
            TargetValue    = targetValue
        };

    private static SchemaSnapshot EmptyTarget() => Snapshot("TGT");

    // ── MissingTable ─────────────────────────────────────────────────────────

    [Fact]
    public void Generate_MissingTable_ReturnsCreateTableSql()
    {
        var reference = Snapshot("REF",
            Table("ORDERS",
                Col("ORDERS", "ORDER_ID",   "NUMBER",   columnId: 1, precision: 10),
                Col("ORDERS", "ORDER_DATE", "DATE",     columnId: 2)));
        var target    = EmptyTarget();

        var results = _generator.Generate(Diff(DifferenceType.MissingTable, "ORDERS"), reference, target);

        results.Should().HaveCount(1);
        var s = results[0];
        s.Sql.Should().Contain("CREATE TABLE");
        s.Sql.Should().Contain("\"TGT\".\"ORDERS\"");
        s.Risk.Should().Be(SqlSuggestionRisk.Low);
        s.IsDestructive.Should().BeFalse();
        s.RequiresManualReview.Should().BeFalse();
    }

    [Fact]
    public void Generate_MissingTable_ColumnsOrderedByColumnId()
    {
        var reference = Snapshot("REF",
            Table("ITEMS",
                Col("ITEMS", "AMOUNT",  "NUMBER", columnId: 2),
                Col("ITEMS", "ITEM_ID", "NUMBER", columnId: 1)));
        var target = EmptyTarget();

        var results = _generator.Generate(Diff(DifferenceType.MissingTable, "ITEMS"), reference, target);

        var sql = results[0].Sql;
        sql.IndexOf("\"ITEM_ID\"").Should().BeLessThan(sql.IndexOf("\"AMOUNT\""));
    }

    [Fact]
    public void Generate_MissingTable_TableNotInSnapshot_ReturnsEmpty()
    {
        var reference = Snapshot("REF");
        var result    = _generator.Generate(Diff(DifferenceType.MissingTable, "GHOST"), reference, EmptyTarget());
        result.Should().BeEmpty();
    }

    [Fact]
    public void Generate_MissingTable_ColumnWithNotNull_ContainsNotNullConstraint()
    {
        var reference = Snapshot("REF",
            Table("ACCOUNTS",
                Col("ACCOUNTS", "CODE", "VARCHAR2", length: 20, nullable: false)));
        var target = EmptyTarget();

        var results = _generator.Generate(Diff(DifferenceType.MissingTable, "ACCOUNTS"), reference, target);

        results[0].Sql.Should().Contain("NOT NULL");
    }

    [Fact]
    public void Generate_MissingTable_ColumnWithDefault_ContainsDefaultClause()
    {
        var reference = Snapshot("REF",
            Table("ACCOUNTS",
                Col("ACCOUNTS", "STATUS", "VARCHAR2", length: 10, def: "'A'")));
        var target = EmptyTarget();

        var results = _generator.Generate(Diff(DifferenceType.MissingTable, "ACCOUNTS"), reference, target);

        results[0].Sql.Should().Contain("DEFAULT 'A'");
    }

    // ── MissingColumn ────────────────────────────────────────────────────────

    [Fact]
    public void Generate_MissingColumn_ReturnsAlterTableAddSql()
    {
        var reference = Snapshot("REF",
            Table("CLIENTS",
                Col("CLIENTS", "EMAIL", "VARCHAR2", length: 200)));
        var target = EmptyTarget();

        var results = _generator.Generate(
            Diff(DifferenceType.MissingColumn, "CLIENTS", columnName: "EMAIL"), reference, target);

        results.Should().HaveCount(1);
        var s = results[0];
        s.Sql.Should().Contain("ALTER TABLE \"TGT\".\"CLIENTS\"");
        s.Sql.Should().Contain("ADD");
        s.Sql.Should().Contain("\"EMAIL\" VARCHAR2(200 CHAR)");
        s.Risk.Should().Be(SqlSuggestionRisk.Low);
        s.IsDestructive.Should().BeFalse();
    }

    [Fact]
    public void Generate_MissingColumn_NotNullableWithDefault_IncludesConstraints()
    {
        var reference = Snapshot("REF",
            Table("CLIENTS",
                Col("CLIENTS", "STATUS", "VARCHAR2", length: 10, nullable: false, def: "'A'")));
        var target = EmptyTarget();

        var results = _generator.Generate(
            Diff(DifferenceType.MissingColumn, "CLIENTS", "STATUS"), reference, target);

        var sql = results[0].Sql;
        sql.Should().Contain("DEFAULT 'A'");
        sql.Should().Contain("NOT NULL");
    }

    [Fact]
    public void Generate_MissingColumn_ColumnNotInSnapshot_ReturnsEmpty()
    {
        var reference = Snapshot("REF", Table("CLIENTS"));
        var result    = _generator.Generate(
            Diff(DifferenceType.MissingColumn, "CLIENTS", "GHOST"), reference, EmptyTarget());
        result.Should().BeEmpty();
    }

    // ── DataTypeMismatch ─────────────────────────────────────────────────────

    [Fact]
    public void Generate_DataTypeMismatch_ReturnsAlterModifyWithHighRiskAndWarnings()
    {
        var reference = Snapshot("REF",
            Table("ORDERS", Col("ORDERS", "NOTE", "CLOB")));
        var target = EmptyTarget();

        var results = _generator.Generate(
            Diff(DifferenceType.DataTypeMismatch, "ORDERS", "NOTE"), reference, target);

        results.Should().HaveCount(1);
        var s = results[0];
        s.Sql.Should().Contain("MODIFY");
        s.Sql.Should().Contain("-- ATENÇÃO:");
        s.Risk.Should().Be(SqlSuggestionRisk.High);
        s.Warnings.Should().NotBeEmpty();
        s.RequiresManualReview.Should().BeTrue();
    }

    // ── DataLengthSmaller ────────────────────────────────────────────────────

    [Fact]
    public void Generate_DataLengthSmaller_ReturnsHighRiskWithWarnings()
    {
        var reference = Snapshot("REF",
            Table("USERS", Col("USERS", "NAME", "VARCHAR2", length: 100)));
        var target = EmptyTarget();

        var results = _generator.Generate(
            Diff(DifferenceType.DataLengthSmaller, "USERS", "NAME"), reference, target);

        var s = results[0];
        s.Risk.Should().Be(SqlSuggestionRisk.High);
        s.Warnings.Should().NotBeEmpty();
        s.Sql.Should().Contain("MODIFY");
        s.Sql.Should().Contain("-- ATENÇÃO:");
    }

    // ── DataLengthLarger ─────────────────────────────────────────────────────

    [Fact]
    public void Generate_DataLengthLarger_ReturnsLowRiskNoWarnings()
    {
        var reference = Snapshot("REF",
            Table("USERS", Col("USERS", "NAME", "VARCHAR2", length: 200)));
        var target = EmptyTarget();

        var results = _generator.Generate(
            Diff(DifferenceType.DataLengthLarger, "USERS", "NAME"), reference, target);

        var s = results[0];
        s.Risk.Should().Be(SqlSuggestionRisk.Low);
        s.Warnings.Should().BeEmpty();
        s.Sql.Should().Contain("MODIFY");
        s.Sql.Should().NotContain("-- ATENÇÃO:");
    }

    // ── PrecisionMismatch ────────────────────────────────────────────────────

    [Fact]
    public void Generate_PrecisionMismatch_ReturnsMediumRiskWithWarnings()
    {
        var reference = Snapshot("REF",
            Table("INVOICES", Col("INVOICES", "TOTAL", "NUMBER", precision: 12, scale: 2)));
        var target = EmptyTarget();

        var results = _generator.Generate(
            Diff(DifferenceType.PrecisionMismatch, "INVOICES", "TOTAL"), reference, target);

        var s = results[0];
        s.Risk.Should().Be(SqlSuggestionRisk.Medium);
        s.Warnings.Should().NotBeEmpty();
    }

    // ── ScaleMismatch ────────────────────────────────────────────────────────

    [Fact]
    public void Generate_ScaleMismatch_ReturnsMediumRiskWithWarnings()
    {
        var reference = Snapshot("REF",
            Table("INVOICES", Col("INVOICES", "RATE", "NUMBER", precision: 5, scale: 4)));
        var target = EmptyTarget();

        var results = _generator.Generate(
            Diff(DifferenceType.ScaleMismatch, "INVOICES", "RATE"), reference, target);

        var s = results[0];
        s.Risk.Should().Be(SqlSuggestionRisk.Medium);
        s.Warnings.Should().NotBeEmpty();
    }

    // ── NullabilityMismatch ───────────────────────────────────────────────────

    [Fact]
    public void Generate_NullabilityMismatch_ReferenceNullable_ReturnsModifyNullMediumRisk()
    {
        var reference = Snapshot("REF", Table("USERS"));
        var target    = EmptyTarget();

        var results = _generator.Generate(
            Diff(DifferenceType.NullabilityMismatch, "USERS", "STATUS", referenceValue: "True"), reference, target);

        var s = results[0];
        s.Sql.Should().Contain("MODIFY");
        s.Sql.Should().Contain("\"STATUS\" NULL");
        s.Risk.Should().Be(SqlSuggestionRisk.Medium);
        s.IsDestructive.Should().BeFalse();
        s.RequiresManualReview.Should().BeFalse();
        s.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Generate_NullabilityMismatch_ReferenceNotNull_ReturnsModifyNotNullHighRisk()
    {
        var reference = Snapshot("REF", Table("USERS"));
        var target    = EmptyTarget();

        var results = _generator.Generate(
            Diff(DifferenceType.NullabilityMismatch, "USERS", "STATUS", referenceValue: "False"), reference, target);

        var s = results[0];
        s.Sql.Should().Contain("-- ATENÇÃO:");
        s.Sql.Should().Contain("\"STATUS\" NOT NULL");
        s.Risk.Should().Be(SqlSuggestionRisk.High);
        s.RequiresManualReview.Should().BeTrue();
        s.Warnings.Should().NotBeEmpty();
    }

    // ── DefaultValueMismatch ─────────────────────────────────────────────────

    [Fact]
    public void Generate_DefaultValueMismatch_ReturnsModifyDefaultLowRisk()
    {
        var reference = Snapshot("REF", Table("ORDERS"));
        var target    = EmptyTarget();

        var results = _generator.Generate(
            Diff(DifferenceType.DefaultValueMismatch, "ORDERS", "STATUS", referenceValue: "'PENDING'"), reference, target);

        var s = results[0];
        s.Sql.Should().Contain("ALTER TABLE \"TGT\".\"ORDERS\"");
        s.Sql.Should().Contain("MODIFY");
        s.Sql.Should().Contain("DEFAULT 'PENDING'");
        s.Risk.Should().Be(SqlSuggestionRisk.Low);
        s.IsDestructive.Should().BeFalse();
        s.RequiresManualReview.Should().BeFalse();
    }

    [Fact]
    public void Generate_DefaultValueMismatch_NullReferenceValue_UsesNullKeyword()
    {
        var reference = Snapshot("REF", Table("ORDERS"));
        var target    = EmptyTarget();

        var results = _generator.Generate(
            Diff(DifferenceType.DefaultValueMismatch, "ORDERS", "STATUS", referenceValue: null), reference, target);

        results[0].Sql.Should().Contain("DEFAULT NULL");
    }

    // ── ExtraColumn ──────────────────────────────────────────────────────────

    [Fact]
    public void Generate_ExtraColumn_ReturnsCommentedDropColumnHighRiskDestructive()
    {
        var reference = Snapshot("REF", Table("CLIENTS"));
        var target    = EmptyTarget();

        var results = _generator.Generate(
            Diff(DifferenceType.ExtraColumn, "CLIENTS", "LEGACY_ID"), reference, target);

        results.Should().HaveCount(1);
        var s = results[0];
        s.Sql.Should().StartWith("--");
        s.Sql.Should().Contain("DROP COLUMN");
        s.Sql.Should().Contain("\"TGT\".\"CLIENTS\"");
        s.Sql.Should().Contain("\"LEGACY_ID\"");
        s.Risk.Should().Be(SqlSuggestionRisk.High);
        s.IsDestructive.Should().BeTrue();
        s.RequiresManualReview.Should().BeTrue();
        s.Warnings.Should().NotBeEmpty();
    }

    // ── ExtraTable ───────────────────────────────────────────────────────────

    [Fact]
    public void Generate_ExtraTable_ReturnsCommentedDropTableHighRiskDestructive()
    {
        var reference = Snapshot("REF");
        var target    = EmptyTarget();

        var results = _generator.Generate(
            Diff(DifferenceType.ExtraTable, "LEGACY_LOG"), reference, target);

        results.Should().HaveCount(1);
        var s = results[0];
        s.Sql.Should().StartWith("--");
        s.Sql.Should().Contain("DROP TABLE");
        s.Sql.Should().Contain("\"TGT\".\"LEGACY_LOG\"");
        s.Risk.Should().Be(SqlSuggestionRisk.High);
        s.IsDestructive.Should().BeTrue();
        s.RequiresManualReview.Should().BeTrue();
        s.Warnings.Should().NotBeEmpty();
    }

    // ── Quoted identifiers ────────────────────────────────────────────────────

    [Fact]
    public void Generate_AllSql_ContainsDoubleQuotedIdentifiers()
    {
        var reference = Snapshot("MY_REF",
            Table("MY_TABLE", Col("MY_TABLE", "MY_COL", "VARCHAR2", length: 50)));
        var target = Snapshot("MY_TGT");

        var results = _generator.Generate(
            Diff(DifferenceType.MissingColumn, "MY_TABLE", "MY_COL"), reference, target);

        results[0].Sql.Should().Contain("\"MY_TGT\"");
        results[0].Sql.Should().Contain("\"MY_TABLE\"");
        results[0].Sql.Should().Contain("\"MY_COL\"");
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_UnknownDifferenceType_ReturnsEmpty()
    {
        var diff = new SchemaDifference
        {
            Type      = (DifferenceType)999,
            Severity  = DifferenceSeverity.Info,
            TableName = "ANY",
            Message   = string.Empty
        };

        var result = _generator.Generate(diff, Snapshot("REF"), EmptyTarget());

        result.Should().BeEmpty();
    }

    // ── Security ──────────────────────────────────────────────────────────────

    [Fact]
    public void OracleSqlSuggestionGenerator_HasNoExecutionMethods()
    {
        var forbiddenTerms = new[] { "Execute", "Apply", "Run", "Migration" };

        var publicMethods = typeof(OracleSqlSuggestionGenerator)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToList();

        foreach (var term in forbiddenTerms)
            publicMethods.Should().NotContain(
                m => m.Contains(term, StringComparison.OrdinalIgnoreCase),
                because: $"the generator must not expose any '{term}' method");
    }
}
