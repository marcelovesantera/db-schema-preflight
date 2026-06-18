namespace DbSchemaPreflight.Core.Models;

public sealed class SqlSuggestion
{
    public required string Title { get; init; }
    public required string Sql { get; init; }
    public required SqlSuggestionRisk Risk { get; init; }
    public required bool IsDestructive { get; init; }
    public required bool RequiresManualReview { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string TableName { get; init; } = string.Empty;
    public string? ColumnName { get; init; }
}
