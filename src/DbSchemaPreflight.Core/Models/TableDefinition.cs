namespace DbSchemaPreflight.Core.Models;

public sealed class TableDefinition
{
    public string Owner { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public List<ColumnDefinition> Columns { get; init; } = new();
}
