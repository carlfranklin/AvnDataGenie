using Azure.AI.Inference;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace AvnDataGenie;

public class Generator(IOptions<Configuration> config, IChatClient chatClient)
{
	private readonly Configuration _config = config.Value;

	public const string SYSTEMPROMPT = """
		You are an expert SQL Server query generator. Generate ONLY the SQL read statement based on the provided schema and query. 
		No explanations, comments, or additional text - just the SQL statement.
		""";
	
	public async Task<string> GenerateStatementFromNlq(string naturalLanguageQuery, string jsonSchema, string llmMetadata)
	{
		// Combine all user prompts into a single, structured message for better performance
		var combinedPrompt = $"""
			DATABASE SCHEMA:
			{jsonSchema}

			LLM METADATA:
			{llmMetadata}

			QUERY REQUEST:
			{naturalLanguageQuery}

			Generate the SQL read statement:
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
			Temperature = _config.Temperature
		};

		// Send request with timeout and performance options
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.RequestTimeoutSeconds));
		var response = await chatClient.GetResponseAsync(chatMessages, chatOptions, cts.Token);

		// Clean and return the generated statement
		var sqlStatement = response.Text.Trim();
		
		// Remove any potential extra formatting or comments that might slip through
		if (sqlStatement.StartsWith("```sql"))
		{
			sqlStatement = sqlStatement.Replace("```sql", "").Replace("```", "").Trim();
		}
		
		return sqlStatement;

	}


}