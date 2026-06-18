using DbSchemaPreflight.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.AddCommand<CompareCommand>("compare")
        .WithDescription("Compares two Oracle schemas and generates an HTML report.");
});
return app.Run(args);
