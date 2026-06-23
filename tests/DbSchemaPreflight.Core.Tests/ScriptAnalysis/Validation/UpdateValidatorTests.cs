using FluentAssertions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using DbSchemaPreflight.Core.ScriptAnalysis.Validation.Validators;

namespace DbSchemaPreflight.Core.Tests.ScriptAnalysis.Validation;

public sealed class UpdateValidatorTests
{
    private readonly UpdateValidator _validator = new();

    [Fact]
    public async Task Validate_TableDoesNotExist_ReturnsError()
    {
        var fake = new FakeScriptValidationQueries();
        var statement = new ParsedStatement(1,
            "UPDATE TB_MISSING SET NAME = 'Bob' WHERE ID = 1", StatementType.Update);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Error);
        result.ErrorReason.Should().Contain("TB_MISSING");
    }

    [Fact]
    public async Task Validate_SetColumnDoesNotExist_ReturnsError()
    {
        var fake = new FakeScriptValidationQueries();
        fake.ExistingTables.Add("TB_USER");
        var statement = new ParsedStatement(1,
            "UPDATE TB_USER SET BOGUS_COL = 1 WHERE ID = 1", StatementType.Update);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Error);
        result.ErrorReason.Should().Contain("BOGUS_COL");
    }

    [Fact]
    public async Task Validate_SetColumnIsPrimaryKey_ReturnsWarning()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "ID", "NAME");
        fake.PrimaryKeys["TB_USER"] = new List<string> { "ID" };
        var statement = new ParsedStatement(1,
            "UPDATE TB_USER SET ID = 99, NAME = 'Bob' WHERE ID = 1", StatementType.Update);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Warning);
        result.Warnings.Should().ContainMatch("*primary key*");
    }

    [Fact]
    public async Task Validate_WhereClauseMatchesNoRows_ReturnsWarning()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "NAME");
        fake.SelectRowCount = 0;
        var statement = new ParsedStatement(1,
            "UPDATE TB_USER SET NAME = 'Bob' WHERE ID = 9999", StatementType.Update);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Warning);
        result.Warnings.Should().ContainMatch("*matches no rows*");
    }

    [Fact]
    public async Task Validate_NoWhereClause_ReturnsWarning()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "NAME");
        var statement = new ParsedStatement(1,
            "UPDATE TB_USER SET NAME = 'Bob'", StatementType.Update);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Warning);
    }

    [Fact]
    public async Task Validate_NoWhereClause_WarningMessageContainsAffectsAllRows()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "NAME");
        var statement = new ParsedStatement(1,
            "UPDATE TB_USER SET NAME = 'Bob'", StatementType.Update);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Warnings.Should().ContainMatch("*affects all rows*");
    }

    [Fact]
    public async Task Validate_AllValid_ReturnsOk()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "NAME");
        fake.SelectRowCount = 1;
        var statement = new ParsedStatement(1,
            "UPDATE TB_USER SET NAME = 'Bob' WHERE ID = 1", StatementType.Update);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Ok);
    }

    [Fact]
    public async Task Validate_WhereMatchesNoRows_ResilienceSuggestionIsNotNull()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "NAME");
        fake.SelectRowCount = 0;
        var statement = new ParsedStatement(1,
            "UPDATE TB_USER SET NAME = 'Bob' WHERE ID = 9999", StatementType.Update);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.ResilienceSuggestion.Should().NotBeNullOrEmpty();
    }
}
