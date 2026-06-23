using YamlDotNet.Serialization;

namespace DbSchemaPreflight.Cli.Config;

public sealed class RootConfig
{
    [YamlMember(Alias = "analyse-script-tool")]
    public AnalyseScriptToolConfig? AnalyseScriptTool { get; set; }
}
