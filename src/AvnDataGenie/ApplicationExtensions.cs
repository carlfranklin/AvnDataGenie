using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OllamaSharp;

namespace AvnDataGenie;

public static class ApplicationExtensions
{
	/// <summary>
	/// Adds AvnDataGenie services to the dependency injection container
	/// </summary>
	/// <param name="services">The service collection to add services to</param>
	/// <param name="configuration">The configuration instance to bind settings from</param>
	/// <param name="configureOptions">An optional action to configure AvnDataGenie options</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddAvnDataGenie(this IServiceCollection services, IConfiguration configuration, Action<Configuration>? configureOptions = null)
	{
		// Bind configuration from the "AvnDataGenie" section
		services.Configure<Configuration>(configuration.GetSection("AvnDataGenie"));
		
		// Apply additional configuration if provided
		if (configureOptions != null)
		{
			services.Configure<Configuration>(configureOptions);
		}

		// Add the Configuration as a singleton service
		services.AddSingleton<Configuration>(provider =>
		{
			var options = provider.GetService<IOptionsMonitor<Configuration>>();
			return options?.CurrentValue ?? new Configuration 
			{ 
				LlmEndpoint = string.Empty,
				LlmApiKey = string.Empty,
				LlmType = LlmType.OpenAI, // Default value, adjust as needed
				ModelName = string.Empty
			};
		});

		// Register IChatClient based on the configured LLM type using AddChatClient
		services.AddChatClient(services =>
		{
			var options = services.GetRequiredService<IOptionsMonitor<Configuration>>();
			var config = options.CurrentValue;
			
			return config.LlmType switch
			{
				LlmType.OpenAI => 
					new OpenAIClient(config.LlmApiKey)
						.GetChatClient(config.ModelName)
						.AsIChatClient(),
					
				LlmType.AzureOpenAI => 
					new AzureOpenAIClient(new Uri(config.LlmEndpoint), new AzureKeyCredential(config.LlmApiKey))
						.GetChatClient(config.ModelName)
						.AsIChatClient(),
					
				LlmType.Ollama => 
					new OllamaApiClient(new Uri(config.LlmEndpoint), config.ModelName),
					
				_ => throw new InvalidOperationException($"Unsupported LLM type: {config.LlmType}")
			};
		});

		// TODO: Add other AvnDataGenie services here as needed
		// services.AddScoped<IYourService, YourService>();

		return services;
	}
}
