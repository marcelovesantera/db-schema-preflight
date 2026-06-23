using DbSchemaPreflight.Core.ScriptAnalysis.Models;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Parsing;

public sealed class ParsedStatement
{
    public int SequenceNumber { get; init; }
    public string RawText { get; init; }
    public StatementType Type { get; init; }
    public IReadOnlyDictionary<string, string> DeclaredVariables { get; init; }

    public ParsedStatement(
        int sequenceNumber,
        string rawText,
        StatementType type,
        IReadOnlyDictionary<string, string>? declaredVariables = null)
    {
        SequenceNumber = sequenceNumber;
        RawText = rawText;
        Type = type;
        DeclaredVariables = declaredVariables ?? new Dictionary<string, string>();
    }
}
