using FluentAssertions;
using DbSchemaPreflight.Core.ScriptAnalysis;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using DbSchemaPreflight.Core.ScriptAnalysis.Validation.Validators;

namespace DbSchemaPreflight.Core.Tests.ScriptAnalysis.Validation;

public sealed class InsertValidatorTests
{
    private readonly InsertValidator _validator = new();

    [Fact]
    public async Task Validate_TableDoesNotExist_ReturnsError()
    {
        var fake = new FakeScriptValidationQueries();
        var statement = new ParsedStatement(1, "INSERT INTO TB_MISSING (ID) VALUES (1)", StatementType.Insert);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Error);
        result.ErrorReason.Should().Contain("TB_MISSING");
    }

    [Fact]
    public async Task Validate_ColumnDoesNotExist_ReturnsError()
    {
        var fake = new FakeScriptValidationQueries();
        fake.ExistingTables.Add("TB_USER");
        var statement = new ParsedStatement(1, "INSERT INTO TB_USER (BOGUS_COL) VALUES (1)", StatementType.Insert);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Error);
        result.ErrorReason.Should().Contain("BOGUS_COL");
    }

    [Fact]
    public async Task Validate_PrimaryKeyDuplicated_ReturnsError()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "ID", "NAME");
        fake.PrimaryKeys["TB_USER"] = new List<string> { "ID" };
        fake.AddColumnValue("TB_USER", "ID", "1");
        var statement = new ParsedStatement(1, "INSERT INTO TB_USER (ID, NAME) VALUES (1, 'Alice')", StatementType.Insert);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Error);
        result.ErrorReason.Should().Contain("Duplicate");
    }

    [Fact]
    public async Task Validate_PrimaryKeyDuplicated_ResilienceSuggestionContainsNotExists()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "ID", "NAME");
        fake.PrimaryKeys["TB_USER"] = new List<string> { "ID" };
        fake.AddColumnValue("TB_USER", "ID", "1");
        var statement = new ParsedStatement(1, "INSERT INTO TB_USER (ID, NAME) VALUES (1, 'Alice')", StatementType.Insert);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.ResilienceSuggestion.Should().Contain("NOT EXISTS");
    }

    [Fact]
    public async Task Validate_ForeignKeyValueMissing_ReturnsError()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_ORDER", "ID", "USER_ID");
        fake.ExistingTables.Add("TB_USER");
        fake.ForeignKeyRefs[("TB_ORDER", "USER_ID")] = new List<ForeignKeyReference>
        {
            new ForeignKeyReference("TB_USER", "ID")
        };
        var statement = new ParsedStatement(1, "INSERT INTO TB_ORDER (ID, USER_ID) VALUES (1, 99)", StatementType.Insert);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Error);
        result.ErrorReason.Should().Contain("Foreign key");
    }

    [Fact]
    public async Task Validate_NotNullColumnMissingValue_ReturnsError()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "ID");
        fake.NotNullColumnsWithoutDefault["TB_USER"] = new List<ColumnConstraintInfo>
        {
            new ColumnConstraintInfo("NAME", IsNotNull: true, HasDefault: false)
        };
        var statement = new ParsedStatement(1, "INSERT INTO TB_USER (ID) VALUES (1)", StatementType.Insert);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Error);
        result.ErrorReason.Should().Contain("NAME");
    }

    [Fact]
    public async Task Validate_PlSqlVariableInValue_AddsToRuntimeDependentFields()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "ID", "NAME");
        var vars = new Dictionary<string, string> { ["v_id"] = "NUMBER" };
        var statement = new ParsedStatement(1, "INSERT INTO TB_USER (ID, NAME) VALUES (v_id, 'Alice')", StatementType.Insert, vars);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Ok);
        result.RuntimeDependentFields.Should().Contain("v_id");
    }

    [Fact]
    public async Task Validate_PlSqlVariableInValue_SkipsPkValidation()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "ID");
        fake.PrimaryKeys["TB_USER"] = new List<string> { "ID" };
        fake.AddColumnValue("TB_USER", "ID", "42");
        var vars = new Dictionary<string, string> { ["v_id"] = "NUMBER" };
        var statement = new ParsedStatement(1, "INSERT INTO TB_USER (ID) VALUES (v_id)", StatementType.Insert, vars);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Ok);
    }

    [Fact]
    public async Task Validate_SequenceNextvalInValue_AddsToRuntimeDependentFields()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "ID", "NAME");
        var statement = new ParsedStatement(1, "INSERT INTO TB_USER (ID, NAME) VALUES (SEQ_USER.nextval, 'Alice')", StatementType.Insert);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Ok);
        result.RuntimeDependentFields.Should().Contain("SEQ_USER.nextval");
    }

    [Fact]
    public async Task Validate_AllValid_ReturnsOk()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "ID", "NAME");
        fake.PrimaryKeys["TB_USER"] = new List<string> { "ID" };
        var statement = new ParsedStatement(1, "INSERT INTO TB_USER (ID, NAME) VALUES (1, 'Alice')", StatementType.Insert);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Ok);
    }

    [Fact]
    public async Task Validate_AllValid_ResilienceSuggestionIsNull()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "ID", "NAME");
        fake.PrimaryKeys["TB_USER"] = new List<string> { "ID" };
        var statement = new ParsedStatement(1, "INSERT INTO TB_USER (ID, NAME) VALUES (1, 'Alice')", StatementType.Insert);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.ResilienceSuggestion.Should().BeNull();
    }
}
