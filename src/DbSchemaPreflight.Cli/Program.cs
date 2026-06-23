using DbSchemaPreflight.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.AddCommand<CompareCommand>("compare")
        .WithDescription("Compares two Oracle schemas and generates an HTML report.");

    config.AddCommand<AnalyseScriptCommand>("analyse-script")
        .WithDescription("Analyses a SQL script and validates each statement against the target Oracle schema.");

    config.AddCommand<InitCommand>("init")
        .WithDescription("Creates a config.yaml template in the current directory.");
});
return app.Run(args);
