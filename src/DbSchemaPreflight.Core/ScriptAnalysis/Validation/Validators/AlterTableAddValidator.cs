using System.Text.RegularExpressions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using static DbSchemaPreflight.Core.ScriptAnalysis.Validation.ValidationHelpers;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Validation.Validators;

internal sealed class AlterTableAddValidator : IStatementValidator
{
    public async Task<StatementResult> ValidateAsync(ParsedStatement statement, string schema, IScriptValidationQueries queries)
    {
        var tableMatch = Regex.Match(statement.RawText, @"ALTER\s+TABLE\s+(\w+)\s+ADD", RegexOptions.IgnoreCase);
        if (!tableMatch.Success)
            return Skipped(statement);

        var tableName = tableMatch.Groups[1].Value;
        if (!await queries.TableExistsAsync(schema, tableName))
            return Error(statement, $"Table '{tableName}' does not exist");

        var colMatch = Regex.Match(statement.RawText, @"\bADD\b\s*\(?\s*(\w+)\s+\w", RegexOptions.IgnoreCase);
        if (!colMatch.Success)
            return Ok(statement);

        var columnName = colMatch.Groups[1].Value;
        if (await queries.ColumnExistsAsync(schema, tableName, columnName))
        {
            var sql = statement.RawText.Replace("'", "''");
            var suggestion = $"""
                BEGIN
                  EXECUTE IMMEDIATE '{sql}';
                EXCEPTION
                  WHEN OTHERS THEN
                    IF SQLCODE = -1430 THEN NULL;
                    ELSE RAISE;
                    END IF;
                END;
                """;
            return Error(statement, $"Column '{columnName}' already exists on table '{tableName}' (ORA-01430)", suggestion);
        }

        return Ok(statement);
    }
}
