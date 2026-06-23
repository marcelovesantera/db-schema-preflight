using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Validation;

public interface IStatementValidator
{
    Task<StatementResult> ValidateAsync(ParsedStatement statement, string schema, IScriptValidationQueries queries);
}
