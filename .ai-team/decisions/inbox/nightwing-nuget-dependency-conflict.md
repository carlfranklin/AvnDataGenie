# Pre-existing NuGet Dependency Conflict Blocks Build

**By:** Nightwing
**Date:** 2026-02-16

**What:** `dotnet build src\AvnDataGenie.sln` fails with NU1605 errors due to a transitive package version conflict. `Microsoft.Extensions.Caching.Memory 9.0.0` pulls `Microsoft.Extensions.DependencyInjection.Abstractions 10.0.2` transitively, but the project directly references version `9.0.6`.

**Impact:** Cannot verify compilation of AvnDataGenie or AvnDataGenie.Tests projects via `dotnet build`. SchemaGenerator and AdminApp build independently.

**Suggested Fix:** Update `Microsoft.Extensions.DependencyInjection.Abstractions` to `10.0.2` in AvnDataGenie.csproj, or update `Microsoft.Extensions.Caching.Memory` to a compatible 10.x version. This should be addressed before any feature work proceeds.
