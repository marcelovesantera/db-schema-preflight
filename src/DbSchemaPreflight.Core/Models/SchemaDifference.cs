namespace DbSchemaPreflight.Core.Models;

public sealed class SchemaDifference
{
    public DifferenceSeverity Severity { get; init; }
    public DifferenceType Type { get; init; }
    public string ObjectType { get; init; } = string.Empty;
    public string TableName { get; init; } = string.Empty;
    public string? ColumnName { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? ReferenceValue { get; init; }
    public string? TargetValue { get; init; }
}
