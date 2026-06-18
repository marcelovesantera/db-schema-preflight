using System.Net;
using System.Text;
using DbSchemaPreflight.Core.Models;
using DbSchemaPreflight.Reporting.Models;
using Scriban;
using Scriban.Runtime;

namespace DbSchemaPreflight.Reporting;

public sealed class HtmlReportGenerator
{
    public string Generate(ReportModel model)
    {
        var template = Template.Parse(TemplateHtml);

        var scriptObject = new ScriptObject();
        scriptObject.Add("reference_schema", model.ReferenceSchema);
        scriptObject.Add("target_schema", model.TargetSchema);
        scriptObject.Add("generated_at", model.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"));

        var summary = new ScriptObject();
        summary.Add("total_tables_in_reference", model.Summary.TotalTablesInReference);
        summary.Add("total_tables_in_target", model.Summary.TotalTablesInTarget);
        summary.Add("total_differences", model.Summary.TotalDifferences);
        summary.Add("critical_count", model.Summary.CriticalCount);
        summary.Add("warning_count", model.Summary.WarningCount);
        summary.Add("info_count", 0);
        summary.Add("readiness_status", model.Summary.ReadinessStatus);
        scriptObject.Add("summary", summary);

        var renderer = new SqlSuggestionHtmlRenderer();
        var suggestionsByDiff = model.SuggestionsByDiff;

        var sharedTableDiffs = model.Differences
            .Where(d => d.Type != DifferenceType.MissingTable && d.Type != DifferenceType.ExtraTable)
            .ToList();

        scriptObject.Add("has_differences", sharedTableDiffs.Count > 0);
        scriptObject.Add("critical_diffs", MapDifferences(
            sharedTableDiffs.Where(d => d.Severity == DifferenceSeverity.Critical), suggestionsByDiff, renderer));
        scriptObject.Add("warning_diffs", MapDifferences(
            sharedTableDiffs.Where(d => d.Severity == DifferenceSeverity.Warning), suggestionsByDiff, renderer));
        scriptObject.Add("info_diffs", MapDifferences(
            sharedTableDiffs.Where(d => d.Severity == DifferenceSeverity.Info), suggestionsByDiff, renderer));

        var tableGroups = new ScriptArray();
        foreach (var group in sharedTableDiffs.GroupBy(d => d.TableName).OrderBy(g => g.Key))
        {
            var tableGroup = new ScriptObject();
            tableGroup.Add("table_name", group.Key);
            tableGroup.Add("diff_count", group.Count());

            var columnGroups = new ScriptArray();
            foreach (var colGroup in group
                .GroupBy(d => d.ColumnName ?? string.Empty)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var colGroupObj = new ScriptObject();
                colGroupObj.Add("column_name", colGroup.Key);
                colGroupObj.Add("diffs", MapDifferences(colGroup.OrderBy(d => d.Severity switch
                {
                    DifferenceSeverity.Critical => 0,
                    DifferenceSeverity.Warning  => 1,
                    _                           => 2
                }), suggestionsByDiff, renderer));
                columnGroups.Add(colGroupObj);
            }
            tableGroup.Add("column_groups", columnGroups);
            tableGroups.Add(tableGroup);
        }
        scriptObject.Add("table_groups", tableGroups);

        var missingTables = new ScriptArray();
        foreach (var d in model.Differences.Where(d => d.Type == DifferenceType.MissingTable))
            missingTables.Add(d.TableName);
        scriptObject.Add("missing_tables", missingTables);

        var extraTables = new ScriptArray();
        foreach (var d in model.Differences.Where(d => d.Type == DifferenceType.ExtraTable))
            extraTables.Add(d.TableName);
        scriptObject.Add("extra_tables", extraTables);

        var context = new TemplateContext { StrictVariables = false, LoopLimit = 0 };
        context.PushGlobal(scriptObject);

        return template.Render(context);
    }

    public void Save(string html, string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(outputPath, html, Encoding.UTF8);
    }

    private static ScriptArray MapDifferences(
        IEnumerable<SchemaDifference> diffs,
        IReadOnlyDictionary<SchemaDifference, IReadOnlyList<SqlSuggestion>> suggestionsByDiff,
        SqlSuggestionHtmlRenderer renderer)
    {
        var array = new ScriptArray();
        foreach (var d in diffs)
        {
            var obj = new ScriptObject();
            obj.Add("severity", d.Severity.ToString());
            obj.Add("type", d.Type.ToString());
            obj.Add("table_name", d.TableName);
            obj.Add("column_name", d.ColumnName ?? string.Empty);
            obj.Add("message", d.Message);
            obj.Add("reference_value", d.ReferenceValue ?? string.Empty);
            obj.Add("target_value", d.TargetValue ?? string.Empty);
            obj.Add("suggestion_id", Guid.NewGuid().ToString("N"));

            var suggestionSql = string.Empty;
            var suggestionHtml = string.Empty;
            if (suggestionsByDiff.TryGetValue(d, out var suggestions))
            {
                suggestionSql = WebUtility.HtmlEncode(
                    string.Join("\n\n", suggestions.Select(s => s.Sql)));
                var sb = new StringBuilder();
                foreach (var s in suggestions)
                    sb.Append(renderer.RenderSuggestion(s));
                suggestionHtml = sb.ToString();
            }
            obj.Add("suggestion_sql", suggestionSql);
            obj.Add("suggestion_html", suggestionHtml);

            array.Add(obj);
        }
        return array;
    }

    private const string TemplateHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <title>Oracle Preflight Report — {{ reference_schema }} vs {{ target_schema }}</title>
            <style>
                :root {
                    --color-critical: #c0392b;
                    --color-critical-bg: #fdecea;
                    --color-warning: #d68910;
                    --color-warning-bg: #fef9e7;
                    --color-info: #2471a3;
                    --color-info-bg: #eaf4fb;
                    --color-ready: #1e8449;
                    --color-ready-bg: #eafaf1;
                    --color-not-ready: #c0392b;
                    --color-not-ready-bg: #fdecea;
                    --color-needs-review: #d68910;
                    --color-needs-review-bg: #fef9e7;
                    --color-border: #dde1e6;
                    --color-text: #21272a;
                    --color-text-muted: #697077;
                    --color-bg: #ffffff;
                    --color-header-bg: #21272a;
                    --color-header-text: #ffffff;
                    --color-section-bg: #f4f4f4;
                    --font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', sans-serif;
                    --font-size-base: 14px;
                    --spacing-xs: 4px;
                    --spacing-sm: 8px;
                    --spacing-md: 16px;
                    --spacing-lg: 24px;
                    --spacing-xl: 32px;
                    --border-radius: 4px;
                    --border-radius-lg: 8px;
                }

                *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

                body {
                    font-family: var(--font-family);
                    font-size: var(--font-size-base);
                    color: var(--color-text);
                    background: var(--color-bg);
                    line-height: 1.6;
                }

                #header {
                    background: var(--color-header-bg);
                    color: var(--color-header-text);
                    padding: var(--spacing-lg) var(--spacing-xl);
                }

