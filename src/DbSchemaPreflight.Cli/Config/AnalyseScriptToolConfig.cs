namespace DbSchemaPreflight.Cli.Config;

public sealed class AnalyseScriptToolConfig
{
    public string Provider { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string? File { get; set; }
    public AnalyseScriptReportConfig Report { get; set; } = new();
}
