using Oracle.ManagedDataAccess.Client;

namespace DbSchemaPreflight.Oracle;

public sealed class OracleConnectionFactory
{
    public OracleConnection OpenConnection(string connectionString)
    {
        var connection = new OracleConnection(connectionString);
        try
        {
            connection.Open();
            return connection;
        }
        catch (Exception ex)
        {
            connection.Dispose();
            throw new Exception($"Could not connect to Oracle: {ex.Message}", ex);
        }
    }
}
