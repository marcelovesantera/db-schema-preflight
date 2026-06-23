using FluentAssertions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using DbSchemaPreflight.Core.ScriptAnalysis.Validation.Validators;

namespace DbSchemaPreflight.Core.Tests.ScriptAnalysis.Validation;

public sealed class DeleteValidatorTests
{
    private readonly DeleteValidator _validator = new();

    [Fact]
    public async Task Validate_TableDoesNotExist_ReturnsError()
    {
        var fake = new FakeScriptValidationQueries();
        var statement = new ParsedStatement(1,
            "DELETE FROM TB_MISSING WHERE ID = 1", StatementType.Delete);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Error);
        result.ErrorReason.Should().Contain("TB_MISSING");
    }

    [Fact]
    public async Task Validate_WhereClauseMatchesNoRows_ReturnsWarning()
    {
        var fake = new FakeScriptValidationQueries();
        fake.ExistingTables.Add("TB_USER");
        fake.SelectRowCount = 0;
        var statement = new ParsedStatement(1,
            "DELETE FROM TB_USER WHERE ID = 9999", StatementType.Delete);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Warning);
        result.Warnings.Should().ContainMatch("*no rows*");
    }

    [Fact]
    public async Task Validate_AllValid_ReturnsOk()
    {
        var fake = new FakeScriptValidationQueries();
        fake.ExistingTables.Add("TB_USER");
        fake.SelectRowCount = 3;
        var statement = new ParsedStatement(1,
            "DELETE FROM TB_USER WHERE STATUS = 'INACTIVE'", StatementType.Delete);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Ok);
    }
}
