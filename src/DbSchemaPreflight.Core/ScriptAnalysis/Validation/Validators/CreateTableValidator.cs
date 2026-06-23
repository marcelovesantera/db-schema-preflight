using System.Text.RegularExpressions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using static DbSchemaPreflight.Core.ScriptAnalysis.Validation.ValidationHelpers;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Validation.Validators;

internal sealed class CreateTableValidator : IStatementValidator
{
    public async Task<StatementResult> ValidateAsync(ParsedStatement statement, string schema, IScriptValidationQueries queries)
    {
        var match = Regex.Match(statement.RawText, @"CREATE\s+TABLE\s+(\w+)", RegexOptions.IgnoreCase);
        if (!match.Success)
            return Skipped(statement);

        var tableName = match.Groups[1].Value;
        if (await queries.TableExistsAsync(schema, tableName))
        {
            var sql = statement.RawText.Replace("'", "''");
            var suggestion = $"""
                BEGIN
                  EXECUTE IMMEDIATE '{sql}';
                EXCEPTION
                  WHEN OTHERS THEN
                    IF SQLCODE = -955 THEN NULL;
                    ELSE RAISE;
                    END IF;
                END;
                """;
            return Error(statement, $"Table '{tableName}' already exists (ORA-00955)", suggestion);
        }

        return Ok(statement);
    }
}
