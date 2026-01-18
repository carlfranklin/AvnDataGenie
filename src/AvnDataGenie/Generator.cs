using Azure.AI.Inference;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace AvnDataGenie;

public class Generator(IOptions<Configuration> config, IChatClient chatClient)
{
	private readonly Configuration _config = config.Value;

	public string SYSTEMPROMPT = "";
	//public const string SYSTEMPROMPT = """
	//	Generate T-SQL SELECT statements. Output SQL only, no explanations.

	//	COLUMN NAME RULES (use EXACTLY these names):
	//	- Artist: ArtistId, Name (NOT ArtistName)
	//	- Album: AlbumId, Title, ArtistId
	//	- Track: TrackId, Name, AlbumId, MediaTypeId, GenreId, Composer, Milliseconds, Bytes, UnitPrice
	//	- InvoiceLine: InvoiceLineId, InvoiceId, TrackId, UnitPrice, Quantity (NOT LineItemPrice, NOT LineItemQuantity)
	//	- Invoice: InvoiceId, CustomerId, InvoiceDate, Total (InvoiceDate is HERE, not on InvoiceLine)
	//	- Customer: CustomerId, FirstName, LastName, Email, SupportRepId
	//	- Genre: GenreId, Name
	//	- MediaType: MediaTypeId, Name
	//	- Playlist: PlaylistId, Name
	//	- PlaylistTrack: PlaylistId, TrackId
	//	- Employee: EmployeeId, FirstName, LastName, Title, ReportsTo

	//	JOIN PATHS:
	//	- Artist sales: InvoiceLine → Track → Album → Artist
	//	- Need InvoiceDate? Join InvoiceLine → Invoice

	//	FORMAT:
	//	SELECT TOP(n)
	//	    col1,
	//	    col2
	//	FROM dbo.Table1 t1
	//	INNER JOIN dbo.Table2 t2 ON t1.Id = t2.ForeignId
	//	GROUP BY col1
	//	ORDER BY col2 DESC;

	//	Use T-SQL: TOP(n) not LIMIT. Output ONLY SQL starting with SELECT.
	//	""";

