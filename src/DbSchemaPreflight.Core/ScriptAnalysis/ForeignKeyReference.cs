namespace DbSchemaPreflight.Core.ScriptAnalysis;

public sealed record ForeignKeyReference(string ReferencedTable, string ReferencedColumn);
