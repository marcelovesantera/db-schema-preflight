using FluentAssertions;
using DbSchemaPreflight.Oracle.SqlGeneration;

namespace DbSchemaPreflight.Core.Tests.SqlGeneration;

public sealed class OracleIdentifierQuoterTests
{
    [Fact]
    public void Quote_SimpleIdentifier_ReturnsWrappedInDoubleQuotes()
    {
        OracleIdentifierQuoter.Quote("CLIENTES").Should().Be("\"CLIENTES\"");
    }

    [Fact]
    public void Quote_ReservedWord_ReturnsWrappedInDoubleQuotes()
    {
        OracleIdentifierQuoter.Quote("TABLE").Should().Be("\"TABLE\"");
    }

    [Fact]
    public void Quote_LowercaseIdentifier_ReturnsWrappedAsIs()
    {
        OracleIdentifierQuoter.Quote("nome").Should().Be("\"nome\"");
    }

    [Fact]
    public void Quote_SchemaName_ReturnsWrappedInDoubleQuotes()
    {
        OracleIdentifierQuoter.Quote("HR").Should().Be("\"HR\"");
    }
}
