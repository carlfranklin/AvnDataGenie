using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OllamaSharp;
using Microsoft.Extensions.Logging;

namespace AvnDataGenie;

public static class ApplicationExtensions
{
	/// <summary>
	/// Builds a chat client with conditional distributed caching if available
	/// </summary>
	private static IChatClient BuildChatClientWithAdditionalConfiguration(IChatClient baseClient, IServiceProvider serviceProvider)
	{
		var builder = new ChatClientBuilder(baseClient);
		
		// Only add caching if IDistributedCache is available
		if (serviceProvider.GetService<IDistributedCache>() != null)
		{
			builder = builder.UseDistributedCache();
		}
		
		return builder.Build(serviceProvider);
	}

	private static readonly Dictionary<LlmType, Func<IServiceProvider, Configuration, IChatClient>> ChatClientFactories = new()
	{
		{
			LlmType.OpenAI, (serviceProvider, config) =>
			{
				var client = new OpenAIClient(config.LlmApiKey);
				var baseClient = client.GetChatClient(config.ModelName).AsIChatClient();
				return BuildChatClientWithAdditionalConfiguration(baseClient, serviceProvider);
			}
		},
		{
			LlmType.AzureOpenAI, (serviceProvider, config) =>
			{
				var client = new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(config.LlmEndpoint), new AzureKeyCredential(config.LlmApiKey));
				var baseClient = client.GetChatClient(config.ModelName).AsIChatClient();
				return BuildChatClientWithAdditionalConfiguration(baseClient, serviceProvider);
			}
		},
		{
			LlmType.Ollama, (serviceProvider, config) =>
			{
				var baseClient = new OllamaApiClient(new Uri(config.LlmEndpoint), config.ModelName);
				return BuildChatClientWithAdditionalConfiguration(baseClient, serviceProvider);
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
		
		// Add distributed caching for chat client responses if not already configured
		if (!services.Any(service => service.ServiceType == typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache)))
		{
			services.AddDistributedMemoryCache();
		}
		
		services.AddScoped<IChatClient>(sp =>
		{
			if (ChatClientFactories.TryGetValue(config.LlmType, out var factory))
			{
				return factory(sp, config);
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
