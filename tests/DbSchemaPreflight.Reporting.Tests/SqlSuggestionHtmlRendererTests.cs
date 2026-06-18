using FluentAssertions;
using DbSchemaPreflight.Core.Models;
using DbSchemaPreflight.Reporting;

namespace DbSchemaPreflight.Reporting.Tests;

public sealed class SqlSuggestionHtmlRendererTests
{
    private readonly SqlSuggestionHtmlRenderer _renderer = new();

    private static SqlSuggestion Suggestion(
        string sql = "SELECT 1 FROM DUAL",
        SqlSuggestionRisk risk = SqlSuggestionRisk.Low,
        bool isDestructive = false,
        IReadOnlyList<string>? warnings = null) =>
        new()
        {
            Title                = "Test suggestion",
            Sql                  = sql,
            Risk                 = risk,
            IsDestructive        = isDestructive,
            RequiresManualReview = false,
            Warnings             = warnings ?? []
        };

    // ── Risk badge colors ─────────────────────────────────────────────────────

    [Fact]
    public void RenderSuggestion_LowRisk_ContainsGreenBadge()
    {
        var html = _renderer.RenderSuggestion(Suggestion(risk: SqlSuggestionRisk.Low));
        html.Should().Contain("#28a745");
        html.Should().Contain("LOW");
    }

    [Fact]
    public void RenderSuggestion_MediumRisk_ContainsAmberBadge()
    {
        var html = _renderer.RenderSuggestion(Suggestion(risk: SqlSuggestionRisk.Medium));
        html.Should().Contain("#ffc107");
        html.Should().Contain("MEDIUM");
    }

    [Fact]
    public void RenderSuggestion_HighRisk_ContainsRedBadge()
    {
        var html = _renderer.RenderSuggestion(Suggestion(risk: SqlSuggestionRisk.High));
        html.Should().Contain("#dc3545");
        html.Should().Contain("HIGH");
    }

    // ── Destructive badge ─────────────────────────────────────────────────────

    [Fact]
    public void RenderSuggestion_IsDestructive_ContainsDestrutivoBadge()
    {
        var html = _renderer.RenderSuggestion(Suggestion(isDestructive: true));
        html.Should().Contain("DESTRUTIVO");
    }

    [Fact]
    public void RenderSuggestion_NotDestructive_DoesNotContainDestrutivoBadge()
    {
        var html = _renderer.RenderSuggestion(Suggestion(isDestructive: false));
        html.Should().NotContain("DESTRUTIVO");
    }

    // ── Warnings list ─────────────────────────────────────────────────────────

    [Fact]
    public void RenderSuggestion_WithWarnings_RendersWarningList()
    {
        var html = _renderer.RenderSuggestion(Suggestion(warnings: ["This may fail"]));
        html.Should().Contain("<ul");
        html.Should().Contain("This may fail");
    }

    [Fact]
    public void RenderSuggestion_WithNoWarnings_DoesNotRenderWarningList()
    {
        var html = _renderer.RenderSuggestion(Suggestion(warnings: []));
        html.Should().NotContain("<ul");
    }

    [Fact]
    public void RenderSuggestion_WithWarnings_HtmlEncodesWarningText()
    {
        var html = _renderer.RenderSuggestion(Suggestion(warnings: ["Risk: <script>alert(1)</script>"]));
        html.Should().Contain("&lt;script&gt;");
        html.Should().NotContain("<script>");
    }

    // ── SQL block HTML encoding ───────────────────────────────────────────────

    [Fact]
    public void RenderSuggestion_SqlBlock_HtmlEncodesSql()
    {
        var html = _renderer.RenderSuggestion(Suggestion(sql: "SELECT a < b FROM T"));
        html.Should().Contain("&lt;");
        html.Should().NotContain("SELECT a < b");
    }

    // ── Copy button JS escaping ───────────────────────────────────────────────

    [Fact]
    public void RenderSuggestion_CopyButton_EscapesSingleQuotesInSql()
    {
        // ' → \' (escape) → \&#39; (html-encode the quote) inside onclick='...'
        var html = _renderer.RenderSuggestion(Suggestion(sql: "DEFAULT 'A'"));
        html.Should().Contain(@"\&#39;");
    }

    [Fact]
    public void RenderSuggestion_CopyButton_EscapesNewlineInSql()
    {
        // \n in the copy button becomes \n (backslash + n) for correct JS string literal.
        // The <pre> block keeps the actual newline — only the copy button is checked here.
        var html = _renderer.RenderSuggestion(Suggestion(sql: "LINE1\nLINE2"));
        var buttonStart = html.IndexOf("<button", StringComparison.Ordinal);
        var buttonFragment = html[buttonStart..];
        buttonFragment.Should().Contain(@"\n");
        buttonFragment.Should().NotContain("LINE1\nLINE2");
    }

    [Fact]
    public void RenderSuggestion_CopyButton_EscapesBackslashInSql()
    {
        // \ → \\ so JS does not interpret it as an escape sequence
        var html = _renderer.RenderSuggestion(Suggestion(sql: @"A\B"));
        html.Should().Contain(@"A\\B");
    }

    // ── Fixed content ─────────────────────────────────────────────────────────

    [Fact]
    public void RenderSuggestion_ContainsSugestaoDeScriptHeader()
    {
        var html = _renderer.RenderSuggestion(Suggestion());
        html.Should().Contain("Sugestão de Script");
    }

    [Fact]
    public void RenderSuggestion_ContainsCopiarButton()
    {
        var html = _renderer.RenderSuggestion(Suggestion());
        html.Should().Contain("Copiar");
    }
}
