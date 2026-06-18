using DbSchemaPreflight.Core.Models;

namespace DbSchemaPreflight.Oracle;

public sealed class OracleMetadataExtractor
{
    public SchemaSnapshot Extract(string connectionString, string schemaName)
    {
        using var connection = new OracleConnectionFactory().OpenConnection(connectionString);

        var tables  = new OracleTableReader().Read(connection, schemaName);
        var columns = new OracleColumnReader().Read(connection, schemaName);

        var tableDict = tables.ToDictionary(t => t.Name);

        foreach (var col in columns)
        {
            if (tableDict.TryGetValue(col.TableName, out var table))
                table.Columns.Add(col);
        }

        return new SchemaSnapshot
        {
            SchemaName  = schemaName.ToUpperInvariant(),
            ExtractedAt = DateTime.UtcNow,
            Tables      = tables
        };
    }
}
