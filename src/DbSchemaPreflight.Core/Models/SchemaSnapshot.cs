namespace DbSchemaPreflight.Core.Models;

public sealed class SchemaSnapshot
{
    public string SchemaName { get; init; } = string.Empty;
    public DateTime ExtractedAt { get; init; }
    public List<TableDefinition> Tables { get; init; } = new();
}
