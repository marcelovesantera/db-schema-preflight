namespace DbSchemaPreflight.Core.ScriptAnalysis;

public sealed record ColumnConstraintInfo(string ColumnName, bool IsNotNull, bool HasDefault);
