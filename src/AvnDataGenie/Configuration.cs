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

}