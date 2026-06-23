using System.Text.RegularExpressions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using static DbSchemaPreflight.Core.ScriptAnalysis.Validation.ValidationHelpers;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Validation.Validators;

internal sealed class InsertSelectValidator : IStatementValidator
{
    public async Task<StatementResult> ValidateAsync(ParsedStatement statement, string schema, IScriptValidationQueries queries)
    {
        var targetMatch = Regex.Match(statement.RawText, @"INSERT\s+INTO\s+(\w+)", RegexOptions.IgnoreCase);
        if (!targetMatch.Success)
            return Skipped(statement);

        var targetTable = targetMatch.Groups[1].Value;
        if (!await queries.TableExistsAsync(schema, targetTable))
            return Error(statement, $"Target table '{targetTable}' does not exist");

        var selectIdx = statement.RawText.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
        if (selectIdx < 0)
            return Skipped(statement);

        var selectQuery = statement.RawText.Substring(selectIdx);

        var sourceTables = ExtractTablesFromSelect(selectQuery)
            .Where(t => !string.Equals(t, targetTable, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var src in sourceTables)
        {
            if (!await queries.TableExistsAsync(schema, src))
                return Error(statement, $"Source table '{src}' does not exist");
        }

        var count = await queries.CountRowsMatchingSelectAsync(selectQuery);
        if (count == 0)
            return Warning(statement, new[] { "SELECT returns no rows; INSERT will insert nothing" });

        return Ok(statement);
    }

    private static IEnumerable<string> ExtractTablesFromSelect(string sql)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(sql, @"\bFROM\b\s+(\w+)", RegexOptions.IgnoreCase))
            tables.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(sql, @"\bJOIN\b\s+(\w+)", RegexOptions.IgnoreCase))
            tables.Add(m.Groups[1].Value);
        return tables;
    }
}
