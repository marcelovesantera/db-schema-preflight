namespace DbSchemaPreflight.Core.ScriptAnalysis;

public interface IScriptValidationQueries
{
    Task<bool> TableExistsAsync(string schema, string tableName);

    Task<bool> ColumnExistsAsync(string schema, string tableName, string columnName);

    Task<IReadOnlyList<string>> GetPrimaryKeyColumnsAsync(string schema, string tableName);

    Task<IReadOnlyList<ForeignKeyReference>> GetForeignKeyReferencesAsync(string schema, string tableName, string columnName);

    Task<bool> ValueExistsInColumnAsync(string schema, string tableName, string columnName, string value);

    Task<IReadOnlyList<ForeignKeyReference>> GetAllForeignKeysAsync(string schema, string tableName);

    Task<bool> ForeignKeyValueExistsAsync(string schema, string referencedTable, string referencedColumn, string value);

    Task<IReadOnlyList<ColumnConstraintInfo>> GetNotNullColumnsWithoutDefaultAsync(string schema, string tableName);

    Task<int> CountRowsMatchingSelectAsync(string selectQuery);
}