	public async Task<string> GenerateStatementFromNlq(string naturalLanguageQuery, string jsonSchema, string llmMetadata)
	{
		// generate the system prompt if not already done
		if (string.IsNullOrWhiteSpace(SYSTEMPROMPT))
		{
			SYSTEMPROMPT = SqlPromptBuilder.CreateSystemPromptFromJson(jsonSchema, llmMetadata);
			//string filePath = "C:\\Users\\carl\\SystemPrompt.txt";
			//await File.WriteAllTextAsync(filePath, SYSTEMPROMPT);
		}

		// Combine all user prompts into a single, structured message for better performance
		var combinedPrompt = $"""
			SCHEMA (use these exact names):
			{jsonSchema}

			{(string.IsNullOrWhiteSpace(llmMetadata) ? "" : $"HINTS:\n{llmMetadata}\n")}
			QUERY: {naturalLanguageQuery}

			Return only T-SQL starting with SELECT.
			""";

		// Create optimized chat messages (single user message instead of multiple)
		var chatMessages = new ChatMessage[]
		{
			new ChatMessage(ChatRole.System, SYSTEMPROMPT),
			new ChatMessage(ChatRole.User, combinedPrompt)
		};

		// Configure chat options for better performance
		var chatOptions = new ChatOptions
		{
			MaxOutputTokens = _config.MaxTokens,
			Temperature = 0.15f
		};

		// Send request with timeout and performance options
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.RequestTimeoutSeconds));
		var response = await chatClient.GetResponseAsync(chatMessages, chatOptions, cts.Token);

		// Clean and return the generated statement
		var sqlStatement = response.Text.Trim();
		
		// Extract SQL from markdown code blocks if present
		var sqlMatch = System.Text.RegularExpressions.Regex.Match(
			sqlStatement, 
			@"```(?:sql)?\s*([\s\S]*?)```", 
			System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		if (sqlMatch.Success)
		{
			sqlStatement = sqlMatch.Groups[1].Value.Trim();
		}
		
		// If response has preamble, extract starting from SELECT
		var selectIndex = sqlStatement.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
		if (selectIndex > 0)
		{
			sqlStatement = sqlStatement[selectIndex..];
		}
		
		// Remove trailing commentary after the SQL ends (after final semicolon or common patterns)
		var lastSemicolon = sqlStatement.LastIndexOf(';');
		if (lastSemicolon > 0)
		{
			// Check if there's commentary after the semicolon
			var afterSemicolon = sqlStatement[(lastSemicolon + 1)..].Trim();
			if (!string.IsNullOrEmpty(afterSemicolon) && !afterSemicolon.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
			{
				sqlStatement = sqlStatement[..(lastSemicolon + 1)];
			}
		}
		
		// Remove SQL comments (-- and /* */)
		sqlStatement = System.Text.RegularExpressions.Regex.Replace(sqlStatement, @"--.*?$", "", 
			System.Text.RegularExpressions.RegexOptions.Multiline);
		sqlStatement = System.Text.RegularExpressions.Regex.Replace(sqlStatement, @"/\*[\s\S]*?\*/", "");
		
		// Format the SQL for readability
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
	/// </summary>
	private static string FormatSql(string sql)
	{
		if (string.IsNullOrWhiteSpace(sql))
			return sql;

		// Normalize whitespace - replace multiple spaces/tabs/newlines with single space
		sql = System.Text.RegularExpressions.Regex.Replace(sql.Trim(), @"\s+", " ");

		// Keywords that should start on a new line (not indented)
		var majorKeywords = new[] { "SELECT", "FROM", "WHERE", "GROUP BY", "HAVING", "ORDER BY", "UNION", "EXCEPT", "INTERSECT" };
		
		// Keywords that should start on a new line (slightly indented for joins)
		var joinKeywords = new[] { "INNER JOIN", "LEFT OUTER JOIN", "RIGHT OUTER JOIN", "FULL OUTER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "CROSS JOIN", "JOIN" };

		// Add newlines before major keywords
		foreach (var keyword in majorKeywords)
		{
			sql = System.Text.RegularExpressions.Regex.Replace(
				sql,
				$@"(?<!^)\s+({keyword})\b",
				$"\n$1",
				System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		}

		// Add newlines before JOIN keywords
		foreach (var keyword in joinKeywords)
		{
			sql = System.Text.RegularExpressions.Regex.Replace(
				sql,
				$@"\s+({keyword})\b",
				$"\n    $1",
				System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		}

		// Add newline and indent after SELECT (for columns)
		sql = System.Text.RegularExpressions.Regex.Replace(
			sql,
			@"(SELECT(?:\s+TOP\s*\(\d+\)|\s+DISTINCT)?)\s+",
			"$1\n    ",
			System.Text.RegularExpressions.RegexOptions.IgnoreCase);

		// Add newlines after commas in SELECT list (but not inside functions)
		sql = FormatSelectColumns(sql);

		// Add newline before ON in joins
		sql = System.Text.RegularExpressions.Regex.Replace(
			sql,
			@"\s+(ON)\s+",
			"\n        $1 ",
			System.Text.RegularExpressions.RegexOptions.IgnoreCase);

		// Clean up any double newlines
		sql = System.Text.RegularExpressions.Regex.Replace(sql, @"\n\s*\n", "\n");
		
		return sql.Trim();
	}

	/// <summary>
	/// Formats columns in SELECT clause, adding newlines after commas while respecting function parentheses.
	/// </summary>
	private static string FormatSelectColumns(string sql)
	{
		var result = new System.Text.StringBuilder();
		int parenDepth = 0;
		bool inSelectClause = false;

		for (int i = 0; i < sql.Length; i++)
		{
			char c = sql[i];

			// Track if we're in SELECT clause
			if (i + 6 <= sql.Length && sql.Substring(i, 6).Equals("SELECT", StringComparison.OrdinalIgnoreCase))
			{
				inSelectClause = true;
			}
			else if (i + 4 <= sql.Length && sql.Substring(i, 4).Equals("FROM", StringComparison.OrdinalIgnoreCase))
			{
				inSelectClause = false;
			}

			// Track parentheses depth
			if (c == '(')
				parenDepth++;
			else if (c == ')')
				parenDepth = Math.Max(0, parenDepth - 1);

			result.Append(c);

			// Add newline after comma if in SELECT clause and not inside parentheses
			if (c == ',' && inSelectClause && parenDepth == 0)
			{
				result.Append("\n    ");
				// Skip any whitespace after the comma
				while (i + 1 < sql.Length && char.IsWhiteSpace(sql[i + 1]))
					i++;
			}
		}

		return result.ToString();
	}


}