using YamlDotNet.Serialization;

namespace DbSchemaPreflight.Cli.Config;

public sealed class RootConfig
{
    [YamlMember(Alias = "compare-tool", ApplyNamingConventions = false)]
    public CompareTool? CompareTool { get; set; }

    [YamlMember(Alias = "analyse-script-tool", ApplyNamingConventions = false)]
    public AnalyseScriptToolConfig? AnalyseScriptTool { get; set; }
}
