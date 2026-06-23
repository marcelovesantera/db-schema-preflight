namespace DbSchemaPreflight.Core.ScriptAnalysis.Models;

public sealed class StatementResult
{
    public int SequenceNumber { get; init; }
    public string StatementText { get; init; }
    public StatementType Type { get; init; }
    public ValidationStatus Status { get; init; }
    public string? ErrorReason { get; init; }
    public IReadOnlyList<string> Warnings { get; init; }
    public string? ResilienceSuggestion { get; init; }
    public IReadOnlyList<string> RuntimeDependentFields { get; init; }

    public StatementResult(
        int sequenceNumber,
        string statementText,
        StatementType type,
        ValidationStatus status,
        string? errorReason = null,
        IReadOnlyList<string>? warnings = null,
        string? resilienceSuggestion = null,
        IReadOnlyList<string>? runtimeDependentFields = null)
    {
        SequenceNumber = sequenceNumber;
        StatementText = statementText;
        Type = type;
        Status = status;
        ErrorReason = errorReason;
        Warnings = warnings ?? [];
        ResilienceSuggestion = resilienceSuggestion;
        RuntimeDependentFields = runtimeDependentFields ?? [];
    }
}
