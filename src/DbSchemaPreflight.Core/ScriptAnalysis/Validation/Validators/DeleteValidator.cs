using System.Text.RegularExpressions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using static DbSchemaPreflight.Core.ScriptAnalysis.Validation.ValidationHelpers;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Validation.Validators;

internal sealed class DeleteValidator : IStatementValidator
{
    public async Task<StatementResult> ValidateAsync(ParsedStatement statement, string schema, IScriptValidationQueries queries)
    {
        var tableMatch = Regex.Match(statement.RawText, @"DELETE\s+FROM\s+(\w+)", RegexOptions.IgnoreCase);
        if (!tableMatch.Success)
            return Skipped(statement);

        var tableName = tableMatch.Groups[1].Value;
        if (!await queries.TableExistsAsync(schema, tableName))
            return Error(statement, $"Table '{tableName}' does not exist");

        var whereMatch = Regex.Match(statement.RawText, @"\bWHERE\b\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (whereMatch.Success)
        {
            var whereClause = whereMatch.Groups[1].Value.Trim();
            if (!HasRuntimeDependentValues(whereClause, statement.DeclaredVariables))
            {
                var count = await queries.CountRowsMatchingSelectAsync(
                    $"SELECT 1 FROM {schema.ToUpperInvariant()}.{tableName.ToUpperInvariant()} WHERE {whereClause}");
                if (count == 0)
                {
                    var suggestion = $"-- Verify intent: DELETE FROM {tableName} WHERE {whereClause}\n-- WHERE clause matched no rows at analysis time.";
                    return Warning(statement,
                        new[] { $"DELETE WHERE condition matches no rows in '{tableName}'" },
                        suggestion);
                }
            }
        }

        return Ok(statement);
    }
}
