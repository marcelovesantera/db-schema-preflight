using System.Net;
using System.Text;
using DbSchemaPreflight.Core.Models;

namespace DbSchemaPreflight.Reporting;

public sealed class SqlSuggestionHtmlRenderer
{
    public string RenderSuggestion(SqlSuggestion suggestion)
    {
        var riskColor = suggestion.Risk switch
        {
            SqlSuggestionRisk.Low    => "#28a745",
            SqlSuggestionRisk.Medium => "#ffc107",
            SqlSuggestionRisk.High   => "#dc3545",
            _                        => "#6c757d"
        };
        var riskLabel = suggestion.Risk.ToString().ToUpperInvariant();

        var sb = new StringBuilder();
        sb.Append("<div style=\"margin-top:8px;padding:10px 12px;border:1px solid #c8cdd4;border-radius:4px;background:#f8f9fa;font-size:13px;\">");

        // Header
        sb.Append("<div style=\"display:flex;align-items:center;flex-wrap:wrap;gap:6px;margin-bottom:8px;\">");
        sb.Append("<span style=\"font-weight:600;\">Sugestão de Script</span>");
        sb.Append("<span style=\"color:#6c757d;\">— não executado automaticamente</span>");
        sb.Append($"<span style=\"font-size:10px;font-weight:700;padding:2px 6px;border-radius:3px;color:#fff;background:{riskColor};text-transform:uppercase;\">{riskLabel}</span>");
        if (suggestion.IsDestructive)
            sb.Append("<span style=\"font-size:10px;font-weight:700;padding:2px 6px;border-radius:3px;color:#fff;background:#dc3545;text-transform:uppercase;\">DESTRUTIVO</span>");
        sb.Append("</div>");

        // Warnings
        if (suggestion.Warnings.Count > 0)
        {
            sb.Append("<ul style=\"margin:0 0 8px 18px;padding:0;color:#856404;\">");
            foreach (var w in suggestion.Warnings)
                sb.Append($"<li>{WebUtility.HtmlEncode(w)}</li>");
            sb.Append("</ul>");
        }

        // SQL pre block
        sb.Append("<pre style=\"margin:0 0 6px;padding:10px 12px;background:#21272a;color:#f4f4f4;border-radius:4px;font-size:12px;overflow-x:auto;white-space:pre-wrap;word-break:break-all;\"><code>");
        sb.Append(WebUtility.HtmlEncode(suggestion.Sql));
        sb.Append("</code></pre>");

        // Copy button
        var jsValue = WebUtility.HtmlEncode(EscapeForJsSingleQuote(suggestion.Sql));
        sb.Append($"<button onclick=\"navigator.clipboard.writeText('{jsValue}')\" style=\"padding:3px 10px;font-size:12px;cursor:pointer;border:1px solid #c8cdd4;border-radius:3px;background:#fff;\">Copiar</button>");

        sb.Append("</div>");
        return sb.ToString();
    }

    private static string EscapeForJsSingleQuote(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
}
