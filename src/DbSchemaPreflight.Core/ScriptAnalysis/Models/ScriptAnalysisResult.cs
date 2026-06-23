namespace DbSchemaPreflight.Core.ScriptAnalysis.Models;

public sealed class ScriptAnalysisResult
{
    public string FilePath { get; init; }
    public DateTime AnalysedAt { get; init; }
    public IReadOnlyList<StatementResult> Statements { get; init; }

    public int OkCount => Statements.Count(s => s.Status == ValidationStatus.Ok);
    public int ErrorCount => Statements.Count(s => s.Status == ValidationStatus.Error);
    public int WarningCount => Statements.Count(s => s.Status == ValidationStatus.Warning);
    public int SkippedCount => Statements.Count(s => s.Status == ValidationStatus.Skipped);

    public ScriptAnalysisResult(string filePath, DateTime analysedAt, IReadOnlyList<StatementResult> statements)
    {
        FilePath = filePath;
        AnalysedAt = analysedAt;
        Statements = statements;
    }
}
