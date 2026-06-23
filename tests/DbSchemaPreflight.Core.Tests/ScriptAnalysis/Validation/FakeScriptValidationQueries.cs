using DbSchemaPreflight.Core.ScriptAnalysis;

namespace DbSchemaPreflight.Core.Tests.ScriptAnalysis.Validation;

internal sealed class FakeScriptValidationQueries : IScriptValidationQueries
{
    public HashSet<string> ExistingTables { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<(string Table, string Column)> ExistingColumns { get; } = new();

    public Dictionary<string, List<string>> PrimaryKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<(string Table, string Column), List<ForeignKeyReference>> ForeignKeyRefs { get; } = new();

    public Dictionary<(string Table, string Column), HashSet<string>> ColumnValues { get; } = new();

    public Dictionary<string, List<ColumnConstraintInfo>> NotNullColumnsWithoutDefault { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int SelectRowCount { get; set; } = 1;

    public Task<bool> TableExistsAsync(string schema, string tableName) =>
        Task.FromResult(ExistingTables.Contains(tableName));

    public Task<bool> ColumnExistsAsync(string schema, string tableName, string columnName) =>
        Task.FromResult(ExistingColumns.Contains((tableName.ToUpperInvariant(), columnName.ToUpperInvariant())));

    public Task<IReadOnlyList<string>> GetPrimaryKeyColumnsAsync(string schema, string tableName) =>
        Task.FromResult<IReadOnlyList<string>>(
            PrimaryKeys.TryGetValue(tableName, out var pks) ? pks : new List<string>());

    public Task<IReadOnlyList<ForeignKeyReference>> GetForeignKeyReferencesAsync(string schema, string tableName, string columnName)
    {
        var key = (tableName.ToUpperInvariant(), columnName.ToUpperInvariant());
        return Task.FromResult<IReadOnlyList<ForeignKeyReference>>(
            ForeignKeyRefs.TryGetValue(key, out var fks) ? fks : new List<ForeignKeyReference>());
    }

    public Task<bool> ValueExistsInColumnAsync(string schema, string tableName, string columnName, string value)
    {
        var key = (tableName.ToUpperInvariant(), columnName.ToUpperInvariant());
        if (!ColumnValues.TryGetValue(key, out var values))
            return Task.FromResult(false);
        return Task.FromResult(values.Contains(value, StringComparer.OrdinalIgnoreCase));
    }

    public Task<IReadOnlyList<ForeignKeyReference>> GetAllForeignKeysAsync(string schema, string tableName) =>
        Task.FromResult<IReadOnlyList<ForeignKeyReference>>(new List<ForeignKeyReference>());

    public Task<bool> ForeignKeyValueExistsAsync(string schema, string referencedTable, string referencedColumn, string value)
    {
        var key = (referencedTable.ToUpperInvariant(), referencedColumn.ToUpperInvariant());
        if (!ColumnValues.TryGetValue(key, out var values))
            return Task.FromResult(false);
        return Task.FromResult(values.Contains(value, StringComparer.OrdinalIgnoreCase));
    }

    public Task<IReadOnlyList<ColumnConstraintInfo>> GetNotNullColumnsWithoutDefaultAsync(string schema, string tableName) =>
        Task.FromResult<IReadOnlyList<ColumnConstraintInfo>>(
            NotNullColumnsWithoutDefault.TryGetValue(tableName, out var cols)
                ? cols
                : new List<ColumnConstraintInfo>());

    public Task<int> CountRowsMatchingSelectAsync(string selectQuery) =>
        Task.FromResult(SelectRowCount);

    // Helper: register table + columns together
    public void AddTableWithColumns(string table, params string[] columns)
    {
        ExistingTables.Add(table);
        foreach (var col in columns)
            ExistingColumns.Add((table.ToUpperInvariant(), col.ToUpperInvariant()));
    }

    // Helper: set a value as existing in a column (for PK/FK checks)
    public void AddColumnValue(string table, string column, string value)
    {
        var key = (table.ToUpperInvariant(), column.ToUpperInvariant());
        if (!ColumnValues.TryGetValue(key, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ColumnValues[key] = set;
        }
        set.Add(value);
    }
}
