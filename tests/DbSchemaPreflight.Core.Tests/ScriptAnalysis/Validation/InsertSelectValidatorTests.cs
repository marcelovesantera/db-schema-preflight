using FluentAssertions;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using DbSchemaPreflight.Core.ScriptAnalysis.Parsing;
using DbSchemaPreflight.Core.ScriptAnalysis.Validation.Validators;

namespace DbSchemaPreflight.Core.Tests.ScriptAnalysis.Validation;

public sealed class InsertSelectValidatorTests
{
    private readonly InsertSelectValidator _validator = new();

    [Fact]
    public async Task Validate_TargetTableDoesNotExist_ReturnsError()
    {
        var fake = new FakeScriptValidationQueries();
        var statement = new ParsedStatement(1,
            "INSERT INTO TB_MISSING (ID) SELECT ID FROM TB_SOURCE", StatementType.InsertSelect);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Error);
        result.ErrorReason.Should().Contain("TB_MISSING");
    }

    [Fact]
    public async Task Validate_SourceTableDoesNotExist_ReturnsError()
    {
        var fake = new FakeScriptValidationQueries();
        fake.ExistingTables.Add("TB_TARGET");
        var statement = new ParsedStatement(1,
            "INSERT INTO TB_TARGET (ID) SELECT ID FROM TB_SOURCE_MISSING", StatementType.InsertSelect);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Error);
        result.ErrorReason.Should().Contain("TB_SOURCE_MISSING");
    }

    [Fact]
    public async Task Validate_SelectReturnsZeroRows_ReturnsWarning()
    {
        var fake = new FakeScriptValidationQueries();
        fake.ExistingTables.Add("TB_TARGET");
        fake.ExistingTables.Add("TB_SOURCE");
        fake.SelectRowCount = 0;
        var statement = new ParsedStatement(1,
            "INSERT INTO TB_TARGET (ID) SELECT ID FROM TB_SOURCE", StatementType.InsertSelect);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Warning);
        result.Warnings.Should().ContainMatch("*no rows*");
    }

    [Fact]
    public async Task Validate_SelectReturnsRows_ReturnsOk()
    {
        var fake = new FakeScriptValidationQueries();
        fake.ExistingTables.Add("TB_TARGET");
        fake.ExistingTables.Add("TB_SOURCE");
        fake.SelectRowCount = 5;
        var statement = new ParsedStatement(1,
            "INSERT INTO TB_TARGET (ID) SELECT ID FROM TB_SOURCE", StatementType.InsertSelect);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Ok);
    }

    [Fact]
    public async Task Validate_AllValid_ReturnsOk()
    {
        var fake = new FakeScriptValidationQueries();
        fake.ExistingTables.Add("TB_TARGET");
        fake.ExistingTables.Add("TB_SOURCE");
        var statement = new ParsedStatement(1,
            "INSERT INTO TB_TARGET (ID) SELECT ID FROM TB_SOURCE", StatementType.InsertSelect);

        var result = await _validator.ValidateAsync(statement, "APP", fake);

        result.Status.Should().Be(ValidationStatus.Ok);
    }
}
