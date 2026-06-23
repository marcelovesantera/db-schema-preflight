using System.Text.RegularExpressions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Parsing;

public sealed class ScriptParser
{
    private static readonly HashSet<string> PureStructuralKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "END", "COMMIT", "ROLLBACK", "/"
    };

    private static readonly Regex BlockCommentRegex = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex DeclareVariableRegex = new(@"^\s*(\w+)\s+(\S+(?:\s*\([^)]*\))?)\s*$", RegexOptions.Compiled);

    public IReadOnlyList<ParsedStatement> Parse(string sqlContent)
    {
        var content = BlockCommentRegex.Replace(sqlContent, string.Empty);
        var tokens = content.Split(';');
        var results = new List<ParsedStatement>();
        var currentDeclaredVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inDeclare = false;
        var sequenceNumber = 1;
        var classifier = new StatementClassifier();

        foreach (var token in tokens)
        {
            var trimmed = RemoveLineComments(token).Trim();

            if (string.IsNullOrWhiteSpace(trimmed) || IsOnlyLineComments(trimmed))
                continue;

            var upper = trimmed.ToUpperInvariant();

            // Pure structural keyword — discard
            if (PureStructuralKeywords.Contains(upper))
                continue;

            // BEGIN alone — end of declare scope
            if (upper == "BEGIN")
            {
                inDeclare = false;
                continue;
            }

            // DECLARE alone or DECLARE followed by content
            if (upper == "DECLARE" || StartsWithKeyword(upper, "DECLARE"))
            {
                inDeclare = true;
                currentDeclaredVariables.Clear();
                ExtractDeclaredVariables(trimmed, currentDeclaredVariables);
                continue;
            }

            // BEGIN followed by a statement on the same token (e.g. "BEGIN\n  INSERT ...")
            if (StartsWithKeyword(upper, "BEGIN"))
            {
                inDeclare = false;
                var body = StripTrailingEnd(trimmed.Substring(5).Trim());
                if (!string.IsNullOrWhiteSpace(body))
                    results.Add(new ParsedStatement(sequenceNumber++, body, classifier.Classify(body), Copy(currentDeclaredVariables)));
                continue;
            }

            // Inside a DECLARE block — accumulate variable declarations
            if (inDeclare)
            {
                if (TryParseVariableDeclaration(trimmed, out var varName, out var varType))
                {
                    currentDeclaredVariables[varName] = varType;
                    continue;
                }
                inDeclare = false;
            }

            var statementText = StripTrailingEnd(trimmed);
            if (string.IsNullOrWhiteSpace(statementText))
                continue;

            results.Add(new ParsedStatement(sequenceNumber++, statementText, classifier.Classify(statementText), Copy(currentDeclaredVariables)));
        }

        return results;
    }

    private static bool StartsWithKeyword(string upper, string keyword) =>
        upper.Length > keyword.Length && upper.StartsWith(keyword) &&
        (upper[keyword.Length] == '\n' || upper[keyword.Length] == '\r' || upper[keyword.Length] == ' ' || upper[keyword.Length] == '\t');

    private static string StripTrailingEnd(string text)
    {
        var trimmed = text.TrimEnd();
        if (trimmed.EndsWith("END", StringComparison.OrdinalIgnoreCase))
            return trimmed.Substring(0, trimmed.Length - 3).TrimEnd();
        return text;
    }

    private static void ExtractDeclaredVariables(string declareBlock, Dictionary<string, string> variables)
    {
        var body = declareBlock.Trim();
        if (body.StartsWith("DECLARE", StringComparison.OrdinalIgnoreCase))
            body = body.Substring(7);

        foreach (var line in body.Split('\n'))
        {
            if (TryParseVariableDeclaration(line.Trim().TrimEnd(';'), out var name, out var type))
                variables[name] = type;
        }
    }

    private static bool TryParseVariableDeclaration(string text, out string varName, out string varType)
    {
        var match = DeclareVariableRegex.Match(text.Trim());
        if (match.Success)
        {
            varName = match.Groups[1].Value;
            varType = match.Groups[2].Value;
            return true;
        }
        varName = string.Empty;
        varType = string.Empty;
        return false;
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

    private static Dictionary<string, string> Copy(Dictionary<string, string> source) =>
        new(source, StringComparer.OrdinalIgnoreCase);
}
