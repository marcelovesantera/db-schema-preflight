using DbSchemaPreflight.Core.ScriptAnalysis;

namespace DbSchemaPreflight.Oracle.ScriptAnalysis;

public static class DbScriptValidationFactory
{
    private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "oracle", "sqlserver", "mysql", "postgresql"
    };

    private static readonly HashSet<string> ImplementedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "oracle"
    };

    public static IScriptValidationQueries Create(string provider, string connectionString)
    {
        if (!SupportedProviders.Contains(provider))
            throw new ArgumentException(
                $"Provider '{provider}' is not supported. Supported providers: oracle, sqlserver, mysql, postgresql",
                nameof(provider));

        if (!ImplementedProviders.Contains(provider))
            throw new NotSupportedException($"Provider '{provider}' is not yet implemented.");

        return new OracleScriptValidationQueries(connectionString);
    }
}
