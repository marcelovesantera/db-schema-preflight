namespace DbSchemaPreflight.Cli.Config;

public sealed class CompareTool
{
    public ConnectionConfig Reference { get; set; } = new();
    public ConnectionConfig Target { get; set; } = new();
    public ReportConfig Report { get; set; } = new();
}

public sealed class ConnectionConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
}

public sealed class ReportConfig
{
    public string Output { get; set; } = string.Empty;
    public bool ExportSql { get; set; }
}
