using FluentAssertions;
using DbSchemaPreflight.Core.Models;
using DbSchemaPreflight.Reporting;
using DbSchemaPreflight.Reporting.Models;

namespace DbSchemaPreflight.Reporting.Tests;

public sealed class HtmlReportGeneratorTests
{
    private readonly HtmlReportGenerator _generator = new();

    [Fact]
    public void Generate_WithCriticalDifferences_ContainsNotReady()
    {
        var model = new ReportModel
        {
            ReferenceSchema = "APP_REF",
            TargetSchema = "APP_TARGET",
            GeneratedAt = DateTime.Now,
            Summary = new ReportSummary(
                totalTablesInReference: 3,
                totalTablesInTarget: 2,
                totalDifferences: 2,
                criticalCount: 2,
                warningCount: 0,
                infoCount: 0),
            Differences =
            [
                new SchemaDifference
                {
                    Severity = DifferenceSeverity.Critical,
                    Type = DifferenceType.MissingTable,
                    ObjectType = "TABLE",
                    TableName = "TB_ORDERS",
                    Message = "Table exists in reference but is missing in target."
                },
                new SchemaDifference
                {
                    Severity = DifferenceSeverity.Critical,
                    Type = DifferenceType.MissingTable,
                    ObjectType = "TABLE",
                    TableName = "TB_PAYMENTS",
                    Message = "Table exists in reference but is missing in target."
                }
            ]
        };

        var html = _generator.Generate(model);

        html.Should().Contain("NOT READY");
    }

    [Fact]
    public void Generate_WithNoDifferences_ContainsReady()
    {
        var model = new ReportModel
        {
            ReferenceSchema = "APP_REF",
            TargetSchema = "APP_TARGET",
            GeneratedAt = DateTime.Now,
            Summary = new ReportSummary(
                totalTablesInReference: 3,
                totalTablesInTarget: 3,
                totalDifferences: 0,
                criticalCount: 0,
                warningCount: 0,
                infoCount: 0),
            Differences = []
        };

        var html = _generator.Generate(model);

        html.Should().Contain("READY");
    }

    [Fact]
    public void Generate_WithWarningAndNoCritical_ContainsNeedsReview()
    {
        var model = new ReportModel
        {
            ReferenceSchema = "APP_REF",
            TargetSchema = "APP_TARGET",
            GeneratedAt = DateTime.Now,
            Summary = new ReportSummary(
                totalTablesInReference: 3,
                totalTablesInTarget: 3,
                totalDifferences: 1,
                criticalCount: 0,
                warningCount: 1,
                infoCount: 0),
            Differences =
            [
                new SchemaDifference
                {
                    Severity = DifferenceSeverity.Warning,
                    Type = DifferenceType.NullabilityMismatch,
                    ObjectType = "COLUMN",
                    TableName = "TB_CUSTOMER",
                    ColumnName = "EMAIL",
                    Message = "Column nullability differs.",
                    ReferenceValue = "NOT NULL",
                    TargetValue = "NULL"
                }
            ]
        };

        var html = _generator.Generate(model);

        html.Should().Contain("NEEDS REVIEW");
    }

    [Fact]
    public void Generate_WithNoDifferences_ContainsEqualSchemasMessageInByTableSection()
    {
        var model = new ReportModel
        {
            ReferenceSchema = "APP_REF",
            TargetSchema = "APP_TARGET",
            GeneratedAt = DateTime.Now,
            Summary = new ReportSummary(
                totalTablesInReference: 2,
                totalTablesInTarget: 2,
                totalDifferences: 0,
                criticalCount: 0,
                warningCount: 0,
                infoCount: 0),
            Differences = []
        };

        var html = _generator.Generate(model);

        html.Should().Contain("All tables are structurally equal.");
    }

    [Fact]
    public void Generate_ContainsReferenceAndTargetSchemaNames()
    {
        var model = new ReportModel
        {
            ReferenceSchema = "APP_REF",
            TargetSchema = "APP_TARGET",
            GeneratedAt = DateTime.Now,
            Summary = new ReportSummary(
                totalTablesInReference: 0,
                totalTablesInTarget: 0,
                totalDifferences: 0,
                criticalCount: 0,
                warningCount: 0,
                infoCount: 0),
            Differences = []
        };

        var html = _generator.Generate(model);

        html.Should().Contain("APP_REF");
        html.Should().Contain("APP_TARGET");
    }

    [Fact]
    public void Generate_ContainsFiveTabPanels()
    {
        var model = new ReportModel
        {
            ReferenceSchema = "APP_REF",
            TargetSchema = "APP_TARGET",
            GeneratedAt = DateTime.Now,
            Summary = new ReportSummary(0, 0, 0, 0, 0, 0),
            Differences = []
        };

        var html = _generator.Generate(model);

        html.Should().Contain("Tables Only in Reference");
        html.Should().Contain("Tables Only in Target");
        html.Should().Contain("Differences by Severity");
        html.Should().Contain("Differences by Table");
        html.Should().Contain("only-reference");
        html.Should().Contain("only-target");
    }

