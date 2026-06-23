using FluentAssertions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;

namespace DbSchemaPreflight.Core.Tests.ScriptAnalysis;

public sealed class StatementClassifierTests
{
    private readonly StatementClassifier _classifier = new();

    [Fact]
    public void Classify_InsertWithValues_ReturnsInsert()
    {
        var result = _classifier.Classify("INSERT INTO TB_USER (ID) VALUES (1)");

        result.Should().Be(StatementType.Insert);
    }

    [Fact]
    public void Classify_InsertWithSelect_ReturnsInsertSelect()
    {
        var result = _classifier.Classify("INSERT INTO TB_USER (ID) SELECT ID FROM TB_SOURCE");

        result.Should().Be(StatementType.InsertSelect);
    }

    [Fact]
    public void Classify_Update_ReturnsUpdate()
    {
        var result = _classifier.Classify("UPDATE TB_USER SET NAME = 'Bob' WHERE ID = 1");

        result.Should().Be(StatementType.Update);
    }

    [Fact]
    public void Classify_DeleteFrom_ReturnsDelete()
    {
        var result = _classifier.Classify("DELETE FROM TB_USER WHERE ID = 1");

        result.Should().Be(StatementType.Delete);
    }

    [Fact]
    public void Classify_CreateTable_ReturnsCreateTable()
    {
        var result = _classifier.Classify("CREATE TABLE TB_NEW (ID NUMBER)");

        result.Should().Be(StatementType.CreateTable);
    }

    [Fact]
    public void Classify_AlterTableAdd_ReturnsAlterTableAdd()
    {
        var result = _classifier.Classify("ALTER TABLE TB_USER ADD (EMAIL VARCHAR2(200))");

        result.Should().Be(StatementType.AlterTableAdd);
    }

    [Fact]
    public void Classify_CreateOrReplaceView_ReturnsCreateOrReplaceView()
    {
        var result = _classifier.Classify("CREATE OR REPLACE VIEW VW_USERS AS SELECT * FROM TB_USER");

        result.Should().Be(StatementType.CreateOrReplaceView);
    }

    [Fact]
    public void Classify_CreateView_ReturnsCreateOrReplaceView()
    {
        var result = _classifier.Classify("CREATE VIEW VW_USERS AS SELECT * FROM TB_USER");

        result.Should().Be(StatementType.CreateOrReplaceView);
    }

    [Fact]
    public void Classify_UnknownStatement_ReturnsOther()
    {
        var result = _classifier.Classify("MERGE INTO TB_USER USING TB_SOURCE ON (TB_USER.ID = TB_SOURCE.ID)");

        result.Should().Be(StatementType.Other);
    }

    [Fact]
    public void Classify_LowercaseInsert_ReturnsInsert()
    {
        var result = _classifier.Classify("insert into tb_user (id) values (1)");

        result.Should().Be(StatementType.Insert);
    }

    [Fact]
    public void Classify_MixedCaseUpdate_ReturnsUpdate()
    {
        var result = _classifier.Classify("Update TB_USER Set Name = 'Bob'");

        result.Should().Be(StatementType.Update);
    }
}
