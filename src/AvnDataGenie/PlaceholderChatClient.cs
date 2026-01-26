using Microsoft.Extensions.AI;

namespace AvnDataGenie;

/// <summary>
/// Placeholder IChatClient for GitHub Copilot which uses CopilotClient directly.
/// This client should never be invoked.
/// </summary>
internal sealed class PlaceholderChatClient : IChatClient
{
	public ChatClientMetadata Metadata => new(providerName: "GitHubCopilot", providerUri: null);

	public Task<ChatResponse> GetResponseAsync(
		IEnumerable<ChatMessage> chatMessages,
		ChatOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		throw new InvalidOperationException(
			"GitHub Copilot uses CopilotClient directly in the Generator class. " +
			"IChatClient should not be invoked when LlmType is GitHubCopilot.");
	}

	public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
		IEnumerable<ChatMessage> chatMessages,
		ChatOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		throw new InvalidOperationException(
			"GitHub Copilot uses CopilotClient directly in the Generator class. " +
			"IChatClient should not be invoked when LlmType is GitHubCopilot.");
	}

	public object? GetService(Type serviceType, object? serviceKey = null) => null;

	public void Dispose() { }
}
