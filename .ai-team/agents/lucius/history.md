# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** AvnDataGenie — OSS natural language database querying system with schema generation, metadata management, and LLM-powered SQL generation.
- **Stack:** C#, .NET 10, Blazor Server, .NET Aspire, SQL Server, Ollama/OpenAI/Azure OpenAI/GitHub Copilot CLI
- **Created:** 2026-02-16

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **2026-02-16:** Created `.github/workflows/ci.yml` — build & test pipeline for every push/PR to main. .NET 10 (`10.0.x`), solution at `src/AvnDataGenie.sln`, `global.json` in `src/`. All actions pinned to SHAs (checkout v6.0.2, setup-dotnet v5.1.0, cache v5.0.3). NuGet cache enabled. Test step auto-discovers all test projects via `dotnet test`.
- AppHost uses `Aspire.AppHost.Sdk/13.1.0` (NuGet-based, not the deprecated workload) — builds fine in CI without Docker at compile time.
- Existing workflows: `build-adminapp-container.yml` (Docker build, path-scoped), plus squad operational workflows (heartbeat, triage, label-enforce, main-guard, issue-assign). No conflicts with CI workflow.

## Cross-Agent Updates

📌 **Team update (2026-02-16):** Roadmap priority established: documentation-first, then CSV export and favorites as quick wins. Multi-DB and RBAC deferred pending foundation work. — decided by Oracle

📌 **Team update (2026-02-16):** Test infrastructure is live. Huntress created xUnit project with 64 passing tests covering SQL formatting and prompt building. All test projects auto-discovered by CI workflow. Full test integration achieved. — decided by Huntress
