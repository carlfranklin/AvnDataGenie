using System.Diagnostics.Eventing.Reader;

var builder = DistributedApplication.CreateBuilder(args);

bool useCopilotCLI = false;

// var ollama = builder.AddOllama("ollama")
// //	.WithGPUSupport()
// 	.WithDataVolume()
// 	.WithLifetime(ContainerLifetime.Persistent);

// var model = ollama.AddModel("test-model", "qwen2.5-coder:1.5b");

var adminApp = builder.AddProject<Projects.AdminApp>("admin")
	.WithExternalHttpEndpoints();

if (useCopilotCLI)
{
	adminApp
		.WithEnvironment("AvnDataGenie__LlmType", "GitHubCopilot")
		.WithEnvironment("AvnDataGenie__ModelName", "gpt-5 mini");	
}
else if (!string.IsNullOrEmpty(builder.Configuration["AvnDataGenie:LlmEndpoint"]) || builder.Configuration["AvnDataGenie:LlmType"] == "GitHubCopilot")
{

	// Read Azure OpenAI configuration from UserSecrets
	var llmEndpoint = builder.Configuration["AvnDataGenie:LlmEndpoint"];
	var llmApiKey = builder.Configuration["AvnDataGenie:LlmApiKey"];
	var llmType = builder.Configuration["AvnDataGenie:LlmType"];
	var modelName = builder.Configuration["AvnDataGenie:ModelName"];

	adminApp.WithEnvironment("AvnDataGenie__LlmEndpoint", llmEndpoint)
		.WithEnvironment("AvnDataGenie__LlmApiKey", llmApiKey)
		.WithEnvironment("AvnDataGenie__LlmType", llmType)
		.WithEnvironment("AvnDataGenie__ModelName", modelName);

}
	
builder.Build().Run();
