using FluentAssertions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using DbSchemaPreflight.Core.ScriptAnalysis.Validation.Validators;

namespace DbSchemaPreflight.Core.Tests.ScriptAnalysis.Validation;

public sealed class CreateTableValidatorTests
{
    private readonly CreateTableValidator _validator = new();

    [Fact]
    public async Task Validate_TableAlreadyExists_ReturnsError()
    {
        var fake = new FakeScriptValidationQueries();
        fake.ExistingTables.Add("TB_USER");
        var statement = new ParsedStatement(1,
            "CREATE TABLE TB_USER (ID NUMBER NOT NULL)", StatementType.CreateTable);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Error);
        result.ErrorReason.Should().Contain("TB_USER");
    }

    [Fact]
    public async Task Validate_TableAlreadyExists_ResilienceSuggestionContainsOra955()
    {
        var fake = new FakeScriptValidationQueries();
        fake.ExistingTables.Add("TB_USER");
        var statement = new ParsedStatement(1,
            "CREATE TABLE TB_USER (ID NUMBER NOT NULL)", StatementType.CreateTable);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.ResilienceSuggestion.Should().Contain("-955");
    }

    [Fact]
    public async Task Validate_TableDoesNotExist_ReturnsOk()
    {
        var fake = new FakeScriptValidationQueries();
        var statement = new ParsedStatement(1,
            "CREATE TABLE TB_NEW (ID NUMBER NOT NULL)", StatementType.CreateTable);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Ok);
    }

    [Fact]
    public async Task Validate_TableDoesNotExist_ResilienceSuggestionIsNull()
    {
        var fake = new FakeScriptValidationQueries();
        var statement = new ParsedStatement(1,
            "CREATE TABLE TB_NEW (ID NUMBER NOT NULL)", StatementType.CreateTable);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.ResilienceSuggestion.Should().BeNull();
    }
}
