using FluentAssertions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;

namespace DbSchemaPreflight.Core.Tests.ScriptAnalysis;

public sealed class ScriptParserTests
{
    private readonly ScriptParser _parser = new();

    [Fact]
    public void Parse_EmptyContent_ReturnsEmpty()
    {
        var result = _parser.Parse(string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SingleInsert_ReturnsOneStatement()
    {
        var sql = "INSERT INTO TB_USER (ID, NAME) VALUES (1, 'Alice')";

        var result = _parser.Parse(sql);

        result.Should().HaveCount(1);
        result[0].SequenceNumber.Should().Be(1);
        result[0].Type.Should().Be(StatementType.Insert);
    }

    [Fact]
    public void Parse_PlSqlBlockWithThreeInserts_ReturnsThreeStatements()
    {
        var sql = @"
DECLARE
  v_id NUMBER;
BEGIN
  INSERT INTO TB_A (ID) VALUES (1);
  INSERT INTO TB_B (ID) VALUES (2);
  INSERT INTO TB_C (ID) VALUES (3);
END";

        var result = _parser.Parse(sql);

        result.Should().HaveCount(3);
        result.Should().AllSatisfy(s => s.Type.Should().Be(StatementType.Insert));
    }

    [Fact]
    public void Parse_PlSqlBlockWithDeclare_PopulatesDeclaredVariables()
    {
        var sql = @"
DECLARE
  v_id NUMBER;
  v_name VARCHAR2(100);
BEGIN
  INSERT INTO TB_USER (ID, NAME) VALUES (v_id, v_name)
END";

        var result = _parser.Parse(sql);

        result.Should().HaveCount(1);
        result[0].DeclaredVariables.Should().ContainKey("v_id");
        result[0].DeclaredVariables.Should().ContainKey("v_name");
        result[0].DeclaredVariables["v_id"].Should().Be("NUMBER");
        result[0].DeclaredVariables["v_name"].Should().Be("VARCHAR2(100)");
    }

    [Fact]
    public void Parse_StructuralKeywordsOnly_ReturnsEmpty()
    {
        var sql = "BEGIN;END;COMMIT;ROLLBACK;/";

        var result = _parser.Parse(sql);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_OnlyComments_ReturnsEmpty()
    {
        var sql = @"-- this is a comment
-- another comment
/* block comment */";

        var result = _parser.Parse(sql);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_QuotedTableName_PreservesRawText()
    {
        var sql = @"INSERT INTO ""MY_TABLE"" (ID) VALUES (1)";

        var result = _parser.Parse(sql);

        result.Should().HaveCount(1);
        result[0].RawText.Should().Contain("\"MY_TABLE\"");
    }

    [Fact]
    public void Parse_OnlyBlockComments_ReturnsEmpty()
    {
        var sql = "/* this is a block comment */";

        var result = _parser.Parse(sql);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MultipleStatementsOutsideBlock_ReturnsAllSequenced()
    {
        var sql = "INSERT INTO TB_A (ID) VALUES (1);\nUPDATE TB_B SET X = 1 WHERE ID = 2;\nDELETE FROM TB_C WHERE ID = 3";

        var result = _parser.Parse(sql);

        result.Should().HaveCount(3);
        result[0].SequenceNumber.Should().Be(1);
        result[1].SequenceNumber.Should().Be(2);
        result[2].SequenceNumber.Should().Be(3);
    }

    [Fact]
    public void Parse_DeleteStatement_ReturnsDeleteType()
    {
        var sql = "DELETE FROM TB_USER WHERE ID = 1";

        var result = _parser.Parse(sql);

        result.Should().HaveCount(1);
        result[0].Type.Should().Be(StatementType.Delete);
    }
}
