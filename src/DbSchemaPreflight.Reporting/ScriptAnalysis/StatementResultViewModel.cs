using DbSchemaPreflight.Core.ScriptAnalysis.Models;

namespace DbSchemaPreflight.Reporting.ScriptAnalysis;

public sealed class StatementResultViewModel
{
    public int SequenceNumber { get; init; }
    public string StatementText { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusCssClass { get; init; } = string.Empty;
    public string? ErrorReason { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string? ResilienceSuggestion { get; init; }
    public IReadOnlyList<string> RuntimeDependentFields { get; init; } = [];

    public static StatementResultViewModel From(StatementResult result)
    {
        var (label, css) = result.Status switch
        {
            ValidationStatus.Ok      => ("OK",      "ok"),
            ValidationStatus.Error   => ("ERROR",   "error"),
            ValidationStatus.Warning => ("WARNING", "warning"),
            ValidationStatus.Skipped => ("SKIPPED", "skipped"),
            _                        => ("SKIPPED", "skipped")
        };

        return new StatementResultViewModel
        {
            SequenceNumber        = result.SequenceNumber,
            StatementText         = result.StatementText,
            StatusLabel           = label,
            StatusCssClass        = css,
            ErrorReason           = result.ErrorReason,
            Warnings              = result.Warnings,
            ResilienceSuggestion  = result.ResilienceSuggestion,
            RuntimeDependentFields = result.RuntimeDependentFields
        };
    }
}
