using DbSchemaPreflight.Core.Models;

namespace DbSchemaPreflight.Oracle.SqlGeneration;

public interface IOracleDataTypeFormatter
{
    string Format(ColumnDefinition column);
}
