namespace DbSchemaPreflight.Reporting.ScriptAnalysis;

public sealed class ScriptAnalysisReportModel
{
    public string FilePath { get; init; } = string.Empty;
    public string Schema { get; init; } = string.Empty;
    public DateTime AnalysedAt { get; init; }
    public int OkCount { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public int SkippedCount { get; init; }
    public IReadOnlyList<StatementResultViewModel> Statements { get; init; } = [];
}
