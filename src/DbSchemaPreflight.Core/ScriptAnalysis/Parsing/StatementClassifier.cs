using DbSchemaPreflight.Core.ScriptAnalysis.Models;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Parsing;

public sealed class StatementClassifier
{
    public StatementType Classify(string statementText)
    {
        var normalized = statementText.Trim().ToUpperInvariant();

        if (normalized.StartsWith("INSERT INTO"))
        {
            return normalized.Contains(" SELECT ") || normalized.Contains("\nSELECT ") || normalized.Contains("\tSELECT ")
                ? StatementType.InsertSelect
                : StatementType.Insert;
        }

        if (normalized.StartsWith("UPDATE "))
            return StatementType.Update;

        if (normalized.StartsWith("DELETE FROM"))
            return StatementType.Delete;

        if (normalized.StartsWith("CREATE TABLE"))
            return StatementType.CreateTable;

        if (normalized.StartsWith("ALTER TABLE") && normalized.Contains(" ADD"))
            return StatementType.AlterTableAdd;

        if (normalized.StartsWith("CREATE OR REPLACE") && normalized.Contains(" VIEW"))
            return StatementType.CreateOrReplaceView;

        if (normalized.StartsWith("CREATE VIEW"))
            return StatementType.CreateOrReplaceView;

        if (normalized.StartsWith("DECLARE") || normalized.StartsWith("BEGIN"))
            return StatementType.PlSqlBlock;

        return StatementType.Other;
    }
}
