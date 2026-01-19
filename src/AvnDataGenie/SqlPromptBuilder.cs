namespace AvnDataGenie;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Builds comprehensive system prompts for LLMs to generate SQL queries.
/// Combines database schema metadata with business rules and configuration
/// to create precise, constrained prompts that guide the LLM to produce
/// valid T-SQL SELECT statements.
/// </summary>
public static class SqlPromptBuilder
{
	/// <summary>
	/// Creates a SYSTEM prompt for an LLM that must generate a single SQL SELECT statement
	/// using only the provided schema + config (aliases, join hints, restricted/PII flags).
	/// </summary>
	/// <param name="databaseSchemaJson">JSON string containing the database schema (tables, columns, keys)</param>
	/// <param name="llmConfigJson">JSON string containing business metadata and rules</param>
	/// <param name="maxTablesToInclude">Maximum number of tables to include in prompt (prevents token overflow)</param>
	/// <param name="maxColumnsPerTableToInclude">Maximum columns per table to include in prompt</param>
	/// <returns>A formatted system prompt ready for LLM consumption</returns>
	/// <exception cref="ArgumentException">Thrown when required JSON parameters are null or empty</exception>
	/// <exception cref="InvalidOperationException">Thrown when JSON deserialization fails</exception>
	public static string CreateSystemPromptFromJson(
	string databaseSchemaJson,
	string llmConfigJson,
	int maxTablesToInclude = 200,
	int maxColumnsPerTableToInclude = 200)
	{
		// Validate required inputs
		if (string.IsNullOrWhiteSpace(databaseSchemaJson))
			throw new ArgumentException("Database schema JSON is required.", nameof(databaseSchemaJson));

		if (string.IsNullOrWhiteSpace(llmConfigJson))
			throw new ArgumentException("LLM config JSON is required.", nameof(llmConfigJson));

		const string CRLF = "\r\n";

		// Configure JSON deserialization to be flexible with input formats
		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,  // Allow "DatabaseName" or "databaseName"
			ReadCommentHandling = JsonCommentHandling.Skip,  // Ignore JSON comments
			AllowTrailingCommas = true  // Be lenient with trailing commas
		};

		// Deserialize schema and configuration from JSON
		var schema = JsonSerializer.Deserialize<DatabaseSchema>(databaseSchemaJson, jsonOptions)
					 ?? throw new InvalidOperationException("Failed to deserialize database schema JSON.");

		var config = JsonSerializer.Deserialize<LlmConfig>(llmConfigJson, jsonOptions)
					 ?? throw new InvalidOperationException("Failed to deserialize LLM config JSON.");

		// Build lookup dictionary for fast table configuration access by fully-qualified name
		var tableConfigByFqn = (config.TableConfigurations ?? new List<TableConfiguration>())
			.ToDictionary(
				tc => $"{tc.SchemaName}.{tc.TableName}",
				tc => tc,
				StringComparer.OrdinalIgnoreCase);

		// StringBuilder for efficient string concatenation (32KB initial capacity)
		var sb = new StringBuilder(32_000);

		// Helper to append a line with consistent line endings
		void Line(string? text = null)
			=> sb.Append(text ?? string.Empty).Append(CRLF);

		// ---------------------------------------------------------------------
		// SECTION 1: Core Instructions
		// ---------------------------------------------------------------------
		Line("You are a SQL query generator.");
		Line("Return EXACTLY ONE SQL Server SELECT statement and nothing else.");
		Line();

		// Define strict rules to constrain LLM behavior
		Line("Hard rules:");
		Line("1) Output must be a single T-SQL SELECT statement (no INSERT/UPDATE/DELETE/MERGE/DDL).");
		Line("2) Use ONLY tables and columns that exist in the provided schema.");
		Line("3) Prefer joins based on declared foreign keys; join hints may clarify intent.");
		Line("4) Do not invent parameters. Use literal values only if the user provides them.");
		Line("5) If the request is ambiguous, choose the safest reasonable interpretation.");
		Line("6) If a field is marked PII or RESTRICTED, do NOT select it.");
		Line("7) Qualify tables as [schema].[table] and columns as [alias].[column].");
		Line("8) Use explicit JOIN ... ON ... clauses (no implicit joins).");
		Line("9) Use GROUP BY correctly and TOP (N) with ORDER BY when applicable.");
		Line();

		// ---------------------------------------------------------------------
		// SECTION 2: Database Context
		// ---------------------------------------------------------------------
		Line("Database:");
		Line($" - Name: {schema.DatabaseName ?? "(unknown)"}");
		Line($" - Server: {schema.ServerName ?? "(unknown)"}");
		Line();

		// ---------------------------------------------------------------------
		// SECTION 3: Business Rules and Hints
		// ---------------------------------------------------------------------
		
		// Include join hints for relationships not captured by foreign keys
		if (config.JoinHints?.Count > 0)
		{
			Line("Join Hints:");
			foreach (var h in config.JoinHints)
			{
				// Format: FromTable.FromColumn -> ToTable.ToColumn // Optional hint text
				var hint = string.IsNullOrWhiteSpace(h.Hint) ? "" : $"  // {h.Hint}";
				Line($" - {h.FromTable}.[{h.FromColumn}] -> {h.ToTable}.[{h.ToColumn}]{hint}");
			}
			Line();
		}

		// Include mandatory filters (security, multi-tenancy, soft-deletes)
		if (config.RequiredFilters?.Count > 0)
		{
			Line("Required Filters:");
			foreach (var rf in config.RequiredFilters)
				Line($" - {rf}");
			Line();
		}

		// Include business terminology for domain understanding
		if (config.BusinessTerms?.Count > 0)
		{
			Line("Business Terms:");
			foreach (var bt in config.BusinessTerms)
				Line($" - {bt}");
			Line();
		}

		// ---------------------------------------------------------------------
		// SECTION 4: Detailed Schema Information
		// ---------------------------------------------------------------------
		Line("Schema:");

		// Iterate through tables (limited to prevent token overflow)
		foreach (var t in (schema.Tables ?? new List<TableDefinition>())
				 .Take(Math.Max(0, maxTablesToInclude)))
		{
			var fqn = $"{t.SchemaName}.{t.TableName}";
			tableConfigByFqn.TryGetValue(fqn, out var tc);

			// Merge aliases from both schema and config, removing duplicates
			var tableAliases =
				SplitAliases(tc?.Aliases)
					.Concat(t.Aliases ?? Enumerable.Empty<string>())
					.Where(a => !string.IsNullOrWhiteSpace(a))
					.Select(a => a.Trim())
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();

			// Table header with schema qualification
			Line($"- Table: [{t.SchemaName}].[{t.TableName}]");

			// Include business-friendly metadata if available
			if (!string.IsNullOrWhiteSpace(tc?.FriendlyName))
				Line($"  FriendlyName: {tc.FriendlyName}");

			if (!string.IsNullOrWhiteSpace(tc?.Description))
				Line($"  Description: {tc.Description}");

			if (tableAliases.Count > 0)
				Line($"  Aliases: {string.Join(", ", tableAliases)}");

			// Primary key information (helps LLM with joins and uniqueness)
			if (t.PrimaryKey?.Columns?.Count > 0)
				Line($"  PrimaryKey: ({string.Join(", ", t.PrimaryKey.Columns.Select(c => $"[{c}]"))})");

			// Foreign key relationships (critical for join generation)
			if (t.ForeignKeys?.Count > 0)
			{
				Line("  ForeignKeys:");
				foreach (var fk in t.ForeignKeys)
				{
					// Handle composite foreign keys (multiple columns)
					for (int i = 0; i < Math.Min(fk.Columns?.Count ?? 0, fk.ReferencedColumns?.Count ?? 0); i++)
					{
						Line(
							$"    - [{t.SchemaName}].[{t.TableName}].[{fk.Columns![i]}] -> " +
							$"[{fk.ReferencedSchema}].[{fk.ReferencedTable}].[{fk.ReferencedColumns![i]}]");
					}
				}
			}

			// Build column configuration lookup for this table
			var colConfigByName = (tc?.ColumnConfigurations ?? new List<ColumnConfiguration>())
				.ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);

			Line("  Columns:");

			// Iterate through columns (limited to prevent token overflow)
			foreach (var c in (t.Columns ?? new List<ColumnDefinition>())
					 .Take(Math.Max(0, maxColumnsPerTableToInclude)))
			{
				colConfigByName.TryGetValue(c.ColumnName, out var cc);

				// Merge column aliases from schema and config
				var colAliases =
					SplitAliases(cc?.Aliases)
						.Concat(c.Aliases ?? Enumerable.Empty<string>())
						.Where(a => !string.IsNullOrWhiteSpace(a))
						.Select(a => a.Trim())
						.Distinct(StringComparer.OrdinalIgnoreCase)
						.ToList();

				// Build flags for constraints and security
				var flags = new List<string>();
				if (!c.IsNullable) flags.Add("NOT NULL");
				if (cc?.IsPii == true) flags.Add("PII");  // Personally Identifiable Information
				if (cc?.IsRestricted == true) flags.Add("RESTRICTED");  // Access restricted

				// Format data type with length/precision if applicable
				var type = c.DataType ?? "unknown";
				if (c.MaxLength.HasValue) type += $"({c.MaxLength})";

				// Build complete column description line
				var line = $"    - [{c.ColumnName}] : {type}";
				if (flags.Count > 0) line += $" [{string.Join(", ", flags)}]";
				if (colAliases.Count > 0) line += $"  Aliases: {string.Join(", ", colAliases)}";
				if (!string.IsNullOrWhiteSpace(cc?.FriendlyName)) line += $"  FriendlyName: {cc.FriendlyName}";
				if (!string.IsNullOrWhiteSpace(cc?.Description)) line += $"  Description: {cc.Description}";

				Line(line);
			}

