using Spectre.Console;
using Spectre.Console.Cli;

namespace DbSchemaPreflight.Cli.Commands;

public sealed class InitSettings : CommandSettings { }

public sealed class InitCommand : Command<InitSettings>
{
    private const string CONFIG_TEMPLATE =
        """
        reference:
          connectionString: "User Id=APP_REF;Password=CHANGE_ME;Data Source=localhost:1521/XEPDB1"
          schema: "APP_REF"

        target:
          connectionString: "User Id=APP_TARGET;Password=CHANGE_ME;Data Source=localhost:1521/XEPDB1"
          schema: "APP_TARGET"

        report:
          output: "./reports/schema-diff.html"
          exportSql: false

        analyse-script-tool:
          provider: "oracle"
          connectionString: "User Id=APP_USER;Password=CHANGE_ME;Data Source=localhost:1521/XEPDB1"
          schema: "APP_SCHEMA"
          # file: "./scripts/my-script.sql"   # optional; if absent, interactive selection from current directory
          report:
            output: "./reports/script-analysis.html"
        """;

    protected override int Execute(CommandContext context, InitSettings settings, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "config.yaml");

        if (File.Exists(configPath))
        {
            AnsiConsole.MarkupLine("[yellow]config.yaml already exists in the current directory.[/]");
            AnsiConsole.MarkupLine("Delete it first or edit it manually.");
            return 1;
        }

        File.WriteAllText(configPath, CONFIG_TEMPLATE);
        AnsiConsole.MarkupLine($"[green]config.yaml created:[/] {configPath}");
        AnsiConsole.MarkupLine("Edit the file and replace CHANGE_ME values before running any command.");
        return 0;
    }
}
