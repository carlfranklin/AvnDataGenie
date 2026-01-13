var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama")
	.WithGPUSupport()
	.WithDataVolume()
	.WithLifetime(ContainerLifetime.Persistent);

var model = ollama.AddModel("test-model", "qwen2.5-coder:1.5b");

var adminApp = builder.AddProject<Projects.AdminApp>("admin")
	.WithReference(model)
	.WaitFor(model)
	.WithExternalHttpEndpoints();
	
builder.Build().Run();
