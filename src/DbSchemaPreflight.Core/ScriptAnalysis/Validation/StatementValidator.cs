using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using DbSchemaPreflight.Core.ScriptAnalysis.Validation.Validators;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Validation;

public sealed class StatementValidator : IStatementValidator
{
    private readonly ResilienceScriptGenerator _generator = new();

    public async Task<StatementResult> ValidateAsync(ParsedStatement statement, string schema, IScriptValidationQueries queries)
    {
        if (statement.Type == StatementType.Other || statement.Type == StatementType.PlSqlBlock)
            return new StatementResult(statement.SequenceNumber, statement.RawText, statement.Type, ValidationStatus.Skipped);

        IStatementValidator validator = statement.Type switch
        {
            StatementType.Insert => new InsertValidator(),
            StatementType.InsertSelect => new InsertSelectValidator(),
            StatementType.Update => new UpdateValidator(),
            StatementType.Delete => new DeleteValidator(),
            StatementType.CreateTable => new CreateTableValidator(),
            StatementType.AlterTableAdd => new AlterTableAddValidator(),
            StatementType.CreateOrReplaceView => new CreateOrReplaceViewValidator(),
            _ => throw new InvalidOperationException($"No validator registered for {statement.Type}")
        };

        var result = await validator.ValidateAsync(statement, schema, queries);

        if (result.ResilienceSuggestion is null
            && result.Status != ValidationStatus.Ok
            && result.Status != ValidationStatus.Skipped)
        {
            var suggestion = _generator.Generate(statement, result);
            if (suggestion is not null)
            {
                result = new StatementResult(
                    result.SequenceNumber, result.StatementText, result.Type, result.Status,
                    result.ErrorReason, result.Warnings, suggestion, result.RuntimeDependentFields);
            }
        }

        return result;
    }
}
