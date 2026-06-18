using Oracle.ManagedDataAccess.Client;
using DbSchemaPreflight.Core.Models;

namespace DbSchemaPreflight.Oracle;

public sealed class OracleTableReader
{
    public List<TableDefinition> Read(OracleConnection connection, string schemaName)
    {
        const string SQL = "SELECT owner, table_name FROM all_tables WHERE owner = :schema ORDER BY table_name";

        using var command = new OracleCommand(SQL, connection);
        command.Parameters.Add(new OracleParameter("schema", schemaName.ToUpperInvariant()));

        using var reader = command.ExecuteReader();

        var tables = new List<TableDefinition>();
        while (reader.Read())
        {
            tables.Add(new TableDefinition
            {
                Owner = reader["owner"].ToString()!.ToUpperInvariant(),
                Name = reader["table_name"].ToString()!.ToUpperInvariant(),
                Columns = new List<ColumnDefinition>()
            });
        }

        return tables;
    }
}
