using Oracle.ManagedDataAccess.Client;

namespace DbSchemaPreflight.Oracle;

public sealed class OracleConnectionFactory
{
    public async Task<OracleConnection> OpenConnectionAsync(string connectionString)
    {
        var connection = new OracleConnection(connectionString);
        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch (Exception ex)
        {
            connection.Dispose();
            throw new Exception($"Could not connect to Oracle: {ex.Message}", ex);
        }
    }
}
