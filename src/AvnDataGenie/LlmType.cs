namespace AvnDataGenie;

/// <summary>
/// The type of LLM service.  This helps MEAI know how to connect to it.
/// </summary>
public enum LlmType
		{
			Ollama,
			OpenAI,
			AzureOpenAI,
			GitHubCopilot
		}
