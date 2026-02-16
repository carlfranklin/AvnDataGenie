# Decision: CI Pipeline Configuration

**By:** Lucius (DevOps Engineer)
**Date:** 2026-02-16

## What

Created `.github/workflows/ci.yml` — a GitHub Actions CI pipeline that runs on every push to `main` and every PR targeting `main`. It restores, builds (Release config), and runs tests for the full solution.

## Key Choices

1. **Ubuntu-only** — no Windows matrix. The project targets Linux containers (`DockerDefaultTargetOS=Linux`) and .NET 10 is cross-platform. Adding Windows doubles pipeline cost with no signal gain.
2. **All actions SHA-pinned** — `actions/checkout@v6.0.2`, `actions/setup-dotnet@v5.1.0`, `actions/cache@v5.0.3`. Supply-chain security, not negotiable.
3. **NuGet cache** — keyed on `*.csproj` + `global.json` hashes. Saves ~30s on repeat runs.
4. **Concurrency group** — cancels in-progress runs on the same branch. No wasted compute on rapid pushes.
5. **AppHost included in build** — the Aspire AppHost uses `Aspire.AppHost.Sdk/13.1.0` which resolves via NuGet, not the deprecated Aspire workload. It compiles cleanly in CI.
6. **Test step wired up but no test projects yet** — `dotnet test` will succeed with 0 tests. When test projects are added, CI picks them up automatically.

## Why

Every PR needs a green check before a human looks at it. Zero test coverage + zero CI = chaos. This is the floor.

## Impact

All team members should be aware that pushes to `main` and PRs to `main` now trigger CI. Build failures will block merges if branch protection is configured.
