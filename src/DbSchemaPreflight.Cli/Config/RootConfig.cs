using YamlDotNet.Serialization;

namespace DbSchemaPreflight.Cli.Config;

public sealed class RootConfig
{
    [YamlMember(Alias = "compare-tool")]
    public CompareTool? CompareTool { get; set; }

    [YamlMember(Alias = "analyse-script-tool")]
    public AnalyseScriptToolConfig? AnalyseScriptTool { get; set; }
}
