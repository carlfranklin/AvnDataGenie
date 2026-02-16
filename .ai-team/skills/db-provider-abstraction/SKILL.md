# SKILL: Database Provider Abstraction Pattern

**Author:** Cyborg
**Created:** 2026-02-16
**Applies to:** SchemaGenerator, SQLResults, SqlPromptBuilder

## Problem

AvnDataGenie is tightly coupled to SQL Server via concrete `SqlConnection`/`SqlCommand` types, SQL Server-specific system catalog queries (`sys.foreign_keys`, `INFORMATION_SCHEMA`), and T-SQL prompt rules. Supporting PostgreSQL/MySQL requires abstracting these touchpoints.

## Pattern

### Three interfaces decouple database-specific logic:

```csharp
// 1. Schema extraction — one implementation per DB engine
public interface ISchemaProvider
{
    Task<DatabaseSchema> GenerateSchemaAsync(string connectionString, CancellationToken ct = default);
    string ProviderName { get; } // "SqlServer", "PostgreSQL", "MySQL"
}

// 2. Query execution — one implementation per DB engine
public interface IQueryExecutor
{
    Task<DataTable> ExecuteAsync(string connectionString, string sql, int timeoutSeconds = 30, CancellationToken ct = default);
    Task<string> ValidateQueryAsync(string sql); // Returns null if valid, error message if not
}

// 3. SQL dialect rules — feeds into SqlPromptBuilder
public interface ISqlDialect
{
    string DialectName { get; } // "T-SQL", "PL/pgSQL", "MySQL"
    string GetPromptRules(); // Dialect-specific LLM instructions
    string WrapIdentifier(string name); // [name] vs "name" vs `name`
}
```

### Registration via DI factory:

```csharp
services.AddScoped<ISchemaProvider>(sp => {
    var config = sp.GetRequiredService<IOptions<Configuration>>().Value;
    return config.DatabaseType switch {
        DatabaseType.SqlServer => new SqlServerSchemaProvider(),
        DatabaseType.PostgreSQL => new PostgresSchemaProvider(),
        _ => throw new NotSupportedException($"Database type {config.DatabaseType} not supported")
    };
});
```

### Migration order:

1. Extract `ISchemaProvider` from `SchemaGenerator.Generator` → rename to `SqlServerSchemaProvider`
2. Extract `IQueryExecutor` from `SQLResults.razor` inline code → `SqlServerQueryExecutor`
3. Add `ISqlDialect` to `SqlPromptBuilder` for dialect-aware prompt generation
4. Add `DatabaseType` enum to `Configuration`
5. Update DI registration in `ApplicationExtensions.cs`

## When to Use

- When adding support for a new database engine
- When refactoring SchemaGenerator or SQLResults for testability
- When adding query validation or read-only enforcement

## Key Constraint

The `SqlPromptBuilder` must remain dialect-aware — T-SQL rules differ from PostgreSQL/MySQL. The `ISqlDialect` interface ensures prompt instructions match the target engine.
