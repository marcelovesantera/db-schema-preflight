using System.Text;
using DbSchemaPreflight.Cli.Config;
using DbSchemaPreflight.Core.Diff;
using DbSchemaPreflight.Core.Models;
using DbSchemaPreflight.Oracle;
using DbSchemaPreflight.Oracle.SqlGeneration;
using DbSchemaPreflight.Reporting;
using DbSchemaPreflight.Reporting.Models;
using Spectre.Console;
using Spectre.Console.Cli;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DbSchemaPreflight.Cli.Commands;

public sealed class CompareCommand : Command<CompareSettings>
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    protected override int Execute(CommandContext context, CompareSettings settings, CancellationToken cancellationToken)
    {
        var yaml = File.ReadAllText(settings.Config!);
        var config = Deserializer.Deserialize<RootConfig>(yaml);

        if (string.IsNullOrWhiteSpace(config.Reference.ConnectionString))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] reference.connectionString is required in the configuration file.");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(config.Reference.Schema))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] reference.schema is required in the configuration file.");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(config.Target.ConnectionString))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] target.connectionString is required in the configuration file.");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(config.Target.Schema))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] target.schema is required in the configuration file.");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(config.Report.Output))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] report.output is required in the configuration file.");
            return 1;
        }

        AnsiConsole.MarkupLine("Database Schema Preflight");
        AnsiConsole.MarkupLine($"Comparing {config.Reference.Schema} → {config.Target.Schema}...");
        AnsiConsole.WriteLine();

        var extractor = new OracleMetadataExtractor();

        SchemaSnapshot reference;
        try
        {
            reference = extractor.Extract(config.Reference.ConnectionString, config.Reference.Schema);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to extract metadata from reference schema '{config.Reference.Schema}': {ex.Message}");
            return 1;
        }

        SchemaSnapshot target;
        try
        {
            target = extractor.Extract(config.Target.ConnectionString, config.Target.Schema);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to extract metadata from target schema '{config.Target.Schema}': {ex.Message}");
            return 1;
        }

        AnsiConsole.MarkupLine("Metadata extraction: OK");

        var differences = new SchemaDiffEngine().Compare(reference, target);

        var sqlGenerator = new OracleSqlSuggestionGenerator(new OracleDataTypeFormatter());
        var diffWithSuggestions = differences
            .Select(d => (Diff: d, Suggestions: sqlGenerator.Generate(d, reference, target)))
            .ToList();

        var criticalCount = differences.Count(d => d.Severity == DifferenceSeverity.Critical);
        var warningCount  = differences.Count(d => d.Severity == DifferenceSeverity.Warning);
        var infoCount     = differences.Count(d => d.Severity == DifferenceSeverity.Info);

        var summary = new ReportSummary(
            totalTablesInReference: reference.Tables.Count,
            totalTablesInTarget: target.Tables.Count,
            totalDifferences: differences.Count,
            criticalCount: criticalCount,
            warningCount: warningCount,
            infoCount: infoCount);

        var model = new ReportModel
        {
            ReferenceSchema = config.Reference.Schema,
            TargetSchema    = config.Target.Schema,
            GeneratedAt     = DateTime.Now,
            Summary         = summary,
            Differences     = differences,
            SuggestionsByDiff = diffWithSuggestions
                .Where(x => x.Suggestions.Count > 0)
                .ToDictionary(x => x.Diff, x => x.Suggestions)
        };

        var generator = new HtmlReportGenerator();
        var html = generator.Generate(model);

        try
        {
            generator.Save(html, config.Report.Output);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to save report to '{config.Report.Output}': {ex.Message}");
            return 1;
        }

        if (config.Report.ExportSql)
        {
            var sqlPath = Path.ChangeExtension(config.Report.Output, ".sql");

            var safeOrder = new[]
            {
                DifferenceType.MissingTable,
                DifferenceType.MissingColumn,
                DifferenceType.DefaultValueMismatch,
                DifferenceType.DataTypeMismatch,
                DifferenceType.DataLengthSmaller,
                DifferenceType.DataLengthLarger,
                DifferenceType.PrecisionMismatch,
                DifferenceType.ScaleMismatch,
                DifferenceType.NullabilityMismatch,
                DifferenceType.ExtraColumn,
                DifferenceType.ExtraTable
            };

            var sqlContent = new StringBuilder();
            sqlContent.AppendLine("-- ============================================================");
            sqlContent.AppendLine("-- Database Schema Preflight — Script de Sugestões");
            sqlContent.AppendLine("-- ATENÇÃO: Este arquivo NÃO deve ser executado automaticamente.");
            sqlContent.AppendLine("--          Revise cada script antes de executar manualmente.");
            sqlContent.AppendLine($"-- Gerado em : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sqlContent.AppendLine($"-- Referência: {config.Reference.Schema}");
            sqlContent.AppendLine($"-- Alvo      : {config.Target.Schema}");
            sqlContent.AppendLine("-- ============================================================");
            sqlContent.AppendLine();

            var byDiff = diffWithSuggestions.ToDictionary(x => x.Diff, x => x.Suggestions);
            foreach (var diffType in safeOrder)
            {
                foreach (var d in differences.Where(d => d.Type == diffType))
                {
                    if (!byDiff.TryGetValue(d, out var typeSuggestions)) continue;
                    foreach (var s in typeSuggestions)
                    {
                        sqlContent.AppendLine(s.Sql);
                        sqlContent.AppendLine();
                    }
                }
            }

            var sqlDir = Path.GetDirectoryName(sqlPath);
            if (!string.IsNullOrEmpty(sqlDir))
                Directory.CreateDirectory(sqlDir);
            File.WriteAllText(sqlPath, sqlContent.ToString(), Encoding.UTF8);

            AnsiConsole.MarkupLine($"SQL script saved to: {sqlPath}");
        }

        AnsiConsole.MarkupLine($"Schema comparison: {differences.Count} difference(s) found");
        AnsiConsole.MarkupLine($"  [red]Critical[/] : {criticalCount}");
        AnsiConsole.MarkupLine($"  [yellow]Warning[/]  : {warningCount}");
        AnsiConsole.MarkupLine($"  Info     : {infoCount}");
        AnsiConsole.WriteLine();

        var statusMarkup = summary.ReadinessStatus switch
        {
            "NOT READY"    => "[red]NOT READY[/]",
            "NEEDS REVIEW" => "[yellow]NEEDS REVIEW[/]",
            _              => "[green]READY[/]"
        };
        AnsiConsole.MarkupLine($"Status: {statusMarkup}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"Report saved to: {config.Report.Output}");

        return summary.CriticalCount > 0 ? 1 : 0;
    }
}
