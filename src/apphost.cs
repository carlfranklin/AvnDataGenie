#:package CommunityToolkit.Aspire.Hosting.Ollama@13.0.1-beta.468
#:project AdminApp
#:sdk Aspire.AppHost.Sdk@13.1.0

var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama")
	.WithDataVolume()
	.WithLifetime(ContainerLifetime.Persistent);

var gemma = ollama.AddModel("gemma", "gemma3:1b");

var adminApp = builder.AddProject<Projects.AdminApp>("admin")
	.WithReference(gemma)
	.WaitFor(gemma)
	.WithExternalHttpEndpoints();

builder.Build().Run();
