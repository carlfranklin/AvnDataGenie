# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** AvnDataGenie — OSS natural language database querying system with schema generation, metadata management, and LLM-powered SQL generation.
- **Stack:** C#, .NET 10, Blazor Server, .NET Aspire, SQL Server, Ollama/OpenAI/Azure OpenAI/GitHub Copilot CLI
- **Created:** 2026-02-16

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- Test project: `src/AvnDataGenie.Tests/` — xUnit on .NET 10, references AvnDataGenie and SchemaGenerator.
- `InternalsVisibleTo("AvnDataGenie.Tests")` added to `AvnDataGenie.csproj` to expose `CleanAndFormatSql`, `FormatSql`, `FormatSelectColumns` for testing.
- These three methods were changed from `private static` to `internal static` in `Generator.cs` — they are pure functions and the highest-leverage test targets.
- `SqlPromptBuilder.CreateSystemPromptFromJson()` is `public static` and fully testable without mocking — takes JSON strings, returns a prompt string.
- `SqlPromptBuilder` uses internal deserialization models (`DatabaseSchema`, `LlmConfig`, etc.) which are `public sealed class` nested types.
- The JSON deserialization is configured with `PropertyNameCaseInsensitive`, `ReadCommentHandling.Skip`, and `AllowTrailingCommas`.
- `FormatSql` regex chain processes major keywords, then JOIN keywords, then SELECT columns — the order matters and can split compound keywords like "INNER JOIN" across lines.
- `SchemaGenerator.Generator.GenerateDatabaseSchemaAsync` requires a live SQL Server connection — not unit-testable without integration setup.
- Pre-existing package version conflict: `Microsoft.Extensions.DependencyInjection.Abstractions` was pinned at 9.0.6 but transitives required 10.0.2. Updated to 10.0.2 to unblock builds.
- Build command: `dotnet test src/AvnDataGenie.Tests/AvnDataGenie.Tests.csproj` from repo root.
- The `LlmConfiguration` model in SchemaGenerator uses class types (`RequiredFilter`, `BusinessTerm`, `JoinHint`) while `SqlPromptBuilder`'s internal `LlmConfig` uses `List<string>` for required filters and business terms — these are separate models with different shapes.

## Cross-Agent Updates

📌 **Team update (2026-02-16):** Roadmap priority established: documentation-first, then CSV export and favorites as quick wins. Multi-DB and RBAC deferred pending foundation work. — decided by Oracle

📌 **Team update (2026-02-16):** P1 Security recommendations from database audit: Add SQL validation before execution (verify SELECT-only), add result set row limit (TOP enforcement), consider read-only connection enforcement. SQL validation is critical for LLM-generated output. — decided by Cyborg

📌 **Team update (2026-02-16):** CI integration complete. Test step is now live in `.github/workflows/ci.yml`. All test projects are auto-discovered and run on every push/PR. — decided by Lucius
