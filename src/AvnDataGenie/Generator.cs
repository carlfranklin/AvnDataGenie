using Azure.AI.Inference;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using GitHub.Copilot.SDK;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace AvnDataGenie;

/// <summary>
/// Natural Language Query (NLQ) to SQL generator powered by Large Language Models.
/// Translates user questions in plain English into executable T-SQL queries
/// using schema metadata and business rules to guide the LLM.
/// Supports multiple LLM backends including Ollama, OpenAI, Azure OpenAI, and GitHub Copilot.
/// </summary>
/// <param name="config">Configuration options for LLM settings (model, timeout, tokens)</param>
/// <param name="chatClient">Microsoft.Extensions.AI chat client for LLM interaction</param>
/// <param name="logger">Logger for diagnostics and debugging</param>
public partial class Generator(IOptions<Configuration> config, IChatClient chatClient, ILogger<Generator> logger)
{
	private readonly Configuration _config = config.Value;

	/// <summary>
	/// Cached system prompt containing database schema and business rules.
	/// Generated once and reused for all queries in the session for performance.
	/// </summary>
	public string SYSTEMPROMPT = "";

	/// <summary>
	/// Generates a T-SQL SELECT statement from a natural language query.
	/// Automatically routes to GitHub Copilot SDK or IChatClient based on configured LlmType.
	/// Combines schema metadata and business rules to create a constrained prompt
	/// that guides the LLM to produce valid, executable SQL.
	/// </summary>
	/// <param name="naturalLanguageQuery">User's question in plain English (e.g., "Show top 10 customers by revenue")</param>
	/// <param name="jsonSchema">JSON string containing database schema (from SchemaGenerator.Generator)</param>
	/// <param name="llmMetadata">JSON string containing business rules and metadata (LlmConfiguration)</param>
	/// <returns>Formatted T-SQL SELECT statement ready for execution</returns>
	/// <exception cref="OperationCanceledException">Thrown when LLM request times out</exception>
	/// <exception cref="Exception">Thrown when LLM service is unavailable or returns an error</exception>
	public async Task<string> GenerateStatementFromNlq(string naturalLanguageQuery, string jsonSchema, string llmMetadata)
	{
		// Route to appropriate LLM backend based on configuration
		if (_config.LlmType == LlmType.GitHubCopilot)
		{
			return await GenerateWithCopilotAsync(naturalLanguageQuery, jsonSchema, llmMetadata);
		}
		else
		{
			return await GenerateWithChatClientAsync(naturalLanguageQuery, jsonSchema, llmMetadata);
		}
	}

