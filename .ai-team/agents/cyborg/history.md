# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** AvnDataGenie — OSS natural language database querying system with schema generation, metadata management, and LLM-powered SQL generation.
- **Stack:** C#, .NET 10, Blazor Server, .NET Aspire, SQL Server, Ollama/OpenAI/Azure OpenAI/GitHub Copilot CLI
- **Created:** 2026-02-16

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-02-16: Initial Database Performance Audit

- **Only 2 files create DB connections:** `SchemaGenerator/Generator.cs` and `AdminApp/Components/Pages/SQLResults.razor`. All connections use `using var` + `OpenAsync()` — no connection leaks.
- **SchemaGenerator has an N+1 query problem:** 1 query for table list, then 4 queries per table (columns, PKs, FKs, indexes). For 500 tables = 2,001 round-trips. All 4 per-table queries can be refactored to bulk queries (5 total round-trips).
- **No CancellationToken** in `GenerateDatabaseSchemaAsync` — users cannot cancel long schema extractions.
- **SQLResults.razor executes LLM-generated SQL directly** with `CommandTimeout = 30`. No SQL validation layer (no check that output is actually a SELECT). No result set row limit. `DataTable.Load()` pulls full result set into memory.
- **Connection pooling is implicitly on** (ADO.NET default) but no explicit pool configuration. Fine for single-user admin tool, needs tuning for multi-user.
- **Multi-DB support requires 3 interfaces:** `ISchemaProvider`, `IQueryExecutor`, `ISqlDialect`. Current code is deeply coupled to SQL Server via `Microsoft.Data.SqlClient`, `INFORMATION_SCHEMA`, `sys.*` views, and T-SQL-specific functions.
- **Connection strings stored in plain text** in `AppState` (in-memory, scoped per circuit). Hardcoded defaults in both Home.razor and SQLResults.razor use `Integrated Security=True`.
- **No timing instrumentation** anywhere in the DB path — needed before optimization work and for roadmap item #6 (Query Performance Analytics).
