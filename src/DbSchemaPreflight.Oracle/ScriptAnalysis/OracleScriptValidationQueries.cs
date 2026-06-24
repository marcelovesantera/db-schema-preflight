using DbSchemaPreflight.Core.ScriptAnalysis;
using Oracle.ManagedDataAccess.Client;

namespace DbSchemaPreflight.Oracle.ScriptAnalysis;

public sealed class OracleScriptValidationQueries : IScriptValidationQueries
{
    private readonly string _connectionString;

    public OracleScriptValidationQueries(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> TableExistsAsync(string schema, string tableName)
    {
        const string SQL = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER = :p_owner AND TABLE_NAME = :p_tname";
        using var conn = await OpenAsync();
        using var cmd = new OracleCommand(SQL, conn);
        cmd.Parameters.Add(new OracleParameter("p_owner", schema.ToUpperInvariant()));
        cmd.Parameters.Add(new OracleParameter("p_tname", tableName.ToUpperInvariant()));
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task<bool> ColumnExistsAsync(string schema, string tableName, string columnName)
    {
        const string SQL = "SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER = :p_owner AND TABLE_NAME = :p_tname AND COLUMN_NAME = :p_colname";
        using var conn = await OpenAsync();
        using var cmd = new OracleCommand(SQL, conn);
        cmd.Parameters.Add(new OracleParameter("p_owner", schema.ToUpperInvariant()));
        cmd.Parameters.Add(new OracleParameter("p_tname", tableName.ToUpperInvariant()));
        cmd.Parameters.Add(new OracleParameter("p_colname", columnName.ToUpperInvariant()));
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task<IReadOnlyList<string>> GetPrimaryKeyColumnsAsync(string schema, string tableName)
    {
        const string SQL = """
            SELECT cc.COLUMN_NAME
            FROM ALL_CONSTRAINTS c
            JOIN ALL_CONS_COLUMNS cc ON c.CONSTRAINT_NAME = cc.CONSTRAINT_NAME AND c.OWNER = cc.OWNER
            WHERE c.OWNER = :p_owner AND c.TABLE_NAME = :p_tname AND c.CONSTRAINT_TYPE = 'P'
            ORDER BY cc.POSITION
            """;
        using var conn = await OpenAsync();
        using var cmd = new OracleCommand(SQL, conn);
        cmd.Parameters.Add(new OracleParameter("p_owner", schema.ToUpperInvariant()));
        cmd.Parameters.Add(new OracleParameter("p_tname", tableName.ToUpperInvariant()));
        using var reader = await cmd.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));
        return columns;
    }

    public async Task<IReadOnlyList<ForeignKeyReference>> GetForeignKeyReferencesAsync(string schema, string tableName, string columnName)
    {
        const string SQL = """
            SELECT rc.TABLE_NAME AS REF_TABLE, rcc.COLUMN_NAME AS REF_COLUMN
            FROM ALL_CONSTRAINTS c
            JOIN ALL_CONS_COLUMNS cc ON c.CONSTRAINT_NAME = cc.CONSTRAINT_NAME AND c.OWNER = cc.OWNER
            JOIN ALL_CONSTRAINTS rc ON c.R_CONSTRAINT_NAME = rc.CONSTRAINT_NAME AND c.R_OWNER = rc.OWNER
            JOIN ALL_CONS_COLUMNS rcc ON rc.CONSTRAINT_NAME = rcc.CONSTRAINT_NAME AND rc.OWNER = rcc.OWNER
            WHERE c.OWNER = :p_owner AND c.TABLE_NAME = :p_tname AND cc.COLUMN_NAME = :p_colname AND c.CONSTRAINT_TYPE = 'R'
            """;
        using var conn = await OpenAsync();
        using var cmd = new OracleCommand(SQL, conn);
        cmd.Parameters.Add(new OracleParameter("p_owner", schema.ToUpperInvariant()));
        cmd.Parameters.Add(new OracleParameter("p_tname", tableName.ToUpperInvariant()));
        cmd.Parameters.Add(new OracleParameter("p_colname", columnName.ToUpperInvariant()));
        return await ReadForeignKeyReferences(cmd);
    }

    public async Task<bool> ValueExistsInColumnAsync(string schema, string tableName, string columnName, string value)
    {
        var sql = $"SELECT COUNT(*) FROM {schema.ToUpperInvariant()}.{tableName.ToUpperInvariant()} WHERE {columnName.ToUpperInvariant()} = :p_value";
        using var conn = await OpenAsync();
        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(new OracleParameter("p_value", value));
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task<IReadOnlyList<ForeignKeyReference>> GetAllForeignKeysAsync(string schema, string tableName)
    {
        const string SQL = """
            SELECT rc.TABLE_NAME AS REF_TABLE, rcc.COLUMN_NAME AS REF_COLUMN
            FROM ALL_CONSTRAINTS c
            JOIN ALL_CONS_COLUMNS cc ON c.CONSTRAINT_NAME = cc.CONSTRAINT_NAME AND c.OWNER = cc.OWNER
            JOIN ALL_CONSTRAINTS rc ON c.R_CONSTRAINT_NAME = rc.CONSTRAINT_NAME AND c.R_OWNER = rc.OWNER
            JOIN ALL_CONS_COLUMNS rcc ON rc.CONSTRAINT_NAME = rcc.CONSTRAINT_NAME AND rc.OWNER = rcc.OWNER
            WHERE c.OWNER = :p_owner AND c.TABLE_NAME = :p_tname AND c.CONSTRAINT_TYPE = 'R'
            """;
        using var conn = await OpenAsync();
        using var cmd = new OracleCommand(SQL, conn);
        cmd.Parameters.Add(new OracleParameter("p_owner", schema.ToUpperInvariant()));
        cmd.Parameters.Add(new OracleParameter("p_tname", tableName.ToUpperInvariant()));
        return await ReadForeignKeyReferences(cmd);
    }

    public async Task<bool> ForeignKeyValueExistsAsync(string schema, string referencedTable, string referencedColumn, string value)
    {
        var sql = $"SELECT COUNT(*) FROM {schema.ToUpperInvariant()}.{referencedTable.ToUpperInvariant()} WHERE {referencedColumn.ToUpperInvariant()} = :p_value";
        using var conn = await OpenAsync();
        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(new OracleParameter("p_value", value));
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task<IReadOnlyList<ColumnConstraintInfo>> GetNotNullColumnsWithoutDefaultAsync(string schema, string tableName)
    {
        const string SQL = """
            SELECT COLUMN_NAME FROM ALL_TAB_COLUMNS
            WHERE OWNER = :p_owner AND TABLE_NAME = :p_tname AND NULLABLE = 'N' AND DATA_DEFAULT IS NULL
            """;
        using var conn = await OpenAsync();
        using var cmd = new OracleCommand(SQL, conn);
        cmd.Parameters.Add(new OracleParameter("p_owner", schema.ToUpperInvariant()));
        cmd.Parameters.Add(new OracleParameter("p_tname", tableName.ToUpperInvariant()));
        using var reader = await cmd.ExecuteReaderAsync();
        var results = new List<ColumnConstraintInfo>();
        while (await reader.ReadAsync())
            results.Add(new ColumnConstraintInfo(reader.GetString(0), IsNotNull: true, HasDefault: false));
        return results;
    }

    public async Task<int> CountRowsMatchingSelectAsync(string selectQuery)
    {
        var sanitized = selectQuery.TrimEnd().TrimEnd(';');
        var sql = $"SELECT COUNT(*) FROM ({sanitized})";
        using var conn = await OpenAsync();
        using var cmd = new OracleCommand(sql, conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<OracleConnection> OpenAsync()
    {
        var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    private static async Task<IReadOnlyList<ForeignKeyReference>> ReadForeignKeyReferences(OracleCommand cmd)
    {
        using var reader = await cmd.ExecuteReaderAsync();
        var results = new List<ForeignKeyReference>();
        while (await reader.ReadAsync())
            results.Add(new ForeignKeyReference(reader.GetString(0), reader.GetString(1)));
        return results;
    }
}
