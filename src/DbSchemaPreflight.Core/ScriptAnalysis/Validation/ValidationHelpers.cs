using System.Text;
using System.Text.RegularExpressions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Validation;

internal static class ValidationHelpers
{
    internal static StatementResult Skipped(ParsedStatement s) =>
        new(s.SequenceNumber, s.RawText, s.Type, ValidationStatus.Skipped);

    internal static StatementResult Ok(ParsedStatement s, IReadOnlyList<string>? runtimeFields = null) =>
        new(s.SequenceNumber, s.RawText, s.Type, ValidationStatus.Ok, runtimeDependentFields: runtimeFields);

    internal static StatementResult Error(ParsedStatement s, string reason, string? suggestion = null, IReadOnlyList<string>? runtimeFields = null) =>
        new(s.SequenceNumber, s.RawText, s.Type, ValidationStatus.Error,
            errorReason: reason, resilienceSuggestion: suggestion, runtimeDependentFields: runtimeFields);

    internal static StatementResult Warning(ParsedStatement s, IReadOnlyList<string> warnings, string? suggestion = null, IReadOnlyList<string>? runtimeFields = null) =>
        new(s.SequenceNumber, s.RawText, s.Type, ValidationStatus.Warning,
            warnings: warnings, resilienceSuggestion: suggestion, runtimeDependentFields: runtimeFields);

    internal static bool IsRuntimeDependent(string value, IReadOnlyDictionary<string, string> declaredVars)
    {
        var v = value.Trim();
        if (declaredVars.ContainsKey(v)) return true;
        if (v.StartsWith("v_", StringComparison.OrdinalIgnoreCase)) return true;
        if (v.EndsWith(".nextval", StringComparison.OrdinalIgnoreCase)) return true;
        if (v.EndsWith(".currval", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static string StripStringLiteral(string value)
    {
        var v = value.Trim();
        if (v.StartsWith("'") && v.EndsWith("'") && v.Length >= 2)
            return v.Substring(1, v.Length - 2);
        return v;
    }

    internal static List<string> TokenizeValues(string valuesStr)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuote = false;
        var depth = 0;

        foreach (var c in valuesStr)
        {
            if (c == '\'' && depth == 0)
            {
                inQuote = !inQuote;
                current.Append(c);
            }
            else if (!inQuote && c == '(')
            {
                depth++;
                current.Append(c);
            }
            else if (!inQuote && c == ')')
            {
                depth--;
                current.Append(c);
            }
            else if (!inQuote && depth == 0 && c == ',')
            {
                tokens.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        if (current.Length > 0)
            tokens.Add(current.ToString().Trim());

        return tokens;
    }

    internal static List<string> ParseColumnList(string columnSection) =>
        columnSection.Split(',')
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();

    internal static List<string> ExtractSetColumns(string setClause)
    {
        var columns = new List<string>();
        foreach (Match m in Regex.Matches(setClause, @"(\w+)\s*=", RegexOptions.IgnoreCase))
            columns.Add(m.Groups[1].Value);
        return columns;
    }

    internal static bool HasRuntimeDependentValues(string clause, IReadOnlyDictionary<string, string> declaredVars)
    {
        foreach (Match m in Regex.Matches(clause, @"\b(\w+(?:\.\w+)?)\b"))
        {
            if (IsRuntimeDependent(m.Groups[1].Value, declaredVars))
                return true;
        }
        return false;
    }
}
