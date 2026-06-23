using System.Text.RegularExpressions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using static DbSchemaPreflight.Core.ScriptAnalysis.Validation.ValidationHelpers;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Validation.Validators;

internal sealed class InsertValidator : IStatementValidator
{
    public async Task<StatementResult> ValidateAsync(ParsedStatement statement, string schema, IScriptValidationQueries queries)
    {
        var tableMatch = Regex.Match(statement.RawText, @"INSERT\s+INTO\s+(\w+)", RegexOptions.IgnoreCase);
        if (!tableMatch.Success)
            return Skipped(statement);

        var tableName = tableMatch.Groups[1].Value;
        if (!await queries.TableExistsAsync(schema, tableName))
            return Error(statement, $"Table '{tableName}' does not exist");

        var colsMatch = Regex.Match(statement.RawText, @"INSERT\s+INTO\s+\w+\s*\(([^)]+)\)", RegexOptions.IgnoreCase);
        if (!colsMatch.Success)
            return Ok(statement);

        var columns = ParseColumnList(colsMatch.Groups[1].Value);

        foreach (var col in columns)
        {
            if (!await queries.ColumnExistsAsync(schema, tableName, col))
                return Error(statement, $"Column '{col}' does not exist on table '{tableName}'");
        }

        var valsMatch = Regex.Match(statement.RawText, @"\bVALUES\b\s*\((.+)\)\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!valsMatch.Success)
            return Ok(statement);

        var values = TokenizeValues(valsMatch.Groups[1].Value);
        var runtimeDependentFields = new List<string>();

        for (var i = 0; i < Math.Min(columns.Count, values.Count); i++)
        {
            if (IsRuntimeDependent(values[i], statement.DeclaredVariables))
                runtimeDependentFields.Add(values[i].Trim());
        }

        var pkColumns = await queries.GetPrimaryKeyColumnsAsync(schema, tableName);

        foreach (var pkCol in pkColumns)
        {
            var pkIdx = columns.FindIndex(c => string.Equals(c, pkCol, StringComparison.OrdinalIgnoreCase));
            if (pkIdx < 0 || pkIdx >= values.Count) continue;

            var pkValue = values[pkIdx].Trim();
            if (IsRuntimeDependent(pkValue, statement.DeclaredVariables)) continue;

            var literalValue = StripStringLiteral(pkValue);
            if (await queries.ValueExistsInColumnAsync(schema, tableName, pkCol, literalValue))
            {
                var suggestion = BuildNotExistsSuggestion(tableName, columns, values, pkCol, pkValue);
                return Error(statement,
                    $"Duplicate value '{pkValue}' for primary key column '{pkCol}'",
                    suggestion,
                    runtimeDependentFields);
            }
        }

        for (var i = 0; i < Math.Min(columns.Count, values.Count); i++)
        {
            var col = columns[i];
            var val = values[i].Trim();
            if (IsRuntimeDependent(val, statement.DeclaredVariables)) continue;

            var fkRefs = await queries.GetForeignKeyReferencesAsync(schema, tableName, col);
            foreach (var fk in fkRefs)
            {
                var literalValue = StripStringLiteral(val);
                if (!await queries.ForeignKeyValueExistsAsync(schema, fk.ReferencedTable, fk.ReferencedColumn, literalValue))
                    return Error(statement,
                        $"Foreign key violation: value '{val}' for column '{col}' does not exist in '{fk.ReferencedTable}.{fk.ReferencedColumn}'",
                        runtimeFields: runtimeDependentFields);
            }
        }

        return Ok(statement, runtimeDependentFields.Count > 0 ? runtimeDependentFields : null);
    }

    private static string BuildNotExistsSuggestion(string tableName, List<string> columns, List<string> values, string pkCol, string pkValue)
    {
        var colList = string.Join(", ", columns);
        var valList = string.Join(", ", values);
        return $"INSERT INTO {tableName} ({colList})\n" +
               $"SELECT {valList} FROM DUAL\n" +
               $"WHERE NOT EXISTS (\n" +
               $"  SELECT 1 FROM {tableName} WHERE {pkCol} = {pkValue}\n" +
               $");";
    }
}
