using DbSchemaPreflight.Core.Models;

namespace DbSchemaPreflight.Core.Diff;

public sealed class SchemaDiffEngine
{
    public List<SchemaDifference> Compare(SchemaSnapshot reference, SchemaSnapshot target)
    {
        var differences = new List<SchemaDifference>();

        var targetTables = target.Tables
            .ToDictionary(t => t.Name.ToUpperInvariant());

        var referenceTables = reference.Tables
            .ToDictionary(t => t.Name.ToUpperInvariant());

        foreach (var refTable in reference.Tables)
        {
            if (!targetTables.ContainsKey(refTable.Name.ToUpperInvariant()))
            {
                differences.Add(new SchemaDifference
                {
                    Severity = DifferenceSeverity.Critical,
                    Type = DifferenceType.MissingTable,
                    ObjectType = "TABLE",
                    TableName = refTable.Name,
                    Message = $"Table '{refTable.Name}' exists in reference but is missing in target."
                });
            }
        }

        foreach (var tgtTable in target.Tables)
        {
            if (!referenceTables.ContainsKey(tgtTable.Name.ToUpperInvariant()))
            {
                differences.Add(new SchemaDifference
                {
                    Severity = DifferenceSeverity.Info,
                    Type = DifferenceType.ExtraTable,
                    ObjectType = "TABLE",
                    TableName = tgtTable.Name,
                    Message = $"Table '{tgtTable.Name}' exists in target but not in reference."
                });
            }
        }

        foreach (var refTable in reference.Tables)
        {
            var key = refTable.Name.ToUpperInvariant();
            if (!targetTables.TryGetValue(key, out var tgtTable))
                continue;

            CompareColumns(refTable, tgtTable, differences);
        }

        return differences;
    }

    private static void CompareColumns(
        TableDefinition reference,
        TableDefinition target,
        List<SchemaDifference> differences)
    {
        var targetColumns = target.Columns
            .ToDictionary(c => c.Name.ToUpperInvariant());

        var referenceColumns = reference.Columns
            .ToDictionary(c => c.Name.ToUpperInvariant());

        foreach (var refCol in reference.Columns)
        {
            if (targetColumns.TryGetValue(refCol.Name.ToUpperInvariant(), out var tgtCol))
            {
                CompareColumnAttributes(refCol, tgtCol, reference.Name, differences);
            }
            else
            {
                differences.Add(new SchemaDifference
                {
                    Severity = DifferenceSeverity.Critical,
                    Type = DifferenceType.MissingColumn,
                    ObjectType = "COLUMN",
                    TableName = reference.Name,
                    ColumnName = refCol.Name,
                    Message = $"Column '{refCol.Name}' in table '{reference.Name}' is missing in target."
                });
            }
        }

        foreach (var tgtCol in target.Columns)
        {
            if (!referenceColumns.ContainsKey(tgtCol.Name.ToUpperInvariant()))
            {
                differences.Add(new SchemaDifference
                {
                    Severity = DifferenceSeverity.Warning,
                    Type = DifferenceType.ExtraColumn,
                    ObjectType = "COLUMN",
                    TableName = reference.Name,
                    ColumnName = tgtCol.Name,
                    Message = $"Column '{tgtCol.Name}' in table '{reference.Name}' exists in target but not in reference."
                });
            }
        }
    }

    private static void CompareColumnAttributes(
        ColumnDefinition reference,
        ColumnDefinition target,
        string tableName,
        List<SchemaDifference> differences)
    {
        if (!string.Equals(reference.DataType, target.DataType, StringComparison.OrdinalIgnoreCase))
        {
            differences.Add(new SchemaDifference
            {
                Severity = DifferenceSeverity.Critical,
                Type = DifferenceType.DataTypeMismatch,
                ObjectType = "COLUMN",
                TableName = tableName,
                ColumnName = reference.Name,
                Message = $"Column '{reference.Name}' in table '{tableName}' has different data types.",
                ReferenceValue = reference.DataType,
                TargetValue = target.DataType
            });
        }

        if (reference.DataLength.HasValue && target.DataLength.HasValue && reference.DataLength != target.DataLength)
        {
            var (severity, type) = target.DataLength < reference.DataLength
                ? (DifferenceSeverity.Critical, DifferenceType.DataLengthSmaller)
                : (DifferenceSeverity.Warning, DifferenceType.DataLengthLarger);

            differences.Add(new SchemaDifference
            {
                Severity = severity,
                Type = type,
                ObjectType = "COLUMN",
                TableName = tableName,
                ColumnName = reference.Name,
                Message = $"Column '{reference.Name}' in table '{tableName}' has different length.",
                ReferenceValue = reference.DataLength.ToString(),
                TargetValue = target.DataLength.ToString()
            });
        }

        if (reference.DataPrecision != target.DataPrecision)
        {
            differences.Add(new SchemaDifference
            {
                Severity = DifferenceSeverity.Warning,
                Type = DifferenceType.PrecisionMismatch,
                ObjectType = "COLUMN",
                TableName = tableName,
                ColumnName = reference.Name,
                Message = $"Column '{reference.Name}' in table '{tableName}' has different precision.",
                ReferenceValue = reference.DataPrecision?.ToString(),
                TargetValue = target.DataPrecision?.ToString()
            });
        }

        if (reference.DataScale != target.DataScale)
        {
            differences.Add(new SchemaDifference
            {
                Severity = DifferenceSeverity.Warning,
                Type = DifferenceType.ScaleMismatch,
                ObjectType = "COLUMN",
                TableName = tableName,
                ColumnName = reference.Name,
                Message = $"Column '{reference.Name}' in table '{tableName}' has different scale.",
                ReferenceValue = reference.DataScale?.ToString(),
                TargetValue = target.DataScale?.ToString()
            });
        }

        if (reference.Nullable != target.Nullable)
        {
            differences.Add(new SchemaDifference
            {
                Severity = DifferenceSeverity.Warning,
                Type = DifferenceType.NullabilityMismatch,
                ObjectType = "COLUMN",
                TableName = tableName,
                ColumnName = reference.Name,
                Message = $"Column '{reference.Name}' in table '{tableName}' has different nullability.",
                ReferenceValue = reference.Nullable.ToString(),
                TargetValue = target.Nullable.ToString()
            });
        }

        var refDefault = reference.DataDefault?.Trim();
        var tgtDefault = target.DataDefault?.Trim();
        if (!string.Equals(refDefault, tgtDefault, StringComparison.OrdinalIgnoreCase))
        {
            differences.Add(new SchemaDifference
            {
                Severity = DifferenceSeverity.Warning,
                Type = DifferenceType.DefaultValueMismatch,
                ObjectType = "COLUMN",
                TableName = tableName,
                ColumnName = reference.Name,
                Message = $"Column '{reference.Name}' in table '{tableName}' has different default values.",
                ReferenceValue = refDefault,
                TargetValue = tgtDefault
            });
        }
    }
}
