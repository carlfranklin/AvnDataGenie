# Skill: XML Documentation for AvnDataGenie

## When to Use
When adding or updating XML documentation comments in this codebase.

## Pattern

### Check Before Writing
Always read the file first. Previous agents have documented many files already — avoid duplicating existing docs.

### What Needs Docs
1. **Class-level** `<summary>` on every public class, including `static` and `partial` classes
2. **Method-level** `<summary>`, `<param>`, `<returns>`, `<exception>` on all public methods and key private methods
3. **Property-level** `<summary>` on every public property
4. **Enum members** — each value gets a `/// <summary>` one-liner
5. **Inline comments** only on complex logic blocks (regex parsing, event-driven patterns, defensive checks)

### What to Skip
- Don't comment trivial getters/setters that are self-documenting
- Don't add inline comments to straightforward CRUD or simple assignments
- Don't duplicate what the method signature already says

### Format Reference
Follow the templates in `COMMENTING_PROGRESS.md` under "COMMENTING GUIDELINES".

### Verification
- Documentation-only changes cannot break compilation, but always attempt `dotnet build` to confirm
- Note: AvnDataGenie.csproj has a pre-existing NU1605 NuGet error that blocks full solution build (as of 2026-02-16)
