using System.Text.RegularExpressions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;

namespace DbSchemaPreflight.Core.ScriptAnalysis.Validation;

public sealed class ResilienceScriptGenerator
{
    public string? Generate(ParsedStatement statement, StatementResult result)
    {
        if (result.Status == ValidationStatus.Ok || result.Status == ValidationStatus.Skipped)
            return null;

        // Validators already populate ResilienceSuggestion; this provides a fallback for cases they miss.
        return statement.Type switch
        {
            StatementType.CreateTable when result.Status == ValidationStatus.Error
                => GenerateOra955Block(statement.RawText),

            StatementType.AlterTableAdd when result.Status == ValidationStatus.Error
                && result.ErrorReason?.Contains("already exists") == true
                => GenerateOra1430Block(statement.RawText),

            _ => null
        };
    }

    private static string GenerateOra955Block(string rawSql)
    {
        var sql = rawSql.Replace("'", "''");
        return $"""
            BEGIN
              EXECUTE IMMEDIATE '{sql}';
            EXCEPTION
              WHEN OTHERS THEN
                IF SQLCODE = -955 THEN NULL;
                ELSE RAISE;
                END IF;
            END;
            """;
    }

    private static string GenerateOra1430Block(string rawSql)
    {
        var sql = rawSql.Replace("'", "''");
        return $"""
            BEGIN
              EXECUTE IMMEDIATE '{sql}';
            EXCEPTION
              WHEN OTHERS THEN
                IF SQLCODE = -1430 THEN NULL;
                ELSE RAISE;
                END IF;
            END;
            """;
    }
}
