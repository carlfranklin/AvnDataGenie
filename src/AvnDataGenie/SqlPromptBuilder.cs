namespace AvnDataGenie;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class SqlPromptBuilder
{
	/// <summary>
	/// Creates a SYSTEM prompt for an LLM that must generate a single SQL SELECT statement
	/// using only the provided schema + config (aliases, join hints, restricted/PII flags).
	/// </summary>
	public static string CreateSystemPromptFromJson(
	string databaseSchemaJson,
	string llmConfigJson,
	int maxTablesToInclude = 200,
	int maxColumnsPerTableToInclude = 200)
	{
		if (string.IsNullOrWhiteSpace(databaseSchemaJson))
			throw new ArgumentException("Database schema JSON is required.", nameof(databaseSchemaJson));

		if (string.IsNullOrWhiteSpace(llmConfigJson))
			throw new ArgumentException("LLM config JSON is required.", nameof(llmConfigJson));

		const string CRLF = "\r\n";

		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true
		};

		var schema = JsonSerializer.Deserialize<DatabaseSchema>(databaseSchemaJson, jsonOptions)
					 ?? throw new InvalidOperationException("Failed to deserialize database schema JSON.");

		var config = JsonSerializer.Deserialize<LlmConfig>(llmConfigJson, jsonOptions)
					 ?? throw new InvalidOperationException("Failed to deserialize LLM config JSON.");

		var tableConfigByFqn = (config.TableConfigurations ?? new List<TableConfiguration>())
			.ToDictionary(
				tc => $"{tc.SchemaName}.{tc.TableName}",
				tc => tc,
				StringComparer.OrdinalIgnoreCase);

		var sb = new StringBuilder(32_000);

		void Line(string? text = null)
			=> sb.Append(text ?? string.Empty).Append(CRLF);

		// ---------------------------------------------------------------------

		Line("You are a SQL query generator.");
		Line("Return EXACTLY ONE SQL Server SELECT statement and nothing else.");
		Line();

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

		Line("Database:");
		Line($" - Name: {schema.DatabaseName ?? "(unknown)"}");
		Line($" - Server: {schema.ServerName ?? "(unknown)"}");
		Line();

		if (config.JoinHints?.Count > 0)
		{
			Line("Join Hints:");
			foreach (var h in config.JoinHints)
			{
				var hint = string.IsNullOrWhiteSpace(h.Hint) ? "" : $"  // {h.Hint}";
				Line($" - {h.FromTable}.[{h.FromColumn}] -> {h.ToTable}.[{h.ToColumn}]{hint}");
			}
			Line();
		}

		if (config.RequiredFilters?.Count > 0)
		{
			Line("Required Filters:");
			foreach (var rf in config.RequiredFilters)
				Line($" - {rf}");
			Line();
		}

		if (config.BusinessTerms?.Count > 0)
		{
			Line("Business Terms:");
			foreach (var bt in config.BusinessTerms)
				Line($" - {bt}");
			Line();
		}

		Line("Schema:");

		foreach (var t in (schema.Tables ?? new List<TableDefinition>())
				 .Take(Math.Max(0, maxTablesToInclude)))
		{
			var fqn = $"{t.SchemaName}.{t.TableName}";
			tableConfigByFqn.TryGetValue(fqn, out var tc);

			var tableAliases =
				SplitAliases(tc?.Aliases)
					.Concat(t.Aliases ?? Enumerable.Empty<string>())
					.Where(a => !string.IsNullOrWhiteSpace(a))
					.Select(a => a.Trim())
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();

			Line($"- Table: [{t.SchemaName}].[{t.TableName}]");

			if (!string.IsNullOrWhiteSpace(tc?.FriendlyName))
				Line($"  FriendlyName: {tc.FriendlyName}");

			if (!string.IsNullOrWhiteSpace(tc?.Description))
				Line($"  Description: {tc.Description}");

			if (tableAliases.Count > 0)
				Line($"  Aliases: {string.Join(", ", tableAliases)}");

			if (t.PrimaryKey?.Columns?.Count > 0)
				Line($"  PrimaryKey: ({string.Join(", ", t.PrimaryKey.Columns.Select(c => $"[{c}]"))})");

			if (t.ForeignKeys?.Count > 0)
			{
				Line("  ForeignKeys:");
				foreach (var fk in t.ForeignKeys)
				{
					for (int i = 0; i < Math.Min(fk.Columns?.Count ?? 0, fk.ReferencedColumns?.Count ?? 0); i++)
					{
						Line(
							$"    - [{t.SchemaName}].[{t.TableName}].[{fk.Columns![i]}] -> " +
							$"[{fk.ReferencedSchema}].[{fk.ReferencedTable}].[{fk.ReferencedColumns![i]}]");
					}
				}
			}

			var colConfigByName = (tc?.ColumnConfigurations ?? new List<ColumnConfiguration>())
				.ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);

			Line("  Columns:");

			foreach (var c in (t.Columns ?? new List<ColumnDefinition>())
					 .Take(Math.Max(0, maxColumnsPerTableToInclude)))
			{
				colConfigByName.TryGetValue(c.ColumnName, out var cc);

				var colAliases =
					SplitAliases(cc?.Aliases)
						.Concat(c.Aliases ?? Enumerable.Empty<string>())
						.Where(a => !string.IsNullOrWhiteSpace(a))
						.Select(a => a.Trim())
						.Distinct(StringComparer.OrdinalIgnoreCase)
						.ToList();

				var flags = new List<string>();
				if (!c.IsNullable) flags.Add("NOT NULL");
				if (cc?.IsPii == true) flags.Add("PII");
				if (cc?.IsRestricted == true) flags.Add("RESTRICTED");

				var type = c.DataType ?? "unknown";
				if (c.MaxLength.HasValue) type += $"({c.MaxLength})";

				var line = $"    - [{c.ColumnName}] : {type}";
				if (flags.Count > 0) line += $" [{string.Join(", ", flags)}]";
				if (colAliases.Count > 0) line += $"  Aliases: {string.Join(", ", colAliases)}";
				if (!string.IsNullOrWhiteSpace(cc?.FriendlyName)) line += $"  FriendlyName: {cc.FriendlyName}";
				if (!string.IsNullOrWhiteSpace(cc?.Description)) line += $"  Description: {cc.Description}";

				Line(line);
			}

			Line();
		}

		Line("Output format:");
		Line("- SQL only. No markdown. No explanation. No JSON.");

		return sb.ToString();
	}



	private static IEnumerable<string> SplitAliases(string? aliases)
	{
		if (string.IsNullOrWhiteSpace(aliases)) yield break;

		// Your config uses a comma-delimited string for aliases, e.g. "record, recording"
		foreach (var a in aliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			yield return a;
	}

	// ===== Minimal models matching your JSON =====

	public sealed class DatabaseSchema
	{
		[JsonPropertyName("databaseName")] public string? DatabaseName { get; set; }
		[JsonPropertyName("serverName")] public string? ServerName { get; set; }
		[JsonPropertyName("generatedAt")] public string? GeneratedAt { get; set; }
		[JsonPropertyName("tables")] public List<TableDefinition>? Tables { get; set; }
	}

	public sealed class TableDefinition
	{
		[JsonPropertyName("schemaName")] public string SchemaName { get; set; } = "dbo";
		[JsonPropertyName("tableName")] public string TableName { get; set; } = "";
		[JsonPropertyName("aliases")] public List<string>? Aliases { get; set; }
		[JsonPropertyName("columns")] public List<ColumnDefinition>? Columns { get; set; }
		[JsonPropertyName("primaryKey")] public PrimaryKeyDefinition? PrimaryKey { get; set; }
		[JsonPropertyName("foreignKeys")] public List<ForeignKeyDefinition>? ForeignKeys { get; set; }
	}

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

	public sealed class PrimaryKeyDefinition
	{
		[JsonPropertyName("constraintName")] public string? ConstraintName { get; set; }
		[JsonPropertyName("columns")] public List<string>? Columns { get; set; }
	}

	public sealed class ForeignKeyDefinition
	{
		[JsonPropertyName("constraintName")] public string? ConstraintName { get; set; }
		[JsonPropertyName("referencedSchema")] public string ReferencedSchema { get; set; } = "dbo";
		[JsonPropertyName("referencedTable")] public string ReferencedTable { get; set; } = "";
		[JsonPropertyName("columns")] public List<string>? Columns { get; set; }
		[JsonPropertyName("referencedColumns")] public List<string>? ReferencedColumns { get; set; }
	}

	public sealed class LlmConfig
	{
		[JsonPropertyName("tableConfigurations")] public List<TableConfiguration>? TableConfigurations { get; set; }
		[JsonPropertyName("joinHints")] public List<JoinHint>? JoinHints { get; set; }
		[JsonPropertyName("requiredFilters")] public List<string>? RequiredFilters { get; set; }
		[JsonPropertyName("businessTerms")] public List<string>? BusinessTerms { get; set; }
	}

	public sealed class TableConfiguration
	{
		[JsonPropertyName("schemaName")] public string SchemaName { get; set; } = "dbo";
		[JsonPropertyName("tableName")] public string TableName { get; set; } = "";
		[JsonPropertyName("friendlyName")] public string? FriendlyName { get; set; }
		[JsonPropertyName("description")] public string? Description { get; set; }
		[JsonPropertyName("aliases")] public string? Aliases { get; set; } // comma-delimited in your file
		[JsonPropertyName("columnConfigurations")] public List<ColumnConfiguration>? ColumnConfigurations { get; set; }
	}

	public sealed class ColumnConfiguration
	{
		[JsonPropertyName("columnName")] public string ColumnName { get; set; } = "";
		[JsonPropertyName("friendlyName")] public string? FriendlyName { get; set; }
		[JsonPropertyName("description")] public string? Description { get; set; }
		[JsonPropertyName("aliases")] public string? Aliases { get; set; } // comma-delimited
		[JsonPropertyName("isPii")] public bool IsPii { get; set; }
		[JsonPropertyName("isRestricted")] public bool IsRestricted { get; set; }
	}

	public sealed class JoinHint
	{
		[JsonPropertyName("fromTable")] public string FromTable { get; set; } = "";
		[JsonPropertyName("fromColumn")] public string FromColumn { get; set; } = "";
		[JsonPropertyName("toTable")] public string ToTable { get; set; } = "";
		[JsonPropertyName("toColumn")] public string ToColumn { get; set; } = "";
		[JsonPropertyName("hint")] public string? Hint { get; set; }
	}
}
