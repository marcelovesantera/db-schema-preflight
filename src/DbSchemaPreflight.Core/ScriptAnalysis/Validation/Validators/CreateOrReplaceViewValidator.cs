using System.Text.RegularExpressions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using static DbSchemaPreflight.Core.ScriptAnalysis.Validation.ValidationHelpers;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Validation.Validators;

internal sealed class CreateOrReplaceViewValidator : IStatementValidator
{
    public async Task<StatementResult> ValidateAsync(ParsedStatement statement, string schema, IScriptValidationQueries queries)
    {
        var viewMatch = Regex.Match(statement.RawText, @"\bVIEW\s+(\w+)\b", RegexOptions.IgnoreCase);
        var viewName = viewMatch.Success ? viewMatch.Groups[1].Value : string.Empty;

        var selectIdx = statement.RawText.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
        if (selectIdx < 0)
            return Ok(statement);

        var selectPart = statement.RawText.Substring(selectIdx);
        var referencedTables = ExtractTablesFromSelect(selectPart)
            .Where(t => !string.Equals(t, viewName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var table in referencedTables)
        {
            if (!await queries.TableExistsAsync(schema, table))
                return Error(statement, $"Referenced table '{table}' does not exist");
        }

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
