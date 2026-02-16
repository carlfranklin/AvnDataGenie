---
name: "dotnet-testing"
description: "Testing patterns and conventions for the AvnDataGenie .NET solution"
domain: "testing"
confidence: "high"
source: "huntress"
---

## Context
The AvnDataGenie solution uses xUnit on .NET 10. The test project is `src/AvnDataGenie.Tests/`. Tests run with `dotnet test src/AvnDataGenie.Tests/AvnDataGenie.Tests.csproj`.

## Patterns

### InternalsVisibleTo
`AvnDataGenie.csproj` has `<InternalsVisibleTo Include="AvnDataGenie.Tests" />`. Use `internal` visibility (not `public`) for methods that need testing but shouldn't be part of the public API.

### Test File Organization
One test class per production class or logical unit:
- `SqlPromptBuilderTests.cs` — tests for `SqlPromptBuilder.CreateSystemPromptFromJson()`
- `CleanAndFormatSqlTests.cs` — tests for `Generator.CleanAndFormatSql()`
- `FormatSqlTests.cs` — tests for `Generator.FormatSql()` and `Generator.FormatSelectColumns()`

### Test Data Helpers
Use `private static` methods to generate test JSON inputs (e.g., `MinimalSchemaJson()`, `RichConfigJson()`). Keep test data as close to real shapes as possible — the JSON must match the deserialization models in `SqlPromptBuilder`.

### Pure Functions First
Prioritize testing pure/static methods that have no external dependencies. In this codebase:
- `SqlPromptBuilder.CreateSystemPromptFromJson()` — public static, takes JSON strings
- `Generator.CleanAndFormatSql()` — internal static, sanitizes LLM output
- `Generator.FormatSql()` — internal static, formats SQL
- `Generator.FormatSelectColumns()` — internal static, formats SELECT columns

### SchemaGenerator Requires Integration Tests
`SchemaGenerator.Generator.GenerateDatabaseSchemaAsync()` requires a live SQL Server connection. Unit tests cannot cover this without a database fixture.

## Anti-Patterns
- **Don't mock what you can test directly** — prefer calling pure functions over mocking IChatClient.
- **Don't test formatting by exact string match** — use `Assert.Contains` for structural assertions since the formatting regexes produce complex output.
- **Don't skip edge cases** — LLM output is unpredictable. Test markdown fences, preambles, trailing commentary, comments, and missing semicolons.
