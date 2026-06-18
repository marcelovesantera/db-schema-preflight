using DbSchemaPreflight.Core.Models;

namespace DbSchemaPreflight.Reporting.Models;

public sealed class ReportModel
{
    public string ReferenceSchema { get; init; } = string.Empty;
    public string TargetSchema { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; }
    public ReportSummary Summary { get; init; } = null!;
    public List<SchemaDifference> Differences { get; init; } = [];
    public IReadOnlyDictionary<SchemaDifference, IReadOnlyList<SqlSuggestion>> SuggestionsByDiff { get; init; }
        = new Dictionary<SchemaDifference, IReadOnlyList<SqlSuggestion>>();
}