	/// <summary>
	/// Internal method to generate SQL using IChatClient (Ollama, OpenAI, Azure OpenAI).
	/// </summary>
	private async Task<string> GenerateWithChatClientAsync(string naturalLanguageQuery, string jsonSchema, string llmMetadata)
	{
		// Generate the system prompt if not already cached
		// System prompt contains schema + rules and is expensive to build, so we cache it
		if (string.IsNullOrWhiteSpace(SYSTEMPROMPT))
		{
			SYSTEMPROMPT = SqlPromptBuilder.CreateSystemPromptFromJson(jsonSchema, llmMetadata);

			// Optionally, write the system prompt to a file for debugging
			// Useful for understanding what context the LLM receives
			//string filePath = "C:\\MYPATH\\SystemPrompt.txt";
			//await File.WriteAllTextAsync(filePath, SYSTEMPROMPT);

			logger.LogInformation("System prompt generated and cached.");
			logger.LogDebug("System Prompt: {SystemPrompt}", SYSTEMPROMPT);
		}

		// Combine all user instructions into a single, structured message
		// This is more efficient than multiple user messages and helps LLM focus
		var combinedPrompt = $"""
			{(string.IsNullOrWhiteSpace(llmMetadata) ? "" : $"HINTS:\n{llmMetadata}\n")}

			QUERY: {naturalLanguageQuery}

			Return only T-SQL starting with SELECT.
			""";

		// Create chat messages: system prompt + user query
		// System prompt provides context and constraints
		// User prompt provides the specific question to answer
		var chatMessages = new ChatMessage[]
		{
			new ChatMessage(ChatRole.System, SYSTEMPROMPT),
			new ChatMessage(ChatRole.User, combinedPrompt)
		};

		// Configure LLM generation parameters
		var chatOptions = new ChatOptions
		{
			MaxOutputTokens = _config.MaxTokens,  // Limit response length
			Temperature = 0.15f  // Low temperature = more deterministic, focused output
		};

		// Send request with timeout to prevent hanging on slow/failed LLM calls
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.RequestTimeoutSeconds));
		var response = await chatClient.GetResponseAsync(chatMessages, chatOptions, cts.Token);

		// Clean and normalize the LLM response
		var sqlStatement = response.Text.Trim();
		
		return CleanAndFormatSql(sqlStatement);
	}

	/// <summary>
	/// Cleans and formats raw SQL output from any LLM.
	/// Extracts SQL from markdown blocks, removes comments and preambles, and formats for readability.
	/// </summary>
	/// <param name="sqlStatement">Raw SQL string from LLM</param>
	/// <returns>Clean, formatted T-SQL statement</returns>
	private static string CleanAndFormatSql(string sqlStatement)
	{
		sqlStatement = sqlStatement.Trim();
		
		// Extract SQL from markdown code blocks if LLM wrapped it in ```sql ... ```
		var sqlMatch = System.Text.RegularExpressions.Regex.Match(
			sqlStatement, 
			@"```(?:sql)?\s*([\s\S]*?)```", 
			System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		if (sqlMatch.Success)
		{
			sqlStatement = sqlMatch.Groups[1].Value.Trim();
		}
		
		// If LLM added preamble text, extract starting from SELECT keyword
		var selectIndex = sqlStatement.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
		if (selectIndex > 0)
		{
			sqlStatement = sqlStatement[selectIndex..];
		}
		
		// Remove trailing commentary after the SQL ends
		// Some LLMs add explanatory text after the query
		var lastSemicolon = sqlStatement.LastIndexOf(';');
		if (lastSemicolon > 0)
		{
			// Check if there's non-SQL text after the semicolon
			var afterSemicolon = sqlStatement[(lastSemicolon + 1)..].Trim();
			if (!string.IsNullOrEmpty(afterSemicolon) && !afterSemicolon.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
			{
				sqlStatement = sqlStatement[..(lastSemicolon + 1)];
			}
		}
		
		// Remove SQL comments (-- single line and /* multi-line */)
		// LLMs sometimes add explanatory comments which aren't needed
		sqlStatement = System.Text.RegularExpressions.Regex.Replace(sqlStatement, @"--.*?$", "", 
			System.Text.RegularExpressions.RegexOptions.Multiline);
		sqlStatement = System.Text.RegularExpressions.Regex.Replace(sqlStatement, @"/\*[\s\S]*?\*/", "");
		
		// Format the SQL for readability with proper indentation
		sqlStatement = FormatSql(sqlStatement);
		
		// Ensure statement ends with semicolon for clean execution
		if (!sqlStatement.EndsWith(';'))
		{
			sqlStatement += ";";
		}
		
		return sqlStatement;
	}

	/// <summary>
	/// Formats SQL statement for readability by adding proper line breaks and indentation.
	/// Uses regex-based rules to identify SQL keywords and structure the output.
	/// </summary>
	/// <param name="sql">Raw SQL string (potentially on one line)</param>
	/// <returns>Formatted SQL with newlines and indentation</returns>
	private static string FormatSql(string sql)
	{
		if (string.IsNullOrWhiteSpace(sql))
			return sql;

		// Normalize whitespace - collapse multiple spaces/tabs/newlines to single space
		sql = System.Text.RegularExpressions.Regex.Replace(sql.Trim(), @"\s+", " ");

		// Keywords that should start on a new line at the base indentation level
		var majorKeywords = new[] { "SELECT", "FROM", "WHERE", "GROUP BY", "HAVING", "ORDER BY", "UNION", "EXCEPT", "INTERSECT" };
		
		// JOIN keywords should be indented one level
		var joinKeywords = new[] { "INNER JOIN", "LEFT OUTER JOIN", "RIGHT OUTER JOIN", "FULL OUTER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "CROSS JOIN", "JOIN" };

		// Add newlines before major keywords (unless at start of statement)
		foreach (var keyword in majorKeywords)
		{
			sql = System.Text.RegularExpressions.Regex.Replace(
				sql,
				$@"(?<!^)\s+({keyword})\b",
				$"\n$1",
				System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		}

		// Add newlines before JOIN keywords with indentation
		foreach (var keyword in joinKeywords)
		{
			sql = System.Text.RegularExpressions.Regex.Replace(
				sql,
				$@"\s+({keyword})\b",
				$"\n    $1",
				System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		}

		// Add newline and indent after SELECT keyword (for column list)
		// Handles SELECT TOP(n) and SELECT DISTINCT
		sql = System.Text.RegularExpressions.Regex.Replace(
			sql,
			@"(SELECT(?:\s+TOP\s*\(\d+\)|\s+DISTINCT)?)\s+",
			"$1\n    ",
			System.Text.RegularExpressions.RegexOptions.IgnoreCase);

		// Add newlines after commas in SELECT list (respecting function parentheses)
		sql = FormatSelectColumns(sql);

		// Add newline before ON in JOIN clauses with extra indentation
		sql = System.Text.RegularExpressions.Regex.Replace(
			sql,
			@"\s+(ON)\s+",
			"\n        $1 ",
			System.Text.RegularExpressions.RegexOptions.IgnoreCase);

		// Clean up any double newlines that may have been introduced
		sql = System.Text.RegularExpressions.Regex.Replace(sql, @"\n\s*\n", "\n");
		
		return sql.Trim();
	}

	/// <summary>
	/// Formats columns in SELECT clause, adding newlines after commas while respecting function parentheses.
	/// Ensures that function calls like COUNT(*) remain on one line, while separating column names.
	/// </summary>
	/// <param name="sql">SQL string to format</param>
	/// <returns>SQL with properly formatted SELECT column list</returns>
	private static string FormatSelectColumns(string sql)
	{
		var result = new System.Text.StringBuilder();
		int parenDepth = 0;
		bool inSelectClause = false;

		for (int i = 0; i < sql.Length; i++)
		{
			char c = sql[i];

			// Detect when we enter/exit SELECT clause
			if (i + 6 <= sql.Length && sql.Substring(i, 6).Equals("SELECT", StringComparison.OrdinalIgnoreCase))
			{
				inSelectClause = true;
			}
			else if (i + 4 <= sql.Length && sql.Substring(i, 4).Equals("FROM", StringComparison.OrdinalIgnoreCase))
			{
				inSelectClause = false;
			}

			// Track parentheses depth to avoid splitting inside function calls
			// e.g., don't split COUNT(*) or SUBSTRING(name, 1, 10)
			if (c == '(')
				parenDepth++;
			else if (c == ')')
				parenDepth = Math.Max(0, parenDepth - 1);

			result.Append(c);

			// Add newline after comma only if:
			// 1. We're in SELECT clause
			// 2. We're not inside parentheses (parenDepth == 0)
			if (c == ',' && inSelectClause && parenDepth == 0)
			{
				result.Append("\n    ");
				// Skip any whitespace after the comma to avoid double spaces
				while (i + 1 < sql.Length && char.IsWhiteSpace(sql[i + 1]))
					i++;
			}
		}

		return result.ToString();
	}

	/// <summary>
	/// Disposes of Copilot resources when the Generator is disposed.
	/// Should be called when the application shuts down or when switching LLM providers.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_copilotSession != null)
		{
			await _copilotSession.DisposeAsync();
			_copilotSession = null;
			logger.LogInformation("Copilot session disposed.");
		}

		if (_copilotClient != null)
		{
			await _copilotClient.StopAsync();
			_copilotClient = null;
			logger.LogInformation("Copilot client stopped.");
		}
	}


}