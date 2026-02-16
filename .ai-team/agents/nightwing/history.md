# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** AvnDataGenie — OSS natural language database querying system with schema generation, metadata management, and LLM-powered SQL generation.
- **Stack:** C#, .NET 10, Blazor Server, .NET Aspire, SQL Server, Ollama/OpenAI/Azure OpenAI/GitHub Copilot CLI
- **Created:** 2026-02-16

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **2026-02-16:** Most HIGH-priority files already had substantial XML docs when inspected — previous work (likely by Oracle/another agent) documented Generator.cs, SqlPromptBuilder.cs, and SchemaGenerator/Generator.cs extensively. Gaps were surgical: missing param docs, missing class-level docs on static classes, missing enum member docs, and missing docs on the Generator-Copilot.cs partial class.
- **2026-02-16:** Pre-existing NuGet NU1605 error in AvnDataGenie.csproj — `Microsoft.Extensions.Caching.Memory 9.0.0` transitively pulls `Microsoft.Extensions.DependencyInjection.Abstractions 10.0.2` but project directly references `9.0.6`. This blocks full-solution build. Not related to documentation work.
- **2026-02-16:** Generator is a partial class split across `Generator.cs` (IChatClient path) and `Generator-Copilot.cs` (CopilotClient path). Both share `CleanAndFormatSql` and `SYSTEMPROMPT` cache. Any future refactoring must account for both files.

## Cross-Agent Updates

📌 **Team update (2026-02-16):** Roadmap priority established: documentation-first, then CSV export and favorites as quick wins. Multi-DB and RBAC deferred pending foundation work. — decided by Oracle

📌 **Team update (2026-02-16):** NuGet dependency conflict resolved by Huntress. Update: `Microsoft.Extensions.DependencyInjection.Abstractions` is now 10.0.2 in AvnDataGenie.csproj. Builds are unblocked. Test infrastructure (64 tests) is live and all passing. — decided by Huntress
