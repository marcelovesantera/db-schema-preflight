using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DbSchemaPreflight.Cli.Commands;

public sealed class CompareSettings : CommandSettings
{
    [CommandOption("--config <FILE_PATH>")]
    [Description("Path to the YAML configuration file with connection strings and output path.")]
    public string? Config { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Config))
            return ValidationResult.Error("--config is required.");

        if (!File.Exists(Config))
            return ValidationResult.Error($"Config file not found: {Config}");

        return ValidationResult.Success();
    }
}