                #header h1 { font-size: 20px; font-weight: 600; margin-bottom: var(--spacing-sm); }
                #header .meta { font-size: 13px; opacity: 0.75; margin-top: var(--spacing-xs); }
                #header .meta strong { opacity: 1; font-weight: 600; }

                main {
                    max-width: 1100px;
                    margin: 0 auto;
                    padding: 0 var(--spacing-lg) var(--spacing-xl);
                }

                /* Tab bar */
                .tab-bar {
                    display: flex;
                    gap: 2px;
                    border-bottom: 2px solid var(--color-border);
                    margin-bottom: var(--spacing-xl);
                    padding-top: var(--spacing-lg);
                    flex-wrap: wrap;
                }

                .tab-btn {
                    padding: var(--spacing-sm) var(--spacing-md);
                    border: none;
                    background: transparent;
                    font-family: var(--font-family);
                    font-size: 13px;
                    font-weight: 500;
                    color: var(--color-text-muted);
                    cursor: pointer;
                    border-bottom: 2px solid transparent;
                    margin-bottom: -2px;
                    border-radius: var(--border-radius) var(--border-radius) 0 0;
                    white-space: nowrap;
                }

                .tab-btn:hover { color: var(--color-text); background: var(--color-section-bg); }

                .tab-btn.active {
                    color: var(--color-text);
                    border-bottom-color: var(--color-text);
                    font-weight: 600;
                }

                /* Tab panels */
                .tab-panel { display: none; }
                .tab-panel.active { display: block; }

                /* Panel headings */
                .panel-title {
                    font-size: 15px;
                    font-weight: 600;
                    text-transform: uppercase;
                    letter-spacing: 0.6px;
                    color: var(--color-text-muted);
                    border-bottom: 2px solid var(--color-border);
                    padding-bottom: var(--spacing-sm);
                    margin-bottom: var(--spacing-md);
                }

                .panel-title + .panel-title { margin-top: var(--spacing-xl); }

                /* Filter bar */
                .filter-bar { margin-bottom: var(--spacing-md); }

                .filter-input {
                    width: 100%;
                    max-width: 400px;
                    padding: var(--spacing-sm) var(--spacing-md);
                    border: 1px solid var(--color-border);
                    border-radius: var(--border-radius);
                    font-family: var(--font-family);
                    font-size: 13px;
                    color: var(--color-text);
                    background: var(--color-bg);
                }

                .filter-input:focus { outline: none; border-color: var(--color-text); }

                /* Summary grid */
                .summary-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
                    gap: var(--spacing-md);
                    margin-bottom: var(--spacing-xl);
                }

                .summary-card {
                    border: 1px solid var(--color-border);
                    border-radius: var(--border-radius-lg);
                    padding: var(--spacing-md);
                    text-align: center;
                    background: var(--color-section-bg);
                }

                .summary-card .value { font-size: 30px; font-weight: 700; line-height: 1; }
                .summary-card .label { font-size: 12px; color: var(--color-text-muted); margin-top: var(--spacing-xs); }
                .summary-card.critical .value { color: var(--color-critical); }
                .summary-card.warning .value  { color: var(--color-warning); }
                .summary-card.info .value     { color: var(--color-info); }

                /* Readiness badge */
                .status-badge {
                    display: inline-block;
                    padding: var(--spacing-sm) var(--spacing-md);
                    border-radius: var(--border-radius);
                    font-size: 20px;
                    font-weight: 700;
                    letter-spacing: 1.5px;
                    border-width: 2px;
                    border-style: solid;
                }

                .status-badge.not-ready   { background: var(--color-not-ready-bg);   color: var(--color-not-ready);   border-color: var(--color-not-ready); }
                .status-badge.needs-review { background: var(--color-needs-review-bg); color: var(--color-needs-review); border-color: var(--color-needs-review); }
                .status-badge.ready       { background: var(--color-ready-bg);        color: var(--color-ready);        border-color: var(--color-ready); }

                /* Diff items */
                .diff-list { list-style: none; }

                .diff-item {
                    padding: var(--spacing-sm) var(--spacing-md);
                    border-left: 4px solid var(--color-border);
                    margin-bottom: var(--spacing-sm);
                    border-radius: 0 var(--border-radius) var(--border-radius) 0;
                    background: var(--color-section-bg);
                }

                .diff-item.critical { border-left-color: var(--color-critical); background: var(--color-critical-bg); }
                .diff-item.warning  { border-left-color: var(--color-warning);  background: var(--color-warning-bg);  }
                .diff-item.info     { border-left-color: var(--color-info);     background: var(--color-info-bg);     }

                .diff-header {
                    display: flex;
                    align-items: center;
                    gap: var(--spacing-sm);
                    margin-bottom: var(--spacing-xs);
                    font-size: 13px;
                    font-weight: 500;
                }

                .diff-badge {
                    font-size: 10px;
                    font-weight: 700;
                    padding: 2px 6px;
                    border-radius: 3px;
                    text-transform: uppercase;
                    white-space: nowrap;
                }

                .diff-badge.critical { background: var(--color-critical); color: #fff; }
                .diff-badge.warning  { background: var(--color-warning);  color: #fff; }
                .diff-badge.info     { background: var(--color-info);     color: #fff; }

                .diff-message { font-size: 13px; color: var(--color-text); }

                .diff-meta {
                    font-size: 12px;
                    color: var(--color-text-muted);
                    margin-top: var(--spacing-xs);
                    font-family: 'Courier New', Courier, monospace;
                }

                /* Severity groups */
                .severity-group { margin-bottom: var(--spacing-md); }

                .severity-group-title {
                    font-size: 12px;
                    font-weight: 700;
                    text-transform: uppercase;
                    letter-spacing: 0.8px;
                    margin-bottom: var(--spacing-sm);
                    padding: var(--spacing-xs) var(--spacing-sm);
                    border-radius: var(--border-radius);
                    display: inline-flex;
                    align-items: center;
                    cursor: pointer;
                    user-select: none;
                }

                .severity-group-title::after {
                    content: '▼';
                    font-size: 9px;
                    font-family: sans-serif;
                    margin-left: var(--spacing-xs);
                }

                .severity-group-title.collapsed::after { content: '►'; }
                .severity-group-title.collapsed + .diff-list { display: none; }

                .severity-group-title.critical { color: var(--color-critical); background: var(--color-critical-bg); }
                .severity-group-title.warning  { color: var(--color-warning);  background: var(--color-warning-bg);  }
                .severity-group-title.info     { color: var(--color-info);     background: var(--color-info-bg);     }

                /* Table groups */
                .table-group { margin-bottom: var(--spacing-md); border: 1px solid var(--color-border); border-radius: var(--border-radius-lg); overflow: hidden; }

                .table-group-header {
                    font-size: 13px;
                    font-weight: 600;
                    background: var(--color-section-bg);
                    padding: var(--spacing-sm) var(--spacing-md);
                    border-bottom: 1px solid var(--color-border);
                    font-family: 'Courier New', Courier, monospace;
                    letter-spacing: 0.3px;
                    cursor: pointer;
                    user-select: none;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                }

                .table-group-header::after {
                    content: '▼';
                    font-size: 10px;
                    color: var(--color-text-muted);
                    flex-shrink: 0;
                    margin-left: var(--spacing-sm);
                    font-family: sans-serif;
                }

                .table-group-header.collapsed::after { content: '►'; }
                .table-group-header.collapsed + .table-group-body { display: none; }

                .table-group-body .diff-item { margin-bottom: 0; border-radius: 0; border-left-width: 3px; }
                .table-group-body .diff-item + .diff-item { border-top: 1px solid rgba(0, 0, 0, 0.06); }

                /* Table name list (Tables Only tabs) */
                .table-name-list { list-style: none; }

                .table-name-item {
                    padding: var(--spacing-sm) var(--spacing-md);
                    border-left: 4px solid var(--color-border);
                    margin-bottom: var(--spacing-sm);
                    border-radius: 0 var(--border-radius) var(--border-radius) 0;
                    background: var(--color-section-bg);
                    font-family: 'Courier New', Courier, monospace;
                    font-size: 13px;
                }

                /* Empty state */
                .no-diff {
                    color: var(--color-text-muted);
                    font-style: italic;
                    padding: var(--spacing-md);
                    border: 1px dashed var(--color-border);
                    border-radius: var(--border-radius);
                    text-align: center;
                    background: var(--color-section-bg);
                }

                /* Column sub-groups */
                .column-group { border-bottom: 1px solid rgba(0,0,0,0.06); }
                .column-group:last-child { border-bottom: none; }
                .column-group-header {
                    font-size: 11px;
                    font-weight: 700;
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                    padding: var(--spacing-xs) var(--spacing-md);
                    background: rgba(0,0,0,0.04);
                    color: var(--color-text-muted);
                    font-family: 'Courier New', Courier, monospace;
                    border-bottom: 1px solid var(--color-border);
                }

                /* Suggestion toggle */
                .diff-content-row {
                    display: flex;
                    align-items: center;
                    gap: var(--spacing-sm);
                    margin-bottom: var(--spacing-xs);
                }
                .diff-content-row .diff-message { flex: 1; margin-bottom: 0; }
                .suggestion-toggle-btn {
                    flex-shrink: 0;
                    padding: 3px 10px;
                    font-size: 11px;
                    font-weight: 600;
                    border: 1px solid var(--color-border);
                    border-radius: var(--border-radius);
                    background: var(--color-bg);
                    color: var(--color-text-muted);
                    cursor: pointer;
                    font-family: var(--font-family);
                }
                .suggestion-toggle-btn:hover { background: var(--color-section-bg); color: var(--color-text); }
                .suggestion-panel {
                    display: none;
                    margin-top: var(--spacing-sm);
                    border: 1px solid var(--color-border);
                    border-radius: var(--border-radius);
                    background: #1e1e1e;
                    overflow: hidden;
                }
                .suggestion-panel.open { display: block; }
                .suggestion-panel-header {
                    display: flex;
                    align-items: center;
                    justify-content: space-between;
                    padding: var(--spacing-xs) var(--spacing-sm);
                    background: #2d2d2d;
                    border-bottom: 1px solid #444;
                    font-size: 11px;
                    color: #aaa;
                    font-family: 'Courier New', Courier, monospace;
                }
                .suggestion-actions { display: flex; gap: var(--spacing-xs); }
                .suggestion-action-btn {
                    padding: 2px 8px;
                    font-size: 11px;
                    border: 1px solid #555;
                    border-radius: 3px;
                    background: #3a3a3a;
                    color: #ddd;
                    cursor: pointer;
                    font-family: var(--font-family);
                }
                .suggestion-action-btn:hover { background: #4a4a4a; color: #fff; }
                .suggestion-code {
                    padding: var(--spacing-sm) var(--spacing-md);
                    font-family: 'Courier New', Courier, monospace;
                    font-size: 12px;
                    color: #d4d4d4;
                    white-space: pre-wrap;
                    word-break: break-all;
                    margin: 0;
                }
            </style>
            <script>
                function showTab(id) {
                    document.querySelectorAll('.tab-panel').forEach(function(p) { p.classList.remove('active'); });
                    var btns = document.querySelectorAll('.tab-btn');
                    btns.forEach(function(b) { b.classList.remove('active'); });
                    document.getElementById(id).classList.add('active');
                    btns.forEach(function(b) { if (b.getAttribute('data-tab') === id) b.classList.add('active'); });
                }

                function filterByName(inputId, containerId) {
                    var filter = document.getElementById(inputId).value.trim().toUpperCase();
                    document.getElementById(containerId).querySelectorAll('[data-table-name]').forEach(function(el) {
                        el.style.display = el.getAttribute('data-table-name').toUpperCase().indexOf(filter) !== -1 ? '' : 'none';
                    });
                }

                function toggleGroup(header) {
                    header.classList.toggle('collapsed');
                }
                function toggleSuggestion(id) {
                    document.getElementById('sug-' + id).classList.toggle('open');
                }
                function copySuggestion(id) {
                    var code = document.getElementById('sug-code-' + id).textContent;
                    var btn = event.target;
                    navigator.clipboard.writeText(code).then(function() {
                        var orig = btn.textContent;
                        btn.textContent = 'Copiado!';
                        setTimeout(function() { btn.textContent = orig; }, 1500);
                    });
                }
                function downloadSuggestion(id, filename) {
                    var code = document.getElementById('sug-code-' + id).textContent;
                    var blob = new Blob([code], { type: 'text/plain' });
                    var a = document.createElement('a');
                    a.href = URL.createObjectURL(blob);
                    a.download = filename;
                    document.body.appendChild(a);
                    a.click();
                    document.body.removeChild(a);
                    URL.revokeObjectURL(a.href);
                }
            </script>
        </head>
        <body>

        <header id="header">
            <h1>Oracle Migration Preflight Report</h1>
            <p class="meta">Generated: <strong>{{ generated_at }}</strong></p>
            <p class="meta">Reference Schema: <strong>{{ reference_schema }}</strong></p>
            <p class="meta">Target Schema: <strong>{{ target_schema }}</strong></p>
        </header>

        <main>
            <div class="tab-bar">
                <button class="tab-btn active" data-tab="summary" onclick="showTab('summary')">Summary</button>
                <button class="tab-btn" data-tab="by-severity" onclick="showTab('by-severity')">Differences by Severity</button>
                <button class="tab-btn" data-tab="by-table" onclick="showTab('by-table')">Differences by Table</button>
                <button class="tab-btn" data-tab="only-reference" onclick="showTab('only-reference')">Tables Only in Reference</button>
                <button class="tab-btn" data-tab="only-target" onclick="showTab('only-target')">Tables Only in Target</button>
            </div>

            <div id="summary" class="tab-panel active">
                <h2 class="panel-title">Summary</h2>
                <div class="summary-grid">
                    <div class="summary-card">
                        <div class="value">{{ summary.total_tables_in_reference }}</div>
                        <div class="label">Tables in Reference</div>
                    </div>
                    <div class="summary-card">
                        <div class="value">{{ summary.total_tables_in_target }}</div>
                        <div class="label">Tables in Target</div>
                    </div>
                    <div class="summary-card">
                        <div class="value">{{ summary.total_differences }}</div>
                        <div class="label">Total Differences</div>
                    </div>
                    <div class="summary-card critical">
                        <div class="value">{{ summary.critical_count }}</div>
                        <div class="label">Critical</div>
                    </div>
                    <div class="summary-card warning">
                        <div class="value">{{ summary.warning_count }}</div>
                        <div class="label">Warning</div>
                    </div>

                </div>
                <h2 class="panel-title">Readiness Status</h2>
                {{- if summary.readiness_status == "NOT READY" }}
                <span class="status-badge not-ready">{{ summary.readiness_status }}</span>
                {{- else if summary.readiness_status == "NEEDS REVIEW" }}
                <span class="status-badge needs-review">{{ summary.readiness_status }}</span>
                {{- else }}
                <span class="status-badge ready">{{ summary.readiness_status }}</span>
                {{- end }}
            </div>

            <div id="by-severity" class="tab-panel">
                <h2 class="panel-title">Differences by Severity</h2>
                {{- if !has_differences }}
                <p class="no-diff">No differences found.</p>
                {{- else }}
                {{- if critical_diffs.size > 0 }}
                <div class="severity-group">
                    <span class="severity-group-title critical" onclick="toggleGroup(this)">Critical &mdash; {{ critical_diffs.size }}</span>
                    <ul class="diff-list">
                        {{- for d in critical_diffs }}
                        <li class="diff-item critical">
                            <div class="diff-header">
                                <span class="diff-badge critical">{{ d.severity }}</span>
                                <span>{{ d.type }} &mdash; {{ d.table_name }}{{ if d.column_name != "" }}.{{ d.column_name }}{{ end }}</span>
                            </div>
                            <div class="diff-content-row">
                                <div class="diff-message">{{ d.message }}</div>
                                {{- if d.suggestion_sql != "" }}
                                <button class="suggestion-toggle-btn" onclick="toggleSuggestion('{{ d.suggestion_id }}')">Ver sugestão SQL ▾</button>
                                {{- end }}
                            </div>
                            {{- if d.reference_value != "" || d.target_value != "" }}
                            <div class="diff-meta">Ref: {{ d.reference_value }} &rarr; Target: {{ d.target_value }}</div>
                            {{- end }}
                            {{- if d.suggestion_sql != "" }}
                            <div id="sug-{{ d.suggestion_id }}" class="suggestion-panel">
                                <div class="suggestion-panel-header">
                                    <span>Sugestão — revise antes de executar manualmente</span>
                                    <div class="suggestion-actions">
                                        <button class="suggestion-action-btn" onclick="copySuggestion('{{ d.suggestion_id }}')">Copiar</button>
                                        <button class="suggestion-action-btn" onclick="downloadSuggestion('{{ d.suggestion_id }}', '{{ d.table_name }}{{ if d.column_name != "" }}_{{ d.column_name }}{{ end }}.sql')">Baixar</button>
                                    </div>
                                </div>
                                <pre id="sug-code-{{ d.suggestion_id }}" class="suggestion-code">{{ d.suggestion_sql }}</pre>
                            </div>
                            {{- end }}
                        </li>
                        {{- end }}
                    </ul>
                </div>
                {{- end }}
                {{- if warning_diffs.size > 0 }}
                <div class="severity-group">
                    <span class="severity-group-title warning" onclick="toggleGroup(this)">Warning &mdash; {{ warning_diffs.size }}</span>
                    <ul class="diff-list">
                        {{- for d in warning_diffs }}
                        <li class="diff-item warning">
                            <div class="diff-header">
                                <span class="diff-badge warning">{{ d.severity }}</span>
                                <span>{{ d.type }} &mdash; {{ d.table_name }}{{ if d.column_name != "" }}.{{ d.column_name }}{{ end }}</span>
                            </div>
                            <div class="diff-content-row">
                                <div class="diff-message">{{ d.message }}</div>
                                {{- if d.suggestion_sql != "" }}
                                <button class="suggestion-toggle-btn" onclick="toggleSuggestion('{{ d.suggestion_id }}')">Ver sugestão SQL ▾</button>
                                {{- end }}
                            </div>
                            {{- if d.reference_value != "" || d.target_value != "" }}
                            <div class="diff-meta">Ref: {{ d.reference_value }} &rarr; Target: {{ d.target_value }}</div>
                            {{- end }}
                            {{- if d.suggestion_sql != "" }}
                            <div id="sug-{{ d.suggestion_id }}" class="suggestion-panel">
                                <div class="suggestion-panel-header">
                                    <span>Sugestão — revise antes de executar manualmente</span>
                                    <div class="suggestion-actions">
                                        <button class="suggestion-action-btn" onclick="copySuggestion('{{ d.suggestion_id }}')">Copiar</button>
                                        <button class="suggestion-action-btn" onclick="downloadSuggestion('{{ d.suggestion_id }}', '{{ d.table_name }}{{ if d.column_name != "" }}_{{ d.column_name }}{{ end }}.sql')">Baixar</button>
                                    </div>
                                </div>
                                <pre id="sug-code-{{ d.suggestion_id }}" class="suggestion-code">{{ d.suggestion_sql }}</pre>
                            </div>
                            {{- end }}
                        </li>
                        {{- end }}
                    </ul>
                </div>
                {{- end }}
                {{- if info_diffs.size > 0 }}
                <div class="severity-group">
                    <span class="severity-group-title info">Info &mdash; {{ info_diffs.size }}</span>
                    <ul class="diff-list">
                        {{- for d in info_diffs }}
                        <li class="diff-item info">
                            <div class="diff-header">
                                <span class="diff-badge info">{{ d.severity }}</span>
                                <span>{{ d.type }} &mdash; {{ d.table_name }}{{ if d.column_name != "" }}.{{ d.column_name }}{{ end }}</span>
                            </div>
                            <div class="diff-content-row">
                                <div class="diff-message">{{ d.message }}</div>
                                {{- if d.suggestion_sql != "" }}
                                <button class="suggestion-toggle-btn" onclick="toggleSuggestion('{{ d.suggestion_id }}')">Ver sugestão SQL ▾</button>
                                {{- end }}
                            </div>
                            {{- if d.reference_value != "" || d.target_value != "" }}
                            <div class="diff-meta">Ref: {{ d.reference_value }} &rarr; Target: {{ d.target_value }}</div>
                            {{- end }}
                            {{- if d.suggestion_sql != "" }}
                            <div id="sug-{{ d.suggestion_id }}" class="suggestion-panel">
                                <div class="suggestion-panel-header">
                                    <span>Sugestão — revise antes de executar manualmente</span>
                                    <div class="suggestion-actions">
                                        <button class="suggestion-action-btn" onclick="copySuggestion('{{ d.suggestion_id }}')">Copiar</button>
                                        <button class="suggestion-action-btn" onclick="downloadSuggestion('{{ d.suggestion_id }}', '{{ d.table_name }}{{ if d.column_name != "" }}_{{ d.column_name }}{{ end }}.sql')">Baixar</button>
                                    </div>
                                </div>
                                <pre id="sug-code-{{ d.suggestion_id }}" class="suggestion-code">{{ d.suggestion_sql }}</pre>
                            </div>
                            {{- end }}
                        </li>
                        {{- end }}
                    </ul>
                </div>
                {{- end }}
                {{- end }}
            </div>

            <div id="by-table" class="tab-panel">
                <h2 class="panel-title">Differences by Table</h2>
                {{- if !has_differences }}
                <p class="no-diff">All tables are structurally equal.</p>
                {{- else }}
                <div class="filter-bar">
                    <input id="filter-by-table" class="filter-input" type="text" placeholder="Filter by table name..." oninput="filterByName('filter-by-table', 'by-table-list')" />
                </div>
                <div id="by-table-list">
                    {{- for group in table_groups }}
                    <div class="table-group" data-table-name="{{ group.table_name }}">
                        <div class="table-group-header" onclick="toggleGroup(this)">
                            {{ group.table_name }} &mdash; {{ group.diff_count }} difference{{ if group.diff_count != 1 }}s{{ end }}
                        </div>
                        <div class="table-group-body">
                            {{- for col_group in group.column_groups }}
                            <div class="column-group">
                                {{- if col_group.column_name != "" }}
                                <div class="column-group-header">{{ col_group.column_name }}</div>
                                {{- end }}
                                {{- for d in col_group.diffs }}
                                <div class="diff-item {{ d.severity | string.downcase }}">
                                    <div class="diff-header">
                                        <span class="diff-badge {{ d.severity | string.downcase }}">{{ d.severity }}</span>
                                        <span>{{ d.type }}{{ if d.column_name != "" }} &mdash; {{ d.column_name }}{{ end }}</span>
                                    </div>
                                    <div class="diff-content-row">
                                        <div class="diff-message">{{ d.message }}</div>
                                        {{- if d.suggestion_sql != "" }}
                                        <button class="suggestion-toggle-btn" onclick="toggleSuggestion('{{ d.suggestion_id }}')">Ver sugestão SQL ▾</button>
                                        {{- end }}
                                    </div>
                                    {{- if d.reference_value != "" || d.target_value != "" }}
                                    <div class="diff-meta">Ref: {{ d.reference_value }} &rarr; Target: {{ d.target_value }}</div>
                                    {{- end }}
                                    {{- if d.suggestion_sql != "" }}
                                    <div id="sug-{{ d.suggestion_id }}" class="suggestion-panel">
                                        <div class="suggestion-panel-header">
                                            <span>Sugestão — revise antes de executar manualmente</span>
                                            <div class="suggestion-actions">
                                                <button class="suggestion-action-btn" onclick="copySuggestion('{{ d.suggestion_id }}')">Copiar</button>
                                                <button class="suggestion-action-btn" onclick="downloadSuggestion('{{ d.suggestion_id }}', '{{ d.table_name }}{{ if d.column_name != "" }}_{{ d.column_name }}{{ end }}.sql')">Baixar</button>
                                            </div>
                                        </div>
                                        <pre id="sug-code-{{ d.suggestion_id }}" class="suggestion-code">{{ d.suggestion_sql }}</pre>
                                    </div>
                                    {{- end }}
                                </div>
                                {{- end }}
                            </div>
                            {{- end }}
                        </div>
                    </div>
                    {{- end }}
                </div>
                {{- end }}
            </div>

            <div id="only-reference" class="tab-panel">
                <h2 class="panel-title">Tables Only in Reference</h2>
                {{- if missing_tables.size == 0 }}
                <p class="no-diff">No tables found exclusively in the reference schema.</p>
                {{- else }}
                <div class="filter-bar">
                    <input id="filter-only-reference" class="filter-input" type="text" placeholder="Filter by table name..." oninput="filterByName('filter-only-reference', 'only-reference-list')" />
                </div>
                <ul id="only-reference-list" class="table-name-list">
                    {{- for t in missing_tables }}
                    <li class="table-name-item" data-table-name="{{ t }}">{{ t }}</li>
                    {{- end }}
                </ul>
                {{- end }}
            </div>

            <div id="only-target" class="tab-panel">
                <h2 class="panel-title">Tables Only in Target</h2>
                {{- if extra_tables.size == 0 }}
                <p class="no-diff">No tables found exclusively in the target schema.</p>
                {{- else }}
                <div class="filter-bar">
                    <input id="filter-only-target" class="filter-input" type="text" placeholder="Filter by table name..." oninput="filterByName('filter-only-target', 'only-target-list')" />
                </div>
                <ul id="only-target-list" class="table-name-list">
                    {{- for t in extra_tables }}
                    <li class="table-name-item" data-table-name="{{ t }}">{{ t }}</li>
                    {{- end }}
                </ul>
                {{- end }}
            </div>
        </main>

        </body>
        </html>
        """;
}
