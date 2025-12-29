#:package CommunityToolkit.Aspire.Hosting.Ollama@13.0.1-beta.468
#:sdk Aspire.AppHost.Sdk@13.1.0

var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama");

var gemma = ollama.AddModel("gemma", "gemma3:1b");

builder.Build().Run();
