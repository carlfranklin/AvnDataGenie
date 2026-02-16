namespace AvnDataGenie;

/// <summary>
/// The type of LLM service.  This helps MEAI know how to connect to it.
/// </summary>
public enum LlmType
		{
			/// <summary>Local or self-hosted Ollama instance for open-source models.</summary>
			Ollama,
			/// <summary>OpenAI API (e.g., GPT-4, GPT-4o) via API key authentication.</summary>
			OpenAI,
			/// <summary>Azure-hosted OpenAI deployment with Azure AD or key credential auth.</summary>
			AzureOpenAI,
			/// <summary>GitHub Copilot SDK using CopilotClient with session-based interaction.</summary>
			GitHubCopilot
		}
