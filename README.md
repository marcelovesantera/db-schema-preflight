# Database Schema Preflight

A .NET 8 CLI tool that compares two Oracle database schemas, generates an HTML report of structural differences, and optionally produces an Oracle DDL/DML script with suggestions to align the target schema. It does **not** execute scripts or modify any database.

## About

The tool connects to two Oracle schemas (reference and target), extracts table and column metadata, computes structural differences, and produces a self-contained HTML report with a readiness status:

- **READY** — no differences found.
- **NEEDS REVIEW** — only warnings (e.g. nullable mismatch, scale difference).
- **NOT READY** — at least one critical difference (e.g. missing table, missing column, type mismatch).

For each difference the report also includes an Oracle SQL suggestion (`CREATE TABLE`, `ALTER TABLE ADD/MODIFY`, or commented-out `DROP`) with a risk level (Low / Medium / High) and inline warnings for destructive operations. With `exportSql: true`, all suggestions are written to a `.sql` file ordered for safe execution.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Read access to both Oracle schemas (`ALL_TABLES`, `ALL_TAB_COLUMNS`)
- Oracle 12c or later (tested with Oracle 19c and Oracle XE 21c)

## Installation

### As a global tool (recommended)

1. Download the `.nupkg` file from the desired release.
2. Install globally:

   ```bash
   dotnet tool install --global DbSchemaPreflight.Cli --add-source <folder-containing-nupkg>
   ```

3. Verify the installation:

   ```bash
   dbpreflight --help
   ```

To update to a new version:

```bash
dotnet tool uninstall --global DbSchemaPreflight.Cli
dotnet tool install --global DbSchemaPreflight.Cli --add-source <folder-containing-new-nupkg>
```

### As a local project (development)

```bash
dotnet run --project src/DbSchemaPreflight.Cli -- <command>
```

## Build

```bash
dotnet build
```

## Configuration

Run `dbpreflight init` in any directory to generate a `config.yaml` template:

```bash
dbpreflight init
```

Edit `config.yaml` with your connection details:

```yaml
compare-tool:
  reference:
    connectionString: "User Id=APP_REF;Password=CHANGE_ME;Data Source=localhost:1521/XEPDB1"
    schema: "APP_REF"

  target:
    connectionString: "User Id=APP_TARGET;Password=CHANGE_ME;Data Source=localhost:1521/XEPDB1"
    schema: "APP_TARGET"

  report:
    output: "./reports/schema-diff.html"
    exportSql: true   # optional — generates ./reports/schema-diff.sql alongside the HTML

analyse-script-tool:
  provider: "oracle"
  connectionString: "User Id=APP_USER;Password=CHANGE_ME;Data Source=localhost:1521/XEPDB1"
  schema: "APP_SCHEMA"
  file: "./scripts/my-script.sql"
  report:
    output: "./reports/script-analysis.html"
```

> `config.yaml` is git-ignored. Never commit real credentials.

## Usage

```bash
dbpreflight compare
dbpreflight analyse-script
```

Expected terminal output for `compare`:

```
[HH:mm:ss] Connecting to reference schema: APP_REF...
[HH:mm:ss] Connecting to target schema: APP_TARGET...
[HH:mm:ss] Comparing schemas...

Status: NOT READY
5 difference(s) found — Critical: 3 | Warning: 2 | Info: 0

Report saved to: ./reports/schema-diff.html
SQL script saved to: ./reports/schema-diff.sql
```

The `.sql` file is only written when `exportSql: true` is set in the config. All destructive statements (`DROP TABLE`, `DROP COLUMN`) are commented out and require manual review before execution.

The exit code reflects the readiness status:

| Status | Exit code |
|---|---|
| `READY` | 0 |
| `NEEDS REVIEW` | 0 |
| `NOT READY` | 1 |
| Error | 1 |

Check the exit code after running:

```bash
# Bash / PowerShell
echo $LASTEXITCODE
```

## Run Tests

```bash
# All tests
dotnet test

# Specific project
dotnet test tests/DbSchemaPreflight.Core.Tests
dotnet test tests/DbSchemaPreflight.Reporting.Tests

# Single test by name
dotnet test tests/DbSchemaPreflight.Core.Tests --filter "FullyQualifiedName~MissingTable"
```

## SQL Examples

The `examples/` directory contains DDL scripts for a realistic comparison scenario:

- `examples/reference-schema.sql` — reference schema with 5 tables.
- `examples/target-schema.sql` — target schema with intentional differences: missing table, missing column, type mismatch, scale mismatch, nullability mismatch.

Use these scripts to create test schemas in a local Oracle XE instance and validate the tool end-to-end.

## Project Structure

```
src/
  DbSchemaPreflight.Cli/         CLI command definitions, argument parsing, orchestration
  DbSchemaPreflight.Core/        Domain models, diff engine, severity classification, SQL suggestion generation
  DbSchemaPreflight.Oracle/      Oracle connection factory, metadata queries
  DbSchemaPreflight.Reporting/   HTML report model, Scriban template rendering, SQL suggestion renderer, file output
tests/
  DbSchemaPreflight.Core.Tests/        Unit tests for SchemaDiffEngine and SQL suggestion generator
  DbSchemaPreflight.Reporting.Tests/   Unit tests for ReportSummary and HtmlReportGenerator
examples/                      SQL DDL scripts for test schemas
```

## Difference Types

| Type | Severity | Trigger |
|---|---|---|
| `MissingTable` | Critical | Table in reference not found in target |
| `MissingColumn` | Critical | Column in reference not found in target |
| `DataTypeMismatch` | Critical | Column data type differs |
| `DataLengthSmaller` | Critical | Target column length is smaller than reference |
| `ExtraTable` | Info | Table in target not in reference |
| `ExtraColumn` | Warning | Column in target not in reference |
| `DataLengthLarger` | Warning | Target column length is larger than reference |
| `PrecisionMismatch` | Warning | Column precision differs |
| `ScaleMismatch` | Warning | Column scale differs |
| `NullabilityMismatch` | Warning | Column nullable flag differs |
| `DefaultValueMismatch` | Warning | Column default value differs |
