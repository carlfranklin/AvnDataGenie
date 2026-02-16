
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;

namespace AvnDataGenie;

/// <summary>
/// Partial class extending Generator with GitHub Copilot SDK integration.
/// Manages CopilotClient lifecycle and session-based message exchange.
/// </summary>
public partial class Generator
{

	/// <summary>Copilot SDK client instance; lazily initialized on first Copilot request.</summary>
	private CopilotClient? _copilotClient;
	/// <summary>Active Copilot session holding system prompt context and conversation state.</summary>
	private CopilotSession? _copilotSession;
	
	/// <summary>
	/// Generates SQL using the GitHub Copilot SDK's session-based event-driven model.
	/// Unlike IChatClient providers, Copilot uses streaming events (message deltas,
	/// idle signals, errors) to deliver results asynchronously.
	/// </summary>
	/// <param name="naturalLanguageQuery">User's question in plain English</param>
	/// <param name="jsonSchema">JSON string containing database schema</param>
	/// <param name="llmMetadata">JSON string containing business rules and metadata</param>
	/// <returns>Cleaned and formatted T-SQL SELECT statement</returns>
	/// <exception cref="Exception">Thrown when Copilot session returns an error event</exception>
	/// <exception cref="OperationCanceledException">Thrown when request exceeds configured timeout</exception>
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

		// Create or reuse Copilot session — sessions persist system prompt context
		// across multiple queries for efficiency
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

		// Subscribe to Copilot session events using reactive pattern.
		// Events arrive asynchronously — we accumulate content and signal completion via TaskCompletionSource.
		using var subscription = _copilotSession.On(evt =>
		{
			try
			{
				switch (evt)
				{
					case AssistantMessageEvent msg:
						// Non-streaming path: full response delivered in a single event
						responseBuilder.Append(msg.Data.Content);
						logger.LogDebug("Received complete message from Copilot.");
					
					break;

					case AssistantMessageDeltaEvent delta:
						// Streaming path: response arrives in incremental chunks
						responseBuilder.Append(delta.Data.DeltaContent);
						break;

					case SessionIdleEvent:
						// Idle signal indicates the model has finished generating — resolve the task
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