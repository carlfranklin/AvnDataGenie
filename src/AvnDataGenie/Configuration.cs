namespace AvnDataGenie;

/// <summary>
/// This class contains configuration settings for the AvnDataGenie runtime
/// </summary>
public class Configuration
{

		/// <summary>
		/// The URL of the LLM service
		/// </summary>
		public required string LlmEndpoint {get; set;}

		/// <summary>
		/// The API key for the LLM service	
		/// </summary>
		public required string LlmApiKey {get; set;} = string.Empty;

		public required LlmType LlmType {get; set;}

		/// <summary>
		/// The model name to use on the LLM service
		/// </summary>
		public required string ModelName {get; set;} = string.Empty;

		/// <summary>
		/// Request timeout in seconds for LLM calls (default: 120 seconds)
		/// </summary>
		public int RequestTimeoutSeconds { get; set; } = 120;

		/// <summary>
		/// Maximum tokens for the response (helps control response length and speed)
		/// </summary>
		public int? MaxTokens { get; set; } = 1000;

		/// <summary>
		/// Temperature for response randomness (0.0-1.0, lower = more deterministic/faster)
		/// </summary>
		public float Temperature { get; set; } = 0.1f;

}