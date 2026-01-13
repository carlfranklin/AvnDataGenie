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
