using DbSchemaPreflight.Core.Models;
using DbSchemaPreflight.Core.SqlGeneration;

namespace DbSchemaPreflight.Oracle.SqlGeneration;

public sealed class OracleSqlSuggestionGenerator : ISqlSuggestionGenerator
{
    private readonly IOracleDataTypeFormatter _formatter;

    public OracleSqlSuggestionGenerator(IOracleDataTypeFormatter formatter)
    {
        _formatter = formatter;
    }

    public IReadOnlyList<SqlSuggestion> Generate(
        SchemaDifference difference,
        SchemaSnapshot reference,
        SchemaSnapshot target)
    {
        return difference.Type switch
        {
            DifferenceType.MissingTable         => GenerateCreateTable(difference, reference, target),
            DifferenceType.MissingColumn        => GenerateAddColumn(difference, reference, target),
            DifferenceType.DataTypeMismatch     => GenerateModifyColumn(difference, reference, target, DifferenceType.DataTypeMismatch),
            DifferenceType.DataLengthSmaller    => GenerateModifyColumn(difference, reference, target, DifferenceType.DataLengthSmaller),
            DifferenceType.DataLengthLarger     => GenerateModifyColumn(difference, reference, target, DifferenceType.DataLengthLarger),
            DifferenceType.PrecisionMismatch    => GenerateModifyColumn(difference, reference, target, DifferenceType.PrecisionMismatch),
            DifferenceType.ScaleMismatch        => GenerateModifyColumn(difference, reference, target, DifferenceType.ScaleMismatch),
            DifferenceType.NullabilityMismatch  => GenerateModifyNullable(difference, target),
            DifferenceType.DefaultValueMismatch => GenerateModifyDefault(difference, target),
            DifferenceType.ExtraColumn          => GenerateDropColumnCommented(difference, target),
            DifferenceType.ExtraTable           => GenerateDropTableCommented(difference, target),
            _                                   => []
        };
    }

    private IReadOnlyList<SqlSuggestion> GenerateCreateTable(
        SchemaDifference difference,
        SchemaSnapshot reference,
        SchemaSnapshot target)
    {
        var table = FindTable(reference, difference.TableName);
        if (table is null) return [];

        var schemaId = OracleIdentifierQuoter.Quote(target.SchemaName);
        var tableId  = OracleIdentifierQuoter.Quote(table.Name);

        var columnLines = table.Columns
            .OrderBy(c => c.ColumnId)
            .Select(col =>
            {
                var colId   = OracleIdentifierQuoter.Quote(col.Name);
                var colType = _formatter.Format(col);
                var parts   = new List<string> { $"    {colId} {colType}" };
                if (col.DataDefault is not null)
                    parts.Add($"DEFAULT {col.DataDefault.Trim()}");
                if (!col.Nullable)
                    parts.Add("NOT NULL");
                return string.Join(" ", parts);
            });

        var sql = $"CREATE TABLE {schemaId}.{tableId} (\n{string.Join(",\n", columnLines)}\n);";

        return [new SqlSuggestion
        {
            Title                = $"Create table {table.Name}",
            Sql                  = sql,
            Risk                 = SqlSuggestionRisk.Low,
            IsDestructive        = false,
            RequiresManualReview = false,
            TableName            = difference.TableName
        }];
    }

    private IReadOnlyList<SqlSuggestion> GenerateAddColumn(
        SchemaDifference difference,
        SchemaSnapshot reference,
        SchemaSnapshot target)
    {
        var col = FindColumn(reference, difference.TableName, difference.ColumnName);
        if (col is null) return [];

        var schemaId = OracleIdentifierQuoter.Quote(target.SchemaName);
        var tableId  = OracleIdentifierQuoter.Quote(difference.TableName);
        var colId    = OracleIdentifierQuoter.Quote(col.Name);
        var colType  = _formatter.Format(col);

        var colDef = $"{colId} {colType}";
        if (col.DataDefault is not null)
            colDef += $" DEFAULT {col.DataDefault.Trim()}";
        if (!col.Nullable)
            colDef += " NOT NULL";

        var sql = $"ALTER TABLE {schemaId}.{tableId}\nADD (\n    {colDef}\n);";

        return [new SqlSuggestion
        {
            Title                = $"Add column {difference.TableName}.{difference.ColumnName}",
            Sql                  = sql,
            Risk                 = SqlSuggestionRisk.Low,
            IsDestructive        = false,
            RequiresManualReview = false,
            TableName            = difference.TableName,
            ColumnName           = difference.ColumnName
        }];
    }

