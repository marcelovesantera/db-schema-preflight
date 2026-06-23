using DbSchemaPreflight.Cli.Config;
using Spectre.Console;
using Spectre.Console.Cli;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DbSchemaPreflight.Cli.Commands;

public sealed class AnalyseScriptCommand : Command<AnalyseScriptSettings>
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "oracle", "sqlserver", "mysql", "postgresql"
    };

    private static readonly HashSet<string> ImplementedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "oracle"
    };

    protected override int Execute(CommandContext context, AnalyseScriptSettings settings, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "config.yaml");

        if (!File.Exists(configPath))
        {
            AnsiConsole.MarkupLine("[red]Config file not found in the current directory.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Run: [bold]dbpreflight init[/]");
            return 1;
        }

        var yaml = File.ReadAllText(configPath);
        var rootConfig = Deserializer.Deserialize<RootConfig>(yaml);

        if (rootConfig?.AnalyseScriptTool is null)
        {
            AnsiConsole.MarkupLine("[red]Section 'analyse-script-tool' not found in config.yaml.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Run [bold]dbpreflight init[/] to see an example configuration with all available sections.");
            return 1;
        }

        var config = rootConfig.AnalyseScriptTool;

        if (!SupportedProviders.Contains(config.Provider))
        {
            AnsiConsole.MarkupLine($"[red]Provider '{Markup.Escape(config.Provider)}' is not supported.[/]");
            AnsiConsole.MarkupLine("Supported providers: oracle, sqlserver, mysql, postgresql");
            return 1;
        }

        if (!ImplementedProviders.Contains(config.Provider))
        {
            AnsiConsole.MarkupLine($"[red]Provider '{Markup.Escape(config.Provider)}' is not yet implemented.[/]");
            AnsiConsole.MarkupLine("Currently supported: oracle");
            return 1;
        }

        string sqlFilePath;
        try
        {
            sqlFilePath = DiscoverSqlFile(config);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.WriteLine(ex.Message);
            return 1;
        }

        AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] Analysing: {Markup.Escape(sqlFilePath)}");
        AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] Connecting to schema: {Markup.Escape(config.Schema)} ({Markup.Escape(config.Provider)})...");

        return 0;
    }

    private static string DiscoverSqlFile(AnalyseScriptToolConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.File))
        {
            if (!File.Exists(config.File))
                throw new InvalidOperationException($"File not found: {config.File}");
            return config.File;
        }

        var sqlFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.sql");

        if (sqlFiles.Length == 0)
        {
            throw new InvalidOperationException(
                """
                No .sql files found in the current directory.

                Add a SQL file to the current directory or configure the file path in config.yaml:

                  analyse-script-tool:
                    file: "./scripts/my-script.sql"
                """);
        }

        var fileNames = sqlFiles.Select(f => Path.GetFileName(f)).ToArray();
        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a SQL file to analyse:")
                .AddChoices(fileNames));

        return Path.Combine(Directory.GetCurrentDirectory(), selected);
    }

    private static string ResolveReportPath(AnalyseScriptToolConfig config, string sqlFilePath)
    {
        if (!string.IsNullOrWhiteSpace(config.Report?.Output))
            return config.Report.Output;

        var baseName = Path.GetFileNameWithoutExtension(sqlFilePath);
        return Path.Combine(".", "reports", $"{baseName}-analysis.html");
    }
}
