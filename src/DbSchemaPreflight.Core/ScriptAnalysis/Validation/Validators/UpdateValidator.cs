using System.Text.RegularExpressions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using static DbSchemaPreflight.Core.ScriptAnalysis.Validation.ValidationHelpers;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Validation.Validators;

internal sealed class UpdateValidator : IStatementValidator
{
    public async Task<StatementResult> ValidateAsync(ParsedStatement statement, string schema, IScriptValidationQueries queries)
    {
        var tableMatch = Regex.Match(statement.RawText, @"UPDATE\s+(\w+)\s+SET", RegexOptions.IgnoreCase);
        if (!tableMatch.Success)
            return Skipped(statement);

        var tableName = tableMatch.Groups[1].Value;
        if (!await queries.TableExistsAsync(schema, tableName))
            return Error(statement, $"Table '{tableName}' does not exist");

        var setMatch = Regex.Match(statement.RawText, @"\bSET\b\s+(.+?)(?=\bWHERE\b|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var setColumns = setMatch.Success ? ExtractSetColumns(setMatch.Groups[1].Value) : new List<string>();

        foreach (var col in setColumns)
        {
            if (!await queries.ColumnExistsAsync(schema, tableName, col))
                return Error(statement, $"Column '{col}' does not exist on table '{tableName}'");
        }

        var warnings = new List<string>();

        var pkColumns = await queries.GetPrimaryKeyColumnsAsync(schema, tableName);
        var pkInSet = setColumns
            .Where(c => pkColumns.Any(pk => string.Equals(pk, c, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (pkInSet.Count > 0)
            warnings.Add($"Updating primary key column(s): {string.Join(", ", pkInSet)}");

        var whereMatch = Regex.Match(statement.RawText, @"\bWHERE\b\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!whereMatch.Success)
        {
            warnings.Add("UPDATE without WHERE clause affects all rows");
        }
        else
        {
            var whereClause = whereMatch.Groups[1].Value.Trim();
            if (!HasRuntimeDependentValues(whereClause, statement.DeclaredVariables))
            {
                var count = await queries.CountRowsMatchingSelectAsync(
                    $"SELECT 1 FROM {schema.ToUpperInvariant()}.{tableName.ToUpperInvariant()} WHERE {whereClause}");
                if (count == 0)
                    warnings.Add($"WHERE clause matches no rows in '{tableName}'");
            }
        }

        if (warnings.Count > 0)
            return Warning(statement, warnings);

        return Ok(statement);
    }
}