    private IReadOnlyList<SqlSuggestion> GenerateModifyColumn(
        SchemaDifference difference,
        SchemaSnapshot reference,
        SchemaSnapshot target,
        DifferenceType diffType)
    {
        var col = FindColumn(reference, difference.TableName, difference.ColumnName);
        if (col is null) return [];

        var schemaId = OracleIdentifierQuoter.Quote(target.SchemaName);
        var tableId  = OracleIdentifierQuoter.Quote(difference.TableName);
        var colId    = OracleIdentifierQuoter.Quote(col.Name);
        var colType  = _formatter.Format(col);

        var (risk, warnings) = ClassifyModifyRisk(diffType);
        var warningComments  = warnings.Count > 0
            ? string.Join("\n", warnings.Select(w => $"-- ATENÇÃO: {w}")) + "\n"
            : string.Empty;

        var sql = $"{warningComments}ALTER TABLE {schemaId}.{tableId}\nMODIFY (\n    {colId} {colType}\n);";

        return [new SqlSuggestion
        {
            Title                = $"Modify column {difference.TableName}.{difference.ColumnName}",
            Sql                  = sql,
            Risk                 = risk,
            IsDestructive        = false,
            RequiresManualReview = warnings.Count > 0,
            Warnings             = warnings,
            TableName            = difference.TableName,
            ColumnName           = difference.ColumnName
        }];
    }

    private static (SqlSuggestionRisk risk, IReadOnlyList<string> warnings) ClassifyModifyRisk(DifferenceType diffType)
    {
        return diffType switch
        {
            DifferenceType.DataLengthLarger  => (SqlSuggestionRisk.Low, []),
            DifferenceType.PrecisionMismatch => (SqlSuggestionRisk.Medium,
                ["Esta alteração pode falhar se existirem valores com precisão maior que a definida na referência."]),
            DifferenceType.ScaleMismatch     => (SqlSuggestionRisk.Medium,
                ["Esta alteração pode falhar se existirem valores com escala maior que a definida na referência."]),
            DifferenceType.DataTypeMismatch  => (SqlSuggestionRisk.High,
                ["Alterar o tipo de dado pode falhar ou causar perda de dados se existirem valores incompatíveis com o novo tipo."]),
            DifferenceType.DataLengthSmaller => (SqlSuggestionRisk.High,
                ["Esta alteração pode falhar se existirem valores maiores que o novo tamanho definido na referência."]),
            _                                => (SqlSuggestionRisk.Medium, [])
        };
    }

    private static IReadOnlyList<SqlSuggestion> GenerateModifyNullable(
        SchemaDifference difference,
        SchemaSnapshot target)
    {
        var schemaId = OracleIdentifierQuoter.Quote(target.SchemaName);
        var tableId  = OracleIdentifierQuoter.Quote(difference.TableName);
        var colId    = OracleIdentifierQuoter.Quote(difference.ColumnName!);

        // ReferenceValue "True"  → reference allows NULL  → make target allow NULL  → lower risk
        // ReferenceValue "False" → reference is NOT NULL  → make target NOT NULL    → higher risk
        var referenceIsNullable = string.Equals(difference.ReferenceValue, "True", StringComparison.OrdinalIgnoreCase);

        if (referenceIsNullable)
        {
            var sql = $"ALTER TABLE {schemaId}.{tableId}\nMODIFY (\n    {colId} NULL\n);";
            return [new SqlSuggestion
            {
                Title                = $"Allow NULL on {difference.TableName}.{difference.ColumnName}",
                Sql                  = sql,
                Risk                 = SqlSuggestionRisk.Medium,
                IsDestructive        = false,
                RequiresManualReview = false,
                TableName            = difference.TableName,
                ColumnName           = difference.ColumnName
            }];
        }
        else
        {
            const string warning = "Esta alteração pode falhar se a coluna contiver valores NULL no banco alvo.";
            var sql = $"-- ATENÇÃO: {warning}\nALTER TABLE {schemaId}.{tableId}\nMODIFY (\n    {colId} NOT NULL\n);";
            return [new SqlSuggestion
            {
                Title                = $"Set NOT NULL on {difference.TableName}.{difference.ColumnName}",
                Sql                  = sql,
                Risk                 = SqlSuggestionRisk.High,
                IsDestructive        = false,
                RequiresManualReview = true,
                Warnings             = [warning],
                TableName            = difference.TableName,
                ColumnName           = difference.ColumnName
            }];
        }
    }

