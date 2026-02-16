---
name: "dotnet-ci-workflow"
description: "Pattern for .NET CI pipelines with Aspire projects in GitHub Actions"
domain: "devops"
confidence: "high"
source: "lucius"
---

## Context
This project uses .NET 10, .NET Aspire (AppHost SDK via NuGet), and has the solution file at `src/AvnDataGenie.sln` with `global.json` in `src/`. CI workflows must account for this non-root solution layout.

## Patterns

### Working Directory
The solution and `global.json` live in `src/`, not the repo root. Set `defaults.run.working-directory: src` on the job to keep all `dotnet` commands scoped correctly.

### Aspire AppHost in CI
The AppHost uses `Aspire.AppHost.Sdk/X.Y.Z` as an MSBuild SDK (resolved via NuGet). It does NOT require the deprecated Aspire workload. It compiles in CI without Docker or container runtime — it only needs containers at runtime, not build time.

### NuGet Cache Key
Use `hashFiles('src/**/*.csproj', 'src/global.json')` to key the NuGet cache. This captures both package version changes and SDK version changes.

### Action Pinning
Always pin actions to full commit SHAs with a version comment:
```yaml
uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2
```

### Concurrency
Use `concurrency.cancel-in-progress: true` with a branch-scoped group to avoid wasted CI minutes on rapid pushes.

### Test Readiness
Wire up `dotnet test` even when no test projects exist. It succeeds with 0 tests and automatically picks up new test projects when added.

## Anti-Patterns
- **Installing Aspire workload in CI** — it's deprecated. The SDK resolves via NuGet.
- **Running CI on `windows-latest` without justification** — doubles cost, no signal for Linux-targeted containerized apps.
- **Unpinned action versions** — supply-chain risk. Always SHA-pin.
- **Missing `--no-restore` on build** — wastes time re-resolving packages after explicit restore step.
