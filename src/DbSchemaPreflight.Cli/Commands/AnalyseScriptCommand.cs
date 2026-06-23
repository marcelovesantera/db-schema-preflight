using DbSchemaPreflight.Cli.Config;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using DbSchemaPreflight.Core.ScriptAnalysis.Validation;
using DbSchemaPreflight.Oracle.ScriptAnalysis;
using DbSchemaPreflight.Reporting.ScriptAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DbSchemaPreflight.Cli.Commands;

public sealed class AnalyseScriptCommand : AsyncCommand<AnalyseScriptSettings>
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

    protected override async Task<int> ExecuteAsync(CommandContext context, AnalyseScriptSettings settings, CancellationToken cancellationToken)
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

        var reportPath = ResolveReportPath(config, sqlFilePath);

        AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] Analysing: {Markup.Escape(sqlFilePath)}");
        AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] Connecting to schema: {Markup.Escape(config.Schema)} ({Markup.Escape(config.Provider)})...");

        var queries = DbScriptValidationFactory.Create(config.Provider, config.ConnectionString);
        try
        {
            // Probe the connection before starting analysis
            await queries.TableExistsAsync(config.Schema, "__probe__");
        }
        catch (Exception)
        {
            AnsiConsole.MarkupLine("[red]Connection failed.[/]");
            AnsiConsole.MarkupLine($"Check the connectionString in config.yaml under analyse-script-tool.");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("No analysis was performed.");
            AnsiConsole.MarkupLine("No script was executed against your database.");
            return 1;
        }

        var sqlContent = await File.ReadAllTextAsync(sqlFilePath);
        var parsed = new ScriptParser().Parse(sqlContent);

        AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] Statements found: {parsed.Count}");

        var validator = new StatementValidator();
        var results = new List<StatementResult>();

        foreach (var statement in parsed)
        {
            var result = await validator.ValidateAsync(statement, config.Schema, queries);
            results.Add(result);
        }

        var analysisResult = new ScriptAnalysisResult(sqlFilePath, DateTime.Now, results);

        try
        {
            await new ScriptAnalysisHtmlReportGenerator().GenerateAsync(analysisResult, config.Schema, reportPath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to save report to '{Markup.Escape(reportPath)}': {Markup.Escape(ex.Message)}");
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] Statements found: {results.Count}");
        AnsiConsole.MarkupLine($"           [green]OK[/]: {analysisResult.OkCount} | [red]ERROR[/]: {analysisResult.ErrorCount} | [yellow]WARNING[/]: {analysisResult.WarningCount} | SKIPPED: {analysisResult.SkippedCount}");
        AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] Report: {Markup.Escape(reportPath)}");

        return analysisResult.ErrorCount > 0 ? 1 : 0;
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
