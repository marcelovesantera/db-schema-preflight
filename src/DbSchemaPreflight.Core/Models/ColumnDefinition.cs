namespace DbSchemaPreflight.Core.Models;

public sealed class ColumnDefinition
{
    public string TableName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty;
    public int? DataLength { get; init; }
    public int? DataPrecision { get; init; }
    public int? DataScale { get; init; }
    public bool Nullable { get; init; }
    public string? DataDefault { get; init; }
    public int ColumnId { get; init; }
}
