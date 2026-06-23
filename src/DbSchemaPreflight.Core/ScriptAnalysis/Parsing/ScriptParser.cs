using System.Text.RegularExpressions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Parsing;

public sealed class ScriptParser
{
    private static readonly HashSet<string> StructuralKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "BEGIN", "END", "COMMIT", "ROLLBACK", "/"
    };

    private static readonly Regex BlockCommentRegex = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex DeclareVariableRegex = new(@"^\s*(\w+)\s+(\S+(?:\s*\([^)]*\))?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

    public IReadOnlyList<ParsedStatement> Parse(string sqlContent)
    {
        var content = BlockCommentRegex.Replace(sqlContent, string.Empty);
        var tokens = content.Split(';');
        var results = new List<ParsedStatement>();
        var currentDeclaredVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sequenceNumber = 1;

        foreach (var token in tokens)
        {
            var trimmed = RemoveLineComments(token).Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var normalized = trimmed.ToUpperInvariant();

            if (normalized == "DECLARE")
                continue;

            if (StructuralKeywords.Contains(normalized))
                continue;

            if (normalized.StartsWith("DECLARE"))
            {
                ExtractDeclaredVariables(trimmed, currentDeclaredVariables);
                continue;
            }

            if (IsOnlyLineComments(trimmed))
                continue;

            var type = new StatementClassifier().Classify(trimmed);

            results.Add(new ParsedStatement(
                sequenceNumber++,
                trimmed,
                type,
                new Dictionary<string, string>(currentDeclaredVariables)));
        }

        return results;
    }

    private static void ExtractDeclaredVariables(string declareBlock, Dictionary<string, string> variables)
    {
        // Strip the DECLARE keyword and parse variable declarations
        var body = declareBlock.Trim();
        if (body.StartsWith("DECLARE", StringComparison.OrdinalIgnoreCase))
            body = body.Substring(7);

        foreach (var line in body.Split('\n'))
        {
            var lineTrimmed = line.Trim().TrimEnd(';');
            if (string.IsNullOrWhiteSpace(lineTrimmed))
                continue;

            var match = DeclareVariableRegex.Match(lineTrimmed);
            if (match.Success)
            {
                var varName = match.Groups[1].Value;
                var varType = match.Groups[2].Value;
                variables[varName] = varType;
            }
        }
    }

    private static string RemoveLineComments(string text)
    {
        var lines = text.Split('\n');
        var result = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var commentIndex = line.IndexOf("--", StringComparison.Ordinal);
            result.Add(commentIndex >= 0 ? line.Substring(0, commentIndex) : line);
        }
        return string.Join('\n', result);
    }

    private static bool IsOnlyLineComments(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var trimmedLine = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedLine) && !trimmedLine.StartsWith("--"))
                return false;
        }
        return true;
    }
}
