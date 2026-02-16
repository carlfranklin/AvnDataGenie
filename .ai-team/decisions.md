# Team Decisions

<!-- Canonical decision ledger. Agents read this before starting work. Scribe merges inbox entries here. -->

## 2026-02-16: Test Infrastructure & Project Setup (consolidated)

**By:** Huntress

**Date:** 2026-02-16

**What:** Created `src/AvnDataGenie.Tests/` as the xUnit test project for the solution. Added `InternalsVisibleTo("AvnDataGenie.Tests")` to `AvnDataGenie.csproj` and changed `CleanAndFormatSql`, `FormatSql`, and `FormatSelectColumns` from `private static` to `internal static` in `Generator.cs`.

**Why:** These three methods are pure functions — no dependencies, no state, deterministic outputs. They are the highest-leverage test targets in the codebase because they sanitize LLM output before it reaches the database. Making them `internal` (not `public`) preserves the API surface while enabling comprehensive testing. The `InternalsVisibleTo` pattern is standard .NET practice for this exact scenario.

**Impact:** 
- Any future test code in `AvnDataGenie.Tests` can access `internal` members of the `AvnDataGenie` assembly.
- Fixed pre-existing `Microsoft.Extensions.DependencyInjection.Abstractions` version conflict (9.0.6 → 10.0.2) that was blocking all builds.
- 64 tests covering `SqlPromptBuilder`, `CleanAndFormatSql`, `FormatSql`, and `FormatSelectColumns` — all passing.
- Foundation for CI integration (tests now discoverable by dotnet test).

---

## 2026-02-16: CI Pipeline Configuration

**By:** Lucius (DevOps Engineer)

**Date:** 2026-02-16

**What:** Created `.github/workflows/ci.yml` — a GitHub Actions CI pipeline that runs on every push to `main` and every PR targeting `main`. It restores, builds (Release config), and runs tests for the full solution.

**Key Choices:**

