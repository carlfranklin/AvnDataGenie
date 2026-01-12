using Azure.AI.Inference;
using Microsoft.Extensions.AI;
using OllamaSharp;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace AvnDataGenie;

public class Generator(Configuration config, IChatClient chatClient)
{

	public const string SYSTEMPROMPT = """
		You are an expert data query generator for Microsoft SQL Server. Given a natural language query, generate the appropriate data read statement 
		based on the provided JSON schema and LLM metadata.
		Respond only with the generated read statement without any additional explanations or text.
		""";
	
	public async Task<string> GenerateStatementFromNlq(string naturalLanguageQuery, string jsonSchema, string llmMetadata)
	{
		// Example prompt construction
		var prompt = $"Generate a read statement for the following query: {naturalLanguageQuery}";

		var schemaPrompt = $"The schema for the database is as follows: {jsonSchema}";

		var hintPrompt = $"Use the following LLM metadata to inform your response: {llmMetadata}";

		// Create chat message
		var chatMessages = new ChatMessage[]
		{
			new ChatMessage(ChatRole.System, SYSTEMPROMPT),
			new ChatMessage(ChatRole.User, schemaPrompt),
			new ChatMessage(ChatRole.User, hintPrompt),
			new ChatMessage(ChatRole.User, prompt)
		};

		// Send request to the chat client
		var response = await chatClient.GetResponseAsync(chatMessages);

		// Extract and return the generated statement
		return response.Text;
	}


}