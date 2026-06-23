using FluentAssertions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using DbSchemaPreflight.Core.ScriptAnalysis.Validation.Validators;

namespace DbSchemaPreflight.Core.Tests.ScriptAnalysis.Validation;

public sealed class AlterTableAddValidatorTests
{
    private readonly AlterTableAddValidator _validator = new();

    [Fact]
    public async Task Validate_TableDoesNotExist_ReturnsError()
    {
        var fake = new FakeScriptValidationQueries();
        var statement = new ParsedStatement(1,
            "ALTER TABLE TB_MISSING ADD (EMAIL VARCHAR2(200))", StatementType.AlterTableAdd);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Error);
        result.ErrorReason.Should().Contain("TB_MISSING");
    }

    [Fact]
    public async Task Validate_ColumnAlreadyExists_ReturnsError()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "EMAIL");
        var statement = new ParsedStatement(1,
            "ALTER TABLE TB_USER ADD (EMAIL VARCHAR2(200))", StatementType.AlterTableAdd);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Error);
        result.ErrorReason.Should().Contain("EMAIL");
    }

    [Fact]
    public async Task Validate_ColumnAlreadyExists_ResilienceSuggestionContainsOra1430()
    {
        var fake = new FakeScriptValidationQueries();
        fake.AddTableWithColumns("TB_USER", "EMAIL");
        var statement = new ParsedStatement(1,
            "ALTER TABLE TB_USER ADD (EMAIL VARCHAR2(200))", StatementType.AlterTableAdd);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.ResilienceSuggestion.Should().Contain("-1430");
    }

    [Fact]
    public async Task Validate_TableExistsAndColumnDoesNotExist_ReturnsOk()
    {
        var fake = new FakeScriptValidationQueries();
        fake.ExistingTables.Add("TB_USER");
        var statement = new ParsedStatement(1,
            "ALTER TABLE TB_USER ADD (PHONE VARCHAR2(20))", StatementType.AlterTableAdd);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Ok);
    }
}
