using Oracle.ManagedDataAccess.Client;
using DbSchemaPreflight.Core.Models;

namespace DbSchemaPreflight.Oracle;

public sealed class OracleColumnReader
{
    public List<ColumnDefinition> Read(OracleConnection connection, string schemaName)
    {
        const string SQL = """
            SELECT table_name, column_name, data_type, data_length,
                   data_precision, data_scale, nullable, data_default, column_id
            FROM all_tab_columns
            WHERE owner = :schema
            ORDER BY table_name, column_id
            """;

        using var command = new OracleCommand(SQL, connection);
        command.Parameters.Add(new OracleParameter("schema", schemaName.ToUpperInvariant()));

        using var reader = command.ExecuteReader();

        var columns = new List<ColumnDefinition>();
        while (reader.Read())
        {
            columns.Add(new ColumnDefinition
            {
                TableName  = reader["table_name"].ToString()!.ToUpperInvariant(),
                Name       = reader["column_name"].ToString()!.ToUpperInvariant(),
                DataType   = reader["data_type"].ToString()!.ToUpperInvariant(),
                DataLength    = reader["data_length"]    is DBNull ? null : Convert.ToInt32(reader["data_length"]),
                DataPrecision = reader["data_precision"] is DBNull ? null : Convert.ToInt32(reader["data_precision"]),
                DataScale     = reader["data_scale"]     is DBNull ? null : Convert.ToInt32(reader["data_scale"]),
                Nullable   = reader["nullable"].ToString() == "Y",
                DataDefault = reader["data_default"] is DBNull ? null : reader["data_default"].ToString()?.Trim(),
                ColumnId   = Convert.ToInt32(reader["column_id"])
            });
        }

        return columns;
    }
}
