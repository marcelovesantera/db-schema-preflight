using DbSchemaPreflight.Core.Models;

namespace DbSchemaPreflight.Core.SqlGeneration;

public interface ISqlSuggestionGenerator
{
    IReadOnlyList<SqlSuggestion> Generate(
        SchemaDifference difference,
        SchemaSnapshot reference,
        SchemaSnapshot target);
}
