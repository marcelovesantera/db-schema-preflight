namespace DbSchemaPreflight.Oracle.SqlGeneration;

public static class OracleIdentifierQuoter
{
    public static string Quote(string name) => $"\"{name}\"";
}
