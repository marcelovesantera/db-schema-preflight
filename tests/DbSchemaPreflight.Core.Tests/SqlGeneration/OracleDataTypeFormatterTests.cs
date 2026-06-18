using FluentAssertions;
using DbSchemaPreflight.Core.Models;
using DbSchemaPreflight.Oracle.SqlGeneration;

namespace DbSchemaPreflight.Core.Tests.SqlGeneration;

public sealed class OracleDataTypeFormatterTests
{
    private readonly OracleDataTypeFormatter _formatter = new();

    private static ColumnDefinition Col(string dataType, int? length = null, int? precision = null, int? scale = null) =>
        new() { TableName = "T", Name = "C", DataType = dataType, DataLength = length, DataPrecision = precision, DataScale = scale };

    [Fact]
    public void Format_Varchar2WithLength_ReturnsCharQualifier()
    {
        _formatter.Format(Col("VARCHAR2", length: 100)).Should().Be("VARCHAR2(100 CHAR)");
    }

    [Fact]
    public void Format_CharWithLength_ReturnsCharQualifier()
    {
        _formatter.Format(Col("CHAR", length: 1)).Should().Be("CHAR(1 CHAR)");
    }

    [Fact]
    public void Format_NumberWithPrecisionAndScale_ReturnsBothParams()
    {
        _formatter.Format(Col("NUMBER", precision: 10, scale: 2)).Should().Be("NUMBER(10,2)");
    }

    [Fact]
    public void Format_NumberWithPrecisionOnly_ReturnsPrecisionOnly()
    {
        _formatter.Format(Col("NUMBER", precision: 10)).Should().Be("NUMBER(10)");
    }

    [Fact]
    public void Format_NumberWithoutPrecision_ReturnsNumberOnly()
    {
        _formatter.Format(Col("NUMBER")).Should().Be("NUMBER");
    }

    [Fact]
    public void Format_Date_ReturnsDate()
    {
        _formatter.Format(Col("DATE")).Should().Be("DATE");
    }

    [Fact]
    public void Format_TimestampWithScale_ReturnsScaleParam()
    {
        _formatter.Format(Col("TIMESTAMP", scale: 6)).Should().Be("TIMESTAMP(6)");
    }

    [Fact]
    public void Format_TimestampWithoutScale_ReturnsTimestampOnly()
    {
        _formatter.Format(Col("TIMESTAMP")).Should().Be("TIMESTAMP");
    }

    [Fact]
    public void Format_Clob_ReturnsClob()
    {
        _formatter.Format(Col("CLOB")).Should().Be("CLOB");
    }

    [Fact]
    public void Format_Blob_ReturnsBlob()
    {
        _formatter.Format(Col("BLOB")).Should().Be("BLOB");
    }

    [Fact]
    public void Format_UnknownType_ReturnsDataTypeAsIs()
    {
        _formatter.Format(Col("NVARCHAR2")).Should().Be("NVARCHAR2");
    }

    [Fact]
    public void Format_LowercaseDataType_NormalizesAndFormatsCorrectly()
    {
        _formatter.Format(Col("varchar2", length: 50)).Should().Be("VARCHAR2(50 CHAR)");
    }

    [Fact]
    public void Format_Varchar2WithoutLength_ReturnsVarchar2Only()
    {
        _formatter.Format(Col("VARCHAR2")).Should().Be("VARCHAR2");
    }

    [Fact]
    public void Format_CharWithoutLength_ReturnsCharOnly()
    {
        _formatter.Format(Col("CHAR")).Should().Be("CHAR");
    }
}