    [Fact]
    public void Generate_WithOnlyMissingTableDiffs_ShowsNoDifferencesInSeverityAndByTableTabs()
    {
        var model = new ReportModel
        {
            ReferenceSchema = "APP_REF",
            TargetSchema = "APP_TARGET",
            GeneratedAt = DateTime.Now,
            Summary = new ReportSummary(1, 0, 1, 1, 0, 0),
            Differences =
            [
                new SchemaDifference
                {
                    Severity = DifferenceSeverity.Critical,
                    Type = DifferenceType.MissingTable,
                    ObjectType = "TABLE",
                    TableName = "TB_ORDERS",
                    Message = "Table exists in reference but is missing in target."
                }
            ]
        };

        var html = _generator.Generate(model);

        html.Should().Contain("No differences found.");
        html.Should().Contain("All tables are structurally equal.");
    }

    [Fact]
    public void Generate_WithMissingTableDiff_TableNameAppearsInReport()
    {
        var model = new ReportModel
        {
            ReferenceSchema = "APP_REF",
            TargetSchema = "APP_TARGET",
            GeneratedAt = DateTime.Now,
            Summary = new ReportSummary(1, 0, 1, 1, 0, 0),
            Differences =
            [
                new SchemaDifference
                {
                    Severity = DifferenceSeverity.Critical,
                    Type = DifferenceType.MissingTable,
                    ObjectType = "TABLE",
                    TableName = "TB_ORDERS",
                    Message = "Table exists in reference but is missing in target."
                }
            ]
        };

        var html = _generator.Generate(model);

        html.Should().Contain("TB_ORDERS");
    }

    [Fact]
    public void Generate_WithMissingTableDiff_DoesNotCreateTableGroupInByTableSection()
    {
        var model = new ReportModel
        {
            ReferenceSchema = "APP_REF",
            TargetSchema = "APP_TARGET",
            GeneratedAt = DateTime.Now,
            Summary = new ReportSummary(1, 0, 1, 1, 0, 0),
            Differences =
            [
                new SchemaDifference
                {
                    Severity = DifferenceSeverity.Critical,
                    Type = DifferenceType.MissingTable,
                    ObjectType = "TABLE",
                    TableName = "TB_ORDERS",
                    Message = "Table exists in reference but is missing in target."
                }
            ]
        };

        var html = _generator.Generate(model);

        html.Should().NotContain("class=\"table-group\" data-table-name=\"TB_ORDERS\"");
    }

    [Fact]
    public void Generate_WithExtraTableDiff_TableNameAppearsInReport()
    {
        var model = new ReportModel
        {
            ReferenceSchema = "APP_REF",
            TargetSchema = "APP_TARGET",
            GeneratedAt = DateTime.Now,
            Summary = new ReportSummary(0, 1, 1, 0, 0, 1),
            Differences =
            [
                new SchemaDifference
                {
                    Severity = DifferenceSeverity.Info,
                    Type = DifferenceType.ExtraTable,
                    ObjectType = "TABLE",
                    TableName = "TB_AUDIT_LOG",
                    Message = "Table exists in target but is not in reference."
                }
            ]
        };

        var html = _generator.Generate(model);

        html.Should().Contain("TB_AUDIT_LOG");
    }

    [Fact]
    public void Generate_WithExtraTableDiff_DoesNotCreateTableGroupInByTableSection()
    {
        var model = new ReportModel
        {
            ReferenceSchema = "APP_REF",
            TargetSchema = "APP_TARGET",
            GeneratedAt = DateTime.Now,
            Summary = new ReportSummary(0, 1, 1, 0, 0, 1),
            Differences =
            [
                new SchemaDifference
                {
                    Severity = DifferenceSeverity.Info,
                    Type = DifferenceType.ExtraTable,
                    ObjectType = "TABLE",
                    TableName = "TB_AUDIT_LOG",
                    Message = "Table exists in target but is not in reference."
                }
            ]
        };

        var html = _generator.Generate(model);

        html.Should().NotContain("class=\"table-group\" data-table-name=\"TB_AUDIT_LOG\"");
    }

    [Fact]
    public void Generate_WithTableGroupContainingMixedSeverities_RendersCriticalBeforeWarning()
    {
        var model = new ReportModel
        {
            ReferenceSchema = "APP_REF",
            TargetSchema = "APP_TARGET",
            GeneratedAt = DateTime.Now,
            Summary = new ReportSummary(1, 1, 2, 1, 1, 0),
            Differences =
            [
                // Warning added before Critical — sort must reorder to Critical first
                new SchemaDifference
                {
                    Severity = DifferenceSeverity.Warning,
                    Type = DifferenceType.NullabilityMismatch,
                    ObjectType = "COLUMN",
                    TableName = "TB_ORDERS",
                    ColumnName = "EMAIL",
                    Message = "Column nullability differs."
                },
                new SchemaDifference
                {
                    Severity = DifferenceSeverity.Critical,
                    Type = DifferenceType.MissingColumn,
                    ObjectType = "COLUMN",
                    TableName = "TB_ORDERS",
                    ColumnName = "AMOUNT",
                    Message = "Column 'AMOUNT' is missing in target."
                }
            ]
        };

        var html = _generator.Generate(model);

        var criticalBadgeIdx = html.IndexOf("diff-badge critical");
        var warningBadgeIdx = html.IndexOf("diff-badge warning");
        criticalBadgeIdx.Should().BeLessThan(warningBadgeIdx);
    }

    [Fact]
    public void Generate_DoesNotContainInfoSummaryCard()
    {
        var model = new ReportModel
        {
            ReferenceSchema = "APP_REF",
            TargetSchema = "APP_TARGET",
            GeneratedAt = DateTime.Now,
            Summary = new ReportSummary(0, 1, 1, 0, 0, 1),
            Differences =
            [
                new SchemaDifference
                {
                    Severity = DifferenceSeverity.Info,
                    Type = DifferenceType.ExtraTable,
                    ObjectType = "TABLE",
                    TableName = "TB_AUDIT_LOG",
                    Message = "Table exists in target but is not in reference."
                }
            ]
        };

        var html = _generator.Generate(model);

        html.Should().NotContain("summary-card info");
    }

    // ── SuggestionsByDiff integration ─────────────────────────────────────────

    [Fact]
    public void Generate_WithSuggestion_ContainsSuggestionBlockInReport()
    {
        var diff = new SchemaDifference
        {
            Severity   = DifferenceSeverity.Critical,
            Type       = DifferenceType.MissingColumn,
            ObjectType = "COLUMN",
            TableName  = "TB_ORDERS",
            ColumnName = "AMOUNT",
            Message    = "Column AMOUNT is missing in target."
        };

        var suggestion = new SqlSuggestion
        {
            Title                = "Add column TB_ORDERS.AMOUNT",
            Sql                  = "ALTER TABLE \"TGT\".\"TB_ORDERS\"\nADD (\n    \"AMOUNT\" NUMBER\n);",
            Risk                 = SqlSuggestionRisk.Low,
            IsDestructive        = false,
            RequiresManualReview = false
        };

        var model = new ReportModel
        {
            ReferenceSchema   = "APP_REF",
            TargetSchema      = "APP_TARGET",
            GeneratedAt       = DateTime.Now,
            Summary           = new ReportSummary(1, 1, 1, 1, 0, 0),
            Differences       = [diff],
            SuggestionsByDiff = new Dictionary<SchemaDifference, IReadOnlyList<SqlSuggestion>>
            {
                [diff] = [suggestion]
            }
        };

        var html = _generator.Generate(model);

        // The template renders a toggle button when suggestion_sql is present
        html.Should().Contain("Ver sugestão SQL");
        html.Should().Contain("suggestion-panel");
    }

    [Fact]
    public void Generate_WithSuggestion_SuggestionSqlAppearsInReport()
    {
        var diff = new SchemaDifference
        {
            Severity   = DifferenceSeverity.Warning,
            Type       = DifferenceType.NullabilityMismatch,
            ObjectType = "COLUMN",
            TableName  = "TB_CLIENTS",
            ColumnName = "EMAIL",
            Message    = "Nullability differs."
        };

        // Avoid double-quotes in SQL so HtmlEncode does not transform the assertion string
        var suggestion = new SqlSuggestion
        {
            Title                = "Set NOT NULL on TB_CLIENTS.EMAIL",
            Sql                  = "ALTER TABLE TGT.TB_CLIENTS MODIFY (EMAIL NOT NULL);",
            Risk                 = SqlSuggestionRisk.High,
            IsDestructive        = false,
            RequiresManualReview = true,
            Warnings             = ["May fail if nulls exist."]
        };

        var model = new ReportModel
        {
            ReferenceSchema   = "APP_REF",
            TargetSchema      = "APP_TARGET",
            GeneratedAt       = DateTime.Now,
            Summary           = new ReportSummary(1, 1, 1, 0, 1, 0),
            Differences       = [diff],
            SuggestionsByDiff = new Dictionary<SchemaDifference, IReadOnlyList<SqlSuggestion>>
            {
                [diff] = [suggestion]
            }
        };

        var html = _generator.Generate(model);

        html.Should().Contain("ALTER TABLE TGT.TB_CLIENTS MODIFY (EMAIL NOT NULL);");
    }
}
