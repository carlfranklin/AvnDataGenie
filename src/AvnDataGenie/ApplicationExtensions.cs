using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OllamaSharp;
using Microsoft.Extensions.Logging;

namespace AvnDataGenie;

public static class ApplicationExtensions
{

	private static readonly Dictionary<LlmType, Func<IServiceCollection, Configuration, IChatClient>> ChatClientFactories = new()
	{
		{
			LlmType.OpenAI, (services, config) =>
			{
				var client = new OpenAIClient(config.LlmApiKey);
				return client.GetChatClient(config.ModelName).AsIChatClient();
			}
		},
		{
			LlmType.AzureOpenAI, (services, config) =>
			{
				var client = new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(config.LlmEndpoint), new AzureKeyCredential(config.LlmApiKey));
				return client.GetChatClient(config.ModelName).AsIChatClient();
			}
		},
		{
			LlmType.Ollama, (services, config) =>
			{
				var ollamaClient = new OllamaApiClient(new Uri(config.LlmEndpoint), config.ModelName);
				return ollamaClient;
			}
		}
	};


	/// <summary>
	/// Adds AvnDataGenie services to the dependency injection container
	/// </summary>
	/// <param name="services">The service collection to add services to</param>
	/// <param name="configuration">The configuration instance to bind settings from</param>
	/// <param name="configureOptions">An optional action to configure AvnDataGenie options</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddAvnDataGenie(this IServiceCollection services, IConfiguration configuration, Action<Configuration>? configureOptions = null)
	{

		Console.WriteLine("Adding AvnDataGenie services to the service collection.");

		// Get and bind configuration from the "AvnDataGenie" section
		var config = new Configuration
		{
			LlmEndpoint = string.Empty,
			LlmApiKey = string.Empty,
			LlmType = LlmType.OpenAI,
			ModelName = string.Empty
		};
		configuration.GetSection("AvnDataGenie").Bind(config);
		
		// Apply additional configuration if provided
		if (configureOptions != null)
		{
			configureOptions(config);
		}

		// Bind configuration from the "AvnDataGenie" section for IOptions pattern
		services.Configure<Configuration>(configuration.GetSection("AvnDataGenie"));
		
		// Apply additional configuration if provided
		if (configureOptions != null)
		{
			services.Configure<Configuration>(configureOptions);
		}


		Console.WriteLine("Configuring IChatClient for AvnDataGenie.");
		
		Console.WriteLine($"Configuring IChatClient for LLM Type: {config.LlmType}, Endpoint: {config.LlmEndpoint}, Model: {config.ModelName}");
		
		services.AddScoped<IChatClient>(sp =>
		{
			if (ChatClientFactories.TryGetValue(config.LlmType, out var factory))
			{
				return factory(services, config);
			}
			else
			{
				throw new InvalidOperationException($"Unsupported LLM type: {config.LlmType}");
			}
		});
		
		// TODO: Add other AvnDataGenie services here as needed
		services.AddScoped<Generator>();

		return services;
	}
}
