
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;

namespace AvnDataGenie;

public partial class Generator
{

	private CopilotClient? _copilotClient;
	private CopilotSession? _copilotSession;
	
	/// <summary>
	/// Internal method to generate SQL using GitHub Copilot SDK.
	/// </summary>
	private async Task<string> GenerateWithCopilotAsync(string naturalLanguageQuery, string jsonSchema, string llmMetadata)
	{
		// Initialize Copilot client if not already created
		if (_copilotClient == null)
		{
			var clientOptions = new CopilotClientOptions
			{
				AutoStart = true,
				AutoRestart = true
			};

			_copilotClient = new CopilotClient(clientOptions);
			await _copilotClient.StartAsync();

			logger.LogInformation("GitHub Copilot client initialized and started.");
		}

		// Generate the system prompt if not already cached
		if (string.IsNullOrWhiteSpace(SYSTEMPROMPT))
		{
			SYSTEMPROMPT = SqlPromptBuilder.CreateSystemPromptFromJson(jsonSchema, llmMetadata);
			logger.LogInformation("System prompt generated and cached.");
			logger.LogDebug("System Prompt: {SystemPrompt}", SYSTEMPROMPT);
		}

		// Create or reuse session
		if (_copilotSession == null)
		{
			var sessionConfig = new SessionConfig
			{
				Model = _config.ModelName, // e.g., "gpt-5", "claude-sonnet-4.5"
				SystemMessage = new SystemMessageConfig
				{
					Mode = SystemMessageMode.Append,
					Content = SYSTEMPROMPT
				}
			};

			_copilotSession = await _copilotClient.CreateSessionAsync(sessionConfig);
			logger.LogInformation("Copilot session created with model: {Model}", _config.ModelName);
		}

		// Combine user query with hints
		var combinedPrompt = $"""
			{(string.IsNullOrWhiteSpace(llmMetadata) ? "" : $"HINTS:\n{llmMetadata}\n")}

			QUERY: {naturalLanguageQuery}

			Return only T-SQL starting with SELECT.
			""";

		// Set up completion tracking
		var responseBuilder = new System.Text.StringBuilder();
		var completionSource = new TaskCompletionSource<string>();
		var hasError = false;

		// Subscribe to session events
		using var subscription = _copilotSession.On(evt =>
		{
			try
			{
				switch (evt)
				{
					case AssistantMessageEvent msg:
						// Non-streaming: complete message received

						responseBuilder.Append(msg.Data.Content);
						logger.LogDebug("Received complete message from Copilot.");
					
					break;

					case AssistantMessageDeltaEvent delta:
						// Streaming: incremental chunks
						responseBuilder.Append(delta.Data.DeltaContent);
						break;

					case SessionIdleEvent:
						// Session finished processing
						completionSource.TrySetResult(responseBuilder.ToString());
						logger.LogDebug("Copilot session is idle, query complete.");
						break;

					case SessionErrorEvent err:
						logger.LogError("Copilot session error: {Error}", err.Data.Message);
						hasError = true;
						completionSource.TrySetException(new Exception($"Copilot error: {err.Data.Message}"));
						break;

					case ToolExecutionStartEvent toolStart:
						logger.LogDebug("Copilot tool execution started: {ToolId}", toolStart.Data.ToolCallId);
						break;

					case ToolExecutionCompleteEvent toolComplete:
						logger.LogDebug("Copilot tool execution completed: {ToolId}", toolComplete.Data.ToolCallId);
						break;
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error processing Copilot event");
				completionSource.TrySetException(ex);
			}
		});

			// Send the message
			await _copilotSession.SendAsync(new MessageOptions 
			{ 
				Prompt = combinedPrompt 
			});

			// Wait for completion with timeout
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.RequestTimeoutSeconds));
			cts.Token.Register(() => completionSource.TrySetCanceled());

			var sqlStatement = await completionSource.Task;

			if (hasError)
			{
				throw new Exception("Copilot returned an error during query generation.");
			}

			return CleanAndFormatSql(sqlStatement);

	}

}