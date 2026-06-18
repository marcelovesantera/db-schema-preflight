using FluentAssertions;
using DbSchemaPreflight.Reporting.Models;

namespace DbSchemaPreflight.Reporting.Tests;

public sealed class ReportSummaryTests
{
    [Fact]
    public void ReadinessStatus_WithCriticalCount_ReturnsNotReady()
    {
        var summary = new ReportSummary(
            totalTablesInReference: 3,
            totalTablesInTarget: 3,
            totalDifferences: 1,
            criticalCount: 1,
            warningCount: 0,
            infoCount: 0);

        summary.ReadinessStatus.Should().Be("NOT READY");
    }

    [Fact]
    public void ReadinessStatus_WithWarningCountAndNoCritical_ReturnsNeedsReview()
    {
        var summary = new ReportSummary(
            totalTablesInReference: 3,
            totalTablesInTarget: 3,
            totalDifferences: 3,
            criticalCount: 0,
            warningCount: 3,
            infoCount: 0);

        summary.ReadinessStatus.Should().Be("NEEDS REVIEW");
    }

    [Fact]
    public void ReadinessStatus_WithInfoCountOnly_ReturnsReady()
    {
        var summary = new ReportSummary(
            totalTablesInReference: 5,
            totalTablesInTarget: 5,
            totalDifferences: 2,
            criticalCount: 0,
            warningCount: 0,
            infoCount: 2);

        summary.ReadinessStatus.Should().Be("READY");
    }

    [Fact]
    public void ReadinessStatus_WithAllZeros_ReturnsReady()
    {
        var summary = new ReportSummary(
            totalTablesInReference: 0,
            totalTablesInTarget: 0,
            totalDifferences: 0,
            criticalCount: 0,
            warningCount: 0,
            infoCount: 0);

        summary.ReadinessStatus.Should().Be("READY");
    }
}
