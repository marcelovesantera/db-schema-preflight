using YamlDotNet.Serialization;

namespace DbSchemaPreflight.Cli.Config;

public sealed class RootConfig
{
    public ConnectionConfig Reference { get; set; } = new();
    public ConnectionConfig Target { get; set; } = new();
    public ReportConfig Report { get; set; } = new();

    [YamlMember(Alias = "analyse-script-tool")]
    public AnalyseScriptToolConfig? AnalyseScriptTool { get; set; }
}