			Line();
		}

		// ---------------------------------------------------------------------
		// SECTION 5: Output Format Instructions
		// ---------------------------------------------------------------------
		Line("Output format:");
		Line("- SQL only. No markdown. No explanation. No JSON.");

		return sb.ToString();
	}

	/// <summary>
	/// Splits a comma-delimited alias string into individual alias strings.
	/// Handles null/whitespace gracefully.
	/// </summary>
	/// <param name="aliases">Comma-separated alias string (e.g., "record, recording, entry")</param>
	/// <returns>Enumerable of individual alias strings, trimmed and non-empty</returns>
	private static IEnumerable<string> SplitAliases(string? aliases)
	{
		if (string.IsNullOrWhiteSpace(aliases)) yield break;

		// Split on comma, remove empty entries, trim whitespace
		foreach (var a in aliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			yield return a;
	}

	// =========================================================================
	// Internal Models for JSON Deserialization
	// These mirror the structure of the database_schema.json and llm_config.json files
	// =========================================================================

	/// <summary>
	/// Represents the complete database schema structure from JSON.
	/// Internal model used only for prompt building.
	/// </summary>
	public sealed class DatabaseSchema
	{
		[JsonPropertyName("databaseName")] public string? DatabaseName { get; set; }
		[JsonPropertyName("serverName")] public string? ServerName { get; set; }
		[JsonPropertyName("generatedAt")] public string? GeneratedAt { get; set; }
		[JsonPropertyName("tables")] public List<TableDefinition>? Tables { get; set; }
	}

	/// <summary>
	/// Represents a single table definition from the schema JSON.
	/// </summary>
	public sealed class TableDefinition
	{
		[JsonPropertyName("schemaName")] public string SchemaName { get; set; } = "dbo";
		[JsonPropertyName("tableName")] public string TableName { get; set; } = "";
		[JsonPropertyName("aliases")] public List<string>? Aliases { get; set; }
		[JsonPropertyName("columns")] public List<ColumnDefinition>? Columns { get; set; }
		[JsonPropertyName("primaryKey")] public PrimaryKeyDefinition? PrimaryKey { get; set; }
		[JsonPropertyName("foreignKeys")] public List<ForeignKeyDefinition>? ForeignKeys { get; set; }
	}

	/// <summary>
	/// Represents a single column definition from the schema JSON.
	/// </summary>
	public sealed class ColumnDefinition
	{
		[JsonPropertyName("columnName")] public string ColumnName { get; set; } = "";
		[JsonPropertyName("dataType")] public string? DataType { get; set; }
		[JsonPropertyName("aliases")] public List<string>? Aliases { get; set; }
		[JsonPropertyName("maxLength")] public int? MaxLength { get; set; }
		[JsonPropertyName("numericPrecision")] public int? NumericPrecision { get; set; }
		[JsonPropertyName("numericScale")] public int? NumericScale { get; set; }
		[JsonPropertyName("isNullable")] public bool IsNullable { get; set; }
	}

	/// <summary>
	/// Represents a primary key constraint from the schema JSON.
	/// </summary>
	public sealed class PrimaryKeyDefinition
	{
		[JsonPropertyName("constraintName")] public string? ConstraintName { get; set; }
		[JsonPropertyName("columns")] public List<string>? Columns { get; set; }
	}

	/// <summary>
	/// Represents a foreign key relationship from the schema JSON.
	/// </summary>
	public sealed class ForeignKeyDefinition
	{
		[JsonPropertyName("constraintName")] public string? ConstraintName { get; set; }
		[JsonPropertyName("referencedSchema")] public string ReferencedSchema { get; set; } = "dbo";
		[JsonPropertyName("referencedTable")] public string ReferencedTable { get; set; } = "";
		[JsonPropertyName("columns")] public List<string>? Columns { get; set; }
		[JsonPropertyName("referencedColumns")] public List<string>? ReferencedColumns { get; set; }
	}

	/// <summary>
	/// Represents the LLM configuration from JSON (business rules and metadata).
	/// </summary>
	public sealed class LlmConfig
	{
		[JsonPropertyName("tableConfigurations")] public List<TableConfiguration>? TableConfigurations { get; set; }
		[JsonPropertyName("joinHints")] public List<JoinHint>? JoinHints { get; set; }
		[JsonPropertyName("requiredFilters")] public List<string>? RequiredFilters { get; set; }
		[JsonPropertyName("businessTerms")] public List<string>? BusinessTerms { get; set; }
	}

	/// <summary>
	/// Business metadata for a specific table from the LLM config.
	/// </summary>
	public sealed class TableConfiguration
	{
		[JsonPropertyName("schemaName")] public string SchemaName { get; set; } = "dbo";
		[JsonPropertyName("tableName")] public string TableName { get; set; } = "";
		[JsonPropertyName("friendlyName")] public string? FriendlyName { get; set; }
		[JsonPropertyName("description")] public string? Description { get; set; }
		[JsonPropertyName("aliases")] public string? Aliases { get; set; } // Comma-delimited in JSON file
		[JsonPropertyName("columnConfigurations")] public List<ColumnConfiguration>? ColumnConfigurations { get; set; }
	}

	/// <summary>
	/// Business metadata for a specific column from the LLM config.
	/// </summary>
	public sealed class ColumnConfiguration
	{
		[JsonPropertyName("columnName")] public string ColumnName { get; set; } = "";
		[JsonPropertyName("friendlyName")] public string? FriendlyName { get; set; }
		[JsonPropertyName("description")] public string? Description { get; set; }
		[JsonPropertyName("aliases")] public string? Aliases { get; set; } // Comma-delimited in JSON file
		[JsonPropertyName("isPii")] public bool IsPii { get; set; }
		[JsonPropertyName("isRestricted")] public bool IsRestricted { get; set; }
	}

	/// <summary>
	/// Explicit join hint for relationships not captured by foreign keys.
	/// </summary>
	public sealed class JoinHint
	{
		[JsonPropertyName("fromTable")] public string FromTable { get; set; } = "";
		[JsonPropertyName("fromColumn")] public string FromColumn { get; set; } = "";
		[JsonPropertyName("toTable")] public string ToTable { get; set; } = "";
		[JsonPropertyName("toColumn")] public string ToColumn { get; set; } = "";
		[JsonPropertyName("hint")] public string? Hint { get; set; }
	}
}
