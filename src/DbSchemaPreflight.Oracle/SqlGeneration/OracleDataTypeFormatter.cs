using DbSchemaPreflight.Core.Models;

namespace DbSchemaPreflight.Oracle.SqlGeneration;

public sealed class OracleDataTypeFormatter : IOracleDataTypeFormatter
{
    public string Format(ColumnDefinition column)
    {
        return column.DataType.ToUpperInvariant() switch
        {
            "VARCHAR2"  => column.DataLength.HasValue ? $"VARCHAR2({column.DataLength} CHAR)" : "VARCHAR2",
            "CHAR"      => column.DataLength.HasValue ? $"CHAR({column.DataLength} CHAR)" : "CHAR",
            "NUMBER"    => FormatNumber(column),
            "DATE"      => "DATE",
            "TIMESTAMP" => column.DataScale.HasValue ? $"TIMESTAMP({column.DataScale})" : "TIMESTAMP",
            "CLOB"      => "CLOB",
            "BLOB"      => "BLOB",
            _           => column.DataType
        };
    }

    private static string FormatNumber(ColumnDefinition column)
    {
        if (column.DataPrecision.HasValue && column.DataScale.HasValue)
            return $"NUMBER({column.DataPrecision},{column.DataScale})";
        if (column.DataPrecision.HasValue)
            return $"NUMBER({column.DataPrecision})";
        return "NUMBER";
    }
}