    private static IReadOnlyList<SqlSuggestion> GenerateModifyDefault(
        SchemaDifference difference,
        SchemaSnapshot target)
    {
        var schemaId     = OracleIdentifierQuoter.Quote(target.SchemaName);
        var tableId      = OracleIdentifierQuoter.Quote(difference.TableName);
        var colId        = OracleIdentifierQuoter.Quote(difference.ColumnName!);
        var defaultValue = difference.ReferenceValue ?? "NULL";

        var sql = $"ALTER TABLE {schemaId}.{tableId}\nMODIFY (\n    {colId} DEFAULT {defaultValue}\n);";

        return [new SqlSuggestion
        {
            Title                = $"Change default on {difference.TableName}.{difference.ColumnName}",
            Sql                  = sql,
            Risk                 = SqlSuggestionRisk.Low,
            IsDestructive        = false,
            RequiresManualReview = false,
            TableName            = difference.TableName,
            ColumnName           = difference.ColumnName
        }];
    }

    private static IReadOnlyList<SqlSuggestion> GenerateDropColumnCommented(
        SchemaDifference difference,
        SchemaSnapshot target)
    {
        var schemaId = OracleIdentifierQuoter.Quote(target.SchemaName);
        var tableId  = OracleIdentifierQuoter.Quote(difference.TableName);
        var colId    = OracleIdentifierQuoter.Quote(difference.ColumnName!);

        const string warning = "Operação destrutiva. O dado da coluna será perdido permanentemente.";
        var sql = $"-- ATENÇÃO: Operação destrutiva. Revise manualmente antes de executar.\n-- ALTER TABLE {schemaId}.{tableId} DROP COLUMN {colId};";

        return [new SqlSuggestion
        {
            Title                = $"Drop extra column {difference.TableName}.{difference.ColumnName}",
            Sql                  = sql,
            Risk                 = SqlSuggestionRisk.High,
            IsDestructive        = true,
            RequiresManualReview = true,
            Warnings             = [warning],
            TableName            = difference.TableName,
            ColumnName           = difference.ColumnName
        }];
    }

    private static IReadOnlyList<SqlSuggestion> GenerateDropTableCommented(
        SchemaDifference difference,
        SchemaSnapshot target)
    {
        var schemaId = OracleIdentifierQuoter.Quote(target.SchemaName);
        var tableId  = OracleIdentifierQuoter.Quote(difference.TableName);

        const string warning = "Operação destrutiva. Todos os dados da tabela serão perdidos permanentemente.";
        var sql = $"-- ATENÇÃO: Operação destrutiva. Revise manualmente antes de executar.\n-- DROP TABLE {schemaId}.{tableId};";

        return [new SqlSuggestion
        {
            Title                = $"Drop extra table {difference.TableName}",
            Sql                  = sql,
            Risk                 = SqlSuggestionRisk.High,
            IsDestructive        = true,
            RequiresManualReview = true,
            Warnings             = [warning],
            TableName            = difference.TableName
        }];
    }

    private static TableDefinition? FindTable(SchemaSnapshot snapshot, string tableName) =>
        snapshot.Tables.FirstOrDefault(t =>
            string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));

    private static ColumnDefinition? FindColumn(SchemaSnapshot snapshot, string tableName, string? columnName)
    {
        if (columnName is null) return null;
        return FindTable(snapshot, tableName)?.Columns
            .FirstOrDefault(c => string.Equals(c.Name, columnName, StringComparison.OrdinalIgnoreCase));
    }
}