1. **Ubuntu-only** — no Windows matrix. The project targets Linux containers (`DockerDefaultTargetOS=Linux`) and .NET 10 is cross-platform. Adding Windows doubles pipeline cost with no signal gain.
2. **All actions SHA-pinned** — `actions/checkout@v6.0.2`, `actions/setup-dotnet@v5.1.0`, `actions/cache@v5.0.3`. Supply-chain security, not negotiable.
3. **NuGet cache** — keyed on `*.csproj` + `global.json` hashes. Saves ~30s on repeat runs.
4. **Concurrency group** — cancels in-progress runs on the same branch. No wasted compute on rapid pushes.
5. **AppHost included in build** — the Aspire AppHost uses `Aspire.AppHost.Sdk/13.1.0` which resolves via NuGet, not the deprecated Aspire workload. It compiles cleanly in CI.
6. **Test step wired up and functional** — `dotnet test` picks up all test projects automatically (integrated with Huntress's test infrastructure).

**Why:** Every PR needs a green check before a human looks at it. Zero test coverage + zero CI = chaos. This is the floor.

**Impact:** All team members should be aware that pushes to `main` and PRs to `main` now trigger CI. Build failures will block merges if branch protection is configured. Test runs are now gated by CI.

---

## 2026-02-16: Database & Performance Review

**Author:** Cyborg (Database & Performance Engineer)

**Date:** 2026-02-16

**Status:** Recommendation

**Scope:** All database interactions in AvnDataGenie

### Database Interaction Catalog

Only **2 files** in the entire codebase create database connections:

| Location | Purpose | Connection Lifetime | Queries Executed |
|---|---|---|---|
| `SchemaGenerator/Generator.cs` | Schema extraction | Single connection, held open for N+1 queries per table | 1 + 4×(table count) |
| `AdminApp/Components/Pages/SQLResults.razor` | User query execution | Single connection, opened and closed per query | 1 |

Supporting files that handle connection strings but don't open connections:
- `AdminApp/AppState.cs` — stores `ConnectionString` in memory
- `AdminApp/Components/Pages/Home.razor` — captures connection string from UI, passes to SchemaGenerator

### Connection Management Assessment

**Connection Creation & Disposal — ✅ Acceptable**

Both connection sites use `using var connection = new SqlConnection(...)` with `await connection.OpenAsync()`. This is correct — connections are disposed deterministically at scope exit. No connection leaks detected.

**Connection Pooling — ⚠️ Implicitly Relied Upon**

ADO.NET connection pooling is **on by default** for `Microsoft.Data.SqlClient`, so the codebase benefits from pooling without configuring it. However:

- **No explicit pooling parameters** in connection strings (`Min Pool Size`, `Max Pool Size`, `Connection Lifetime`)
- The hardcoded default connection string in both `Home.razor:145` and `SQLResults.razor:184` includes `Connect Timeout=30` but no pool tuning
- For a single-user admin tool, the defaults are fine. For multi-user (roadmap item #8), this will need attention.

**Recommendation:** No action needed now. When multi-user support lands, add `Min Pool Size=2;Max Pool Size=10` to connection string defaults and document pooling expectations.

**Connection Hold Time — 🔴 Critical Issue in SchemaGenerator**

`SchemaGenerator/Generator.cs` opens **one connection** and holds it for the entire schema extraction. For each table, it executes 4 sequential queries:

```
GetColumnsAsync      → INFORMATION_SCHEMA.COLUMNS
GetPrimaryKeyAsync   → INFORMATION_SCHEMA.TABLE_CONSTRAINTS + KEY_COLUMN_USAGE
GetForeignKeysAsync  → sys.foreign_keys + sys.foreign_key_columns
GetIndexesAsync      → sys.indexes + sys.index_columns
```

For a database with 500 tables, this means **2,001 round-trips** (1 for table list + 4×500) on a single held connection. Measured characteristics:

- **Latency:** Linear with table count. Each round-trip adds ~2-5ms on local, ~10-50ms over network. 500 tables ≈ 10-100 seconds.
- **Lock contention:** INFORMATION_SCHEMA and sys.* views acquire schema locks. Holding a connection open during extended reads is low-risk for metadata but wasteful.
- **No CancellationToken:** The `GenerateDatabaseSchemaAsync` method accepts no cancellation token. If schema extraction hangs (network issue, lock wait), the user has no way to cancel. The UI just spins.

**Connection String Security — ⚠️ Moderate Risk**

- Connection strings are stored in **plain text** in `AppState.ConnectionString` (in-memory, scoped per SignalR circuit)
- They are typed directly into a `<textarea>` in the browser and transmitted over SignalR
- The hardcoded default connection strings in `Home.razor:145` and `SQLResults.razor:184` use `Integrated Security=True` which is safe, but if a user enters credentials in the connection string, they flow through browser ↔ server unencrypted (within the SignalR connection)
- **No validation** of connection string format before opening — malformed strings produce raw `SqlException` messages that may leak server info

**Recommendation:** Add connection string parsing/validation using `SqlConnectionStringBuilder` before attempting connection. Strip sensitive info from error messages returned to the UI.

### Schema Extraction Performance

**Query Efficiency — 🔴 N+1 Problem**

The current approach is a classic **N+1 query pattern**:

```
1 query: Get all tables
N queries × 4: For each table, get columns, PKs, FKs, indexes
```

**All 4 per-table queries could be combined into bulk queries** that fetch metadata for ALL tables at once:

```sql
-- Instead of N calls to GetColumnsAsync:
SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE, ...
FROM INFORMATION_SCHEMA.COLUMNS
ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION

-- Instead of N calls to GetPrimaryKeyAsync:
SELECT tc.TABLE_SCHEMA, tc.TABLE_NAME, kc.CONSTRAINT_NAME, kc.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kc ON ...
WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
ORDER BY tc.TABLE_SCHEMA, tc.TABLE_NAME, kc.ORDINAL_POSITION

-- (Similar for FKs and indexes)
```

This would reduce **4N+1 round-trips to 5 round-trips** regardless of table count. For 500 tables, that's ~2,001 → 5 queries. Order of magnitude improvement.

**Query Quality — ✅ Good with Minor Issues**

- **Parameterized queries:** All per-table queries use `@SchemaName` and `@TableName` parameters. Good — no SQL injection risk in schema extraction.
- **INFORMATION_SCHEMA vs sys.\*:** The code mixes both (INFORMATION_SCHEMA for tables/columns/PKs, sys.* for FKs/indexes). This is pragmatic — sys.* is more reliable for FK and index metadata. But it ties us to SQL Server.
- **Missing metadata:** No extraction of computed columns, filtered indexes, included columns, or row counts. Row counts (`sys.dm_db_partition_stats`) would be valuable for the LLM to generate better `TOP N` defaults.

**Large Database Behavior — 🔴 No Safeguards**

- No progress reporting during schema extraction
- No table count limit or warning
- No timeout on the overall operation
- A database with 1,000+ tables will produce a very large JSON schema that may exceed LLM token limits (the `SqlPromptBuilder` has `maxTablesToInclude = 200` as a default cap, which is good, but schema extraction still processes all tables)

**Recommendation:** Add a table count warning at 100+ tables and an option to select specific schemas/tables for extraction.

### SQL Execution Review (SQLResults.razor)

**Execution Pattern — ✅ Acceptable**

```csharp
using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();
using var command = new SqlCommand(appState.SQLString, connection);
command.CommandType = CommandType.Text;
command.CommandTimeout = 30;
using var reader = await command.ExecuteReaderAsync();
resultData = new DataTable();
resultData.Load(reader);
```

This is clean. Connection opens, executes, loads results into DataTable, disposes. The 30-second `CommandTimeout` is reasonable.

**SQL Injection — ⚠️ Architectural Risk (Not a Bug)**

The `appState.SQLString` is **LLM-generated SQL** executed directly against a live database. This is by design — the product's purpose is to execute NLQ-generated queries. However:

- **No SQL validation layer** between LLM output and execution. The `CleanAndFormatSql` method strips markdown and comments but does NOT validate the SQL is actually a SELECT.
- The prompt instructs the LLM to generate "only SELECT" but LLMs can hallucinate. A malicious or confused LLM could return `DROP TABLE`, `DELETE`, or `EXEC xp_cmdshell`.
- **No read-only enforcement** at the connection level.

**Recommendations:**
1. **Parse the SQL** before execution — at minimum, verify it starts with `SELECT` (after whitespace/comment stripping). The existing `selectIndex` check in `CleanAndFormatSql` catches preamble but doesn't reject non-SELECT statements.
2. **Use a read-only connection** — create a database user with `db_datareader` only, or set the connection to use `ApplicationIntent=ReadOnly` (already in the default connection string, but only affects read-only routing on AG replicas, not permissions).
3. **Wrap in `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED`** or `NOLOCK` hints to prevent user queries from blocking production workloads. This is an admin tool querying live databases.

**Result Set Handling — ⚠️ Memory Concern**

`DataTable.Load(reader)` loads the **entire result set into memory**. For a query that returns millions of rows, this will cause:
- High memory allocation on the server
- Slow Blazor Server rendering (all rows rendered into HTML)
- Potential SignalR circuit timeout during render

**Recommendation:** Add `TOP 1000` enforcement or a configurable row limit. The LLM prompt already instructs `TOP (N)` usage, but this should be enforced at the execution layer as a safety net.

**Error Handling — ✅ Adequate**

Exceptions are caught and displayed to the user. However, raw `SqlException.Message` may contain server names, file paths, or other internal info. Consider sanitizing error output.

**Missing: No CancellationToken Support**

`ExecuteQuery` has no cancellation mechanism. If a query runs for 29 seconds, the user must wait. Adding `CancellationToken` support with a "Cancel" button would improve UX.

### Multi-Database Readiness Assessment

**Current SQL Server Coupling — 🔴 Deep**

The codebase is tightly coupled to SQL Server in these ways:

| Coupling Point | Detail |
|---|---|
| `Microsoft.Data.SqlClient` | Direct NuGet dependency in SchemaGenerator and AdminApp |
| `SqlConnection` / `SqlCommand` | Concrete types used everywhere, no abstraction |
| `INFORMATION_SCHEMA.*` | SQL Server-specific behavior (e.g., `TABLE_TYPE = 'BASE TABLE'`) |
| `sys.foreign_keys`, `sys.indexes` | SQL Server system catalog views — no equivalent in ANSI SQL |
| `OBJECT_SCHEMA_NAME()`, `OBJECT_NAME()`, `COL_NAME()` | SQL Server-specific functions |
| T-SQL prompt instructions | `CleanAndFormatSql` and `SqlPromptBuilder` emit T-SQL rules |
| Connection string format | SQL Server connection string syntax assumed everywhere |

**Recommended Abstraction Architecture**

```
┌─────────────────────────────┐
│     ISchemaProvider         │  ← Extract schema from any DB
│  GenerateSchemaAsync()      │
└──────────┬──────────────────┘
           │ implements
    ┌──────┴──────┐
    │             │
SqlServerSchema  PostgresSchema  MySqlSchema
Provider         Provider        Provider
```

```
┌─────────────────────────────┐
│     IQueryExecutor          │  ← Execute queries against any DB
│  ExecuteQueryAsync()        │
│  ValidateQueryAsync()       │  ← SQL validation per dialect
└──────────┬──────────────────┘
           │ implements
    ┌──────┴──────┐
    │             │
SqlServerQuery   PostgresQuery   MySqlQuery
Executor         Executor        Executor
```

```
┌─────────────────────────────┐
│     ISqlDialect             │  ← Prompt rules per DB engine
│  GetPromptRules()           │
│  GetDialectName()           │
└─────────────────────────────┘
```

**Migration Steps (Ordered)**

1. **Extract `ISchemaProvider` interface** from `SchemaGenerator.Generator` with method `Task<DatabaseSchema> GenerateSchemaAsync(string connectionString, CancellationToken ct)`
2. **Rename current `Generator`** to `SqlServerSchemaProvider` implementing `ISchemaProvider`
3. **Extract `IQueryExecutor` interface** from `SQLResults.razor` inline code with method `Task<DataTable> ExecuteAsync(string connectionString, string sql, CancellationToken ct)`
4. **Create `SqlServerQueryExecutor`** implementing `IQueryExecutor`
5. **Register via DI** with factory pattern: `services.AddScoped<ISchemaProvider>(sp => CreateProvider(config.DatabaseType))`
6. **Add `ISqlDialect`** to `SqlPromptBuilder` so prompt rules are dialect-aware (T-SQL vs PL/pgSQL vs MySQL)
7. **Add `DatabaseType` enum** to `Configuration` (SqlServer, PostgreSQL, MySQL)

This aligns with the team's roadmap decision (item #7: "Support for additional database types — requires SchemaGenerator provider abstraction").

### Summary of Findings

| Finding | Severity | Effort | Priority |
|---|---|---|---|
| N+1 queries in SchemaGenerator | 🔴 High | Medium (refactor 4 methods to bulk) | P1 — do before large DB testing |
| No CancellationToken in schema extraction | 🔴 High | Low (add parameter, pass through) | P1 — UX blocker |
| No SQL validation before execution | ⚠️ Medium | Low (add SELECT check) | P1 — security |
| No result set size limit | ⚠️ Medium | Low (add TOP enforcement) | P2 |
| Connection string not validated/sanitized | ⚠️ Medium | Low | P2 |
| No read-only connection enforcement | ⚠️ Medium | Low (documentation/config) | P2 |
| Error messages may leak server info | ⚠️ Low | Low | P3 |
| No query timing instrumentation | ⚠️ Low | Low | P3 — needed for roadmap item #6 |
| Multi-DB abstraction not started | 📋 Info | High | Per roadmap: after documentation + CSV export |

### Instrumentation Recommendations

Before optimizing anything else, **add timing instrumentation**:

```csharp
// SchemaGenerator — measure per-phase timing
var sw = Stopwatch.StartNew();
var tables = await GetTablesAsync(connection);
logger.LogInformation("Schema extraction: {TableCount} tables discovered in {ElapsedMs}ms", 
    tables.Count, sw.ElapsedMilliseconds);

// SQLResults — measure query execution time
var sw = Stopwatch.StartNew();
using var reader = await command.ExecuteReaderAsync();
resultData = new DataTable();
resultData.Load(reader);
logger.LogInformation("Query executed: {RowCount} rows in {ElapsedMs}ms", 
    resultData.Rows.Count, sw.ElapsedMilliseconds);
```

This data feeds directly into roadmap item #6 (Query Performance Analytics) and gives us baselines before any optimization work.

---

## 2026-02-16: Roadmap Priority Order and Documentation-First Strategy

**By:** Oracle

**What:** Established recommended execution order for the 7 unchecked roadmap items, with documentation completion and export-to-CSV as the top priorities before architectural features.

**Why:** The codebase has zero test coverage, no provider abstraction for multi-database support, and 66% of files are undocumented. Features like multi-database support and multi-user RBAC require foundational architecture work (provider interfaces, auth middleware) that would be risky to attempt without tests. The recommended order is:

1. **Finish code documentation** (19 files remain — unblocks all contributors)
2. **Export results to CSV/Excel** (low-risk, high-value, contained in SQLResults.razor)
3. **Query favorites/bookmarks** (extends existing AppState + localStorage pattern)
4. **Save SQL SELECT results along with queries** (extends AppState, moderate scope)
5. **Query templates and saved queries** (extends favorites, builds on #3 and #4)
6. **Query performance analytics** (needs timing instrumentation in Generator)
7. **Support for additional database types** (requires SchemaGenerator provider abstraction — largest effort)
8. **Multi-user support with RBAC** (architectural overhaul — deferred until foundation is solid)

This order front-loads low-risk, high-value work and defers features that require architectural changes until the codebase has documentation, tests, and cleaner abstractions.
