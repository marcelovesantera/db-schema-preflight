using System.Net;
using System.Text;
using DbSchemaPreflight.Core.ScriptAnalysis.Models;
using Scriban;
using Scriban.Runtime;

namespace DbSchemaPreflight.Reporting.ScriptAnalysis;

public sealed class ScriptAnalysisHtmlReportGenerator
{
    public async Task GenerateAsync(ScriptAnalysisResult result, string schema, string outputPath)
    {
        var model = MapModel(result, schema);
        var html = Render(model);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(outputPath, html, Encoding.UTF8);
    }

    private static ScriptAnalysisReportModel MapModel(ScriptAnalysisResult result, string schema) =>
        new()
        {
            FilePath    = result.FilePath,
            Schema      = schema,
            AnalysedAt  = result.AnalysedAt,
            OkCount     = result.OkCount,
            ErrorCount  = result.ErrorCount,
            WarningCount = result.WarningCount,
            SkippedCount = result.SkippedCount,
            Statements  = result.Statements.Select(StatementResultViewModel.From).ToList()
        };

    private static string Render(ScriptAnalysisReportModel model)
    {
        var template = Template.Parse(TemplateHtml);

        var root = new ScriptObject();
        root.Add("file_path",     WebUtility.HtmlEncode(model.FilePath));
        root.Add("schema",        WebUtility.HtmlEncode(model.Schema));
        root.Add("analysed_at",   model.AnalysedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        root.Add("ok_count",      model.OkCount);
        root.Add("error_count",   model.ErrorCount);
        root.Add("warning_count", model.WarningCount);
        root.Add("skipped_count", model.SkippedCount);

        var statements = new ScriptArray();
        foreach (var s in model.Statements)
        {
            var obj = new ScriptObject();
            obj.Add("sequence_number",         s.SequenceNumber);
            obj.Add("statement_text",          WebUtility.HtmlEncode(s.StatementText));
            obj.Add("status_label",            s.StatusLabel);
            obj.Add("status_css",              s.StatusCssClass);
            obj.Add("error_reason",            s.ErrorReason is not null ? WebUtility.HtmlEncode(s.ErrorReason) : "");
            obj.Add("resilience_suggestion",   s.ResilienceSuggestion is not null ? WebUtility.HtmlEncode(s.ResilienceSuggestion) : "");
            obj.Add("suggestion_id",           Guid.NewGuid().ToString("N"));

            var warnings = new ScriptArray();
            foreach (var w in s.Warnings)
                warnings.Add(WebUtility.HtmlEncode(w));
            obj.Add("warnings", warnings);

            var runtimeFields = new ScriptArray();
            foreach (var f in s.RuntimeDependentFields)
                runtimeFields.Add(WebUtility.HtmlEncode(f));
            obj.Add("runtime_dependent_fields", runtimeFields);

            statements.Add(obj);
        }
        root.Add("statements", statements);

        var context = new TemplateContext { StrictVariables = false, LoopLimit = 0 };
        context.PushGlobal(root);
        return template.Render(context);
    }

    private const string TemplateHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <title>DB Schema Preflight — Script Analysis</title>
            <style>
                :root {
                    --ok:      #28a745;
                    --ok-bg:   #f0fff4;
                    --error:   #dc3545;
                    --error-bg:#fff5f5;
                    --warn:    #d68910;
                    --warn-bg: #fffbea;
                    --skip:    #6c757d;
                    --skip-bg: #f8f9fa;
                    --border:  #dde1e6;
                    --text:    #21272a;
                    --muted:   #697077;
                    --header-bg: #21272a;
                    --section-bg:#f4f4f4;
                    --font: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                }
                *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
                body { font-family: var(--font); font-size: 14px; color: var(--text); background: #fff; line-height: 1.6; }
                #header { background: var(--header-bg); color: #fff; padding: 24px 32px; }
                #header h1 { font-size: 20px; font-weight: 600; margin-bottom: 8px; }
                #header .meta { font-size: 13px; opacity: .75; margin-top: 4px; }
                #header .meta strong { opacity: 1; font-weight: 600; }
                main { max-width: 960px; margin: 0 auto; padding: 0 24px 40px; }
                .summary-bar {
                    display: flex; gap: 24px; flex-wrap: wrap;
                    padding: 16px 0; border-bottom: 2px solid var(--border); margin-bottom: 24px; margin-top: 24px;
                    font-size: 15px; font-weight: 600;
                }
                .summary-item { display: flex; align-items: center; gap: 8px; }
                .summary-count { font-size: 22px; font-weight: 700; }
                .summary-count.ok     { color: var(--ok); }
                .summary-count.error  { color: var(--error); }
                .summary-count.warning{ color: var(--warn); }
                .summary-count.skipped{ color: var(--skip); }
                .stmt-list { list-style: none; }
                .stmt-item {
                    border-left: 4px solid var(--border); border-radius: 0 6px 6px 0;
                    background: var(--section-bg); margin-bottom: 8px; padding: 10px 14px;
                }
                .stmt-item.ok      { border-left-color: var(--ok);    background: var(--ok-bg); }
                .stmt-item.error   { border-left-color: var(--error);  background: var(--error-bg); }
                .stmt-item.warning { border-left-color: var(--warn);   background: var(--warn-bg); }
                .stmt-item.skipped { border-left-color: var(--skip);   background: var(--skip-bg); }
                .stmt-header { display: flex; align-items: flex-start; gap: 10px; }
                .stmt-num { font-size: 12px; color: var(--muted); white-space: nowrap; min-width: 28px; padding-top: 2px; }
                .badge {
                    font-size: 10px; font-weight: 700; padding: 2px 7px; border-radius: 3px;
                    text-transform: uppercase; white-space: nowrap; flex-shrink: 0;
                }
                .badge.ok      { background: var(--ok);    color: #fff; }
                .badge.error   { background: var(--error);  color: #fff; }
                .badge.warning { background: var(--warn);   color: #fff; }
                .badge.skipped { background: var(--skip);   color: #fff; }
                .stmt-text {
                    font-family: 'Courier New', Courier, monospace; font-size: 12px;
                    color: var(--text); white-space: pre-wrap; word-break: break-all; flex: 1;
                }
                .stmt-detail { margin-top: 8px; padding-left: 38px; font-size: 13px; }
                .stmt-error-reason { color: var(--error); margin-bottom: 6px; }
                .stmt-warning-item { color: var(--warn); margin-bottom: 4px; }
                .stmt-runtime-note { font-style: italic; color: var(--muted); margin-top: 6px; font-size: 12px; }
                .resilience-block { margin-top: 8px; border: 1px solid var(--border); border-radius: 4px; background: #1e1e1e; overflow: hidden; }
                .resilience-header {
                    display: flex; align-items: center; justify-content: space-between;
                    padding: 4px 10px; background: #2d2d2d; border-bottom: 1px solid #444;
                    font-size: 11px; color: #aaa; font-family: 'Courier New', monospace;
                }
                .copy-btn {
                    padding: 2px 8px; font-size: 11px; border: 1px solid #555; border-radius: 3px;
                    background: #3a3a3a; color: #ddd; cursor: pointer; font-family: var(--font);
                }
                .copy-btn:hover { background: #4a4a4a; color: #fff; }
                .resilience-code {
                    padding: 10px 14px; font-family: 'Courier New', Courier, monospace;
                    font-size: 12px; color: #d4d4d4; white-space: pre-wrap; word-break: break-all; margin: 0;
                }
            </style>
            <script>
                function copyResilience(id) {
                    var code = document.getElementById('res-code-' + id).textContent;
                    var btn = event.target;
                    navigator.clipboard.writeText(code).then(function() {
                        var orig = btn.textContent;
                        btn.textContent = 'Copied!';
                        setTimeout(function() { btn.textContent = orig; }, 1500);
                    });
                }
            </script>
        </head>
        <body>

        <header id="header">
            <h1>DB Schema Preflight — Script Analysis</h1>
            <p class="meta">File: <strong>{{ file_path }}</strong></p>
            <p class="meta">Schema: <strong>{{ schema }}</strong></p>
            <p class="meta">Analysed at: <strong>{{ analysed_at }}</strong></p>
        </header>

        <main>
            <div class="summary-bar">
                <div class="summary-item"><span class="summary-count ok">{{ ok_count }}</span> OK</div>
                <div class="summary-item"><span class="summary-count error">{{ error_count }}</span> ERROR</div>
                <div class="summary-item"><span class="summary-count warning">{{ warning_count }}</span> WARNING</div>
                <div class="summary-item"><span class="summary-count skipped">{{ skipped_count }}</span> SKIPPED</div>
            </div>

            <ul class="stmt-list">
            {{- for s in statements }}
            <li class="stmt-item {{ s.status_css }}">
                <div class="stmt-header">
                    <span class="stmt-num">#{{ s.sequence_number }}</span>
                    <span class="badge {{ s.status_css }}">{{ s.status_label }}</span>
                    <pre class="stmt-text">{{ s.statement_text }}</pre>
                </div>
                {{- if s.error_reason != "" || s.warnings.size > 0 || s.resilience_suggestion != "" || s.runtime_dependent_fields.size > 0 }}
                <div class="stmt-detail">
                    {{- if s.error_reason != "" }}
                    <div class="stmt-error-reason">Reason: {{ s.error_reason }}</div>
                    {{- end }}
                    {{- for w in s.warnings }}
                    <div class="stmt-warning-item">⚠ {{ w }}</div>
                    {{- end }}
                    {{- if s.runtime_dependent_fields.size > 0 }}
                    <div class="stmt-runtime-note">Values not verified (runtime-dependent): {{ s.runtime_dependent_fields | array.join ", " }}</div>
                    {{- end }}
                    {{- if s.resilience_suggestion != "" }}
                    <div class="resilience-block">
                        <div class="resilience-header">
                            <span>Resilience suggestion — review before executing</span>
                            <button class="copy-btn" onclick="copyResilience('{{ s.suggestion_id }}')">Copy</button>
                        </div>
                        <pre id="res-code-{{ s.suggestion_id }}" class="resilience-code">{{ s.resilience_suggestion }}</pre>
                    </div>
                    {{- end }}
                </div>
                {{- end }}
            </li>
            {{- end }}
            </ul>
        </main>

        </body>
        </html>
        """;
}
