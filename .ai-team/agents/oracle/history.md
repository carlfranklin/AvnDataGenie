# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** AvnDataGenie — OSS natural language database querying system with schema generation, metadata management, and LLM-powered SQL generation.
- **Stack:** C#, .NET 10, Blazor Server, .NET Aspire, SQL Server, Ollama/OpenAI/Azure OpenAI/GitHub Copilot CLI
- **Created:** 2026-02-16

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **Architecture is a 4-project Aspire solution:** `AppHost` (orchestrator) → `AdminApp` (Blazor Server UI) → `AvnDataGenie` (NLQ→SQL generation core) → `SchemaGenerator` (DB metadata extraction). `ServiceDefaults` provides standard Aspire wiring (OTEL, health checks, resilience).
- **LLM abstraction via Microsoft.Extensions.AI:** `IChatClient` is the abstraction layer. Four providers are supported: Ollama, OpenAI, AzureOpenAI, GitHubCopilot. Copilot uses a separate `CopilotClient` SDK path via partial class `Generator-Copilot.cs`, with a `PlaceholderChatClient` standing in for the DI container.
- **State management is `AppState` (scoped, one per SignalR circuit).** Query history persists to `localStorage` via JS interop. Schema/config persist to local JSON files (`database_schema.json`, `llm_config.json`).
- **No test projects exist.** Zero unit tests, zero integration tests. This is a significant gap.
- **SQL generation pipeline:** `SqlPromptBuilder.CreateSystemPromptFromJson()` builds a structured text prompt from schema + config JSON → `Generator.GenerateStatementFromNlq()` sends it to the LLM → `CleanAndFormatSql()` strips markdown fences, comments, preamble and formats output.
- **Key file paths:** `src/AvnDataGenie/SqlPromptBuilder.cs` (prompt construction, ~370 LOC), `src/AvnDataGenie/Generator.cs` (LLM orchestration, ~305 LOC), `src/AdminApp/AppState.cs` (state management, ~230 LOC), `src/SchemaGenerator/Generator.cs` (DB introspection, ~310 LOC).
- **Schema model duplication:** `SqlPromptBuilder` defines its own internal DTOs (`DatabaseSchema`, `TableDefinition`, etc.) for JSON deserialization, separate from `SchemaGenerator.Models`. These are intentionally independent for decoupling.
- **Database support is SQL Server only.** `SchemaGenerator.Generator` uses `Microsoft.Data.SqlClient` and `INFORMATION_SCHEMA` views plus `sys.foreign_keys`/`sys.indexes`. Expanding to PostgreSQL/MySQL requires a provider abstraction.
- **Config.razor is the largest file (~49KB).** It contains inline scoped CSS and all four configuration tabs (Tables/Columns, Joins, Filters, Business Terms) in a single component.
- **Code documentation is 34% complete (10/29 files).** Model layer and AppState are done. Generator classes (highest business value) and all Razor pages/layout files remain undocumented.
- **.NET 10, Aspire SDK 13.1.0, targets net10.0.** Uses preview MEAI packages (9.7.0-preview).
