namespace DbSchemaPreflight.Reporting.Models;

public sealed class ReportSummary
{
    public int TotalTablesInReference { get; init; }
    public int TotalTablesInTarget { get; init; }
    public int TotalDifferences { get; init; }
    public int CriticalCount { get; init; }
    public int WarningCount { get; init; }
    public int InfoCount { get; init; }
    public string ReadinessStatus { get; }

    public ReportSummary(
        int totalTablesInReference,
        int totalTablesInTarget,
        int totalDifferences,
        int criticalCount,
        int warningCount,
        int infoCount)
    {
        TotalTablesInReference = totalTablesInReference;
        TotalTablesInTarget = totalTablesInTarget;
        TotalDifferences = totalDifferences;
        CriticalCount = criticalCount;
        WarningCount = warningCount;
        InfoCount = infoCount;
        ReadinessStatus = CalculateReadinessStatus(criticalCount, warningCount);
    }

    public static string CalculateReadinessStatus(int criticalCount, int warningCount)
    {
        if (criticalCount > 0)
            return "NOT READY";
        if (warningCount > 0)
            return "NEEDS REVIEW";
        return "READY";
    }
}
