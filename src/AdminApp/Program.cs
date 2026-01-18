using AdminApp;
using AdminApp.Components;
using AvnDataGenie;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

builder.Services.AddScoped<AppState>();

Console.WriteLine("Configuring AvnDataGenie integration...");

// Test AvnDataGenie integration
builder.Services.AddAvnDataGenie(builder.Configuration, config =>
{

	if (!string.IsNullOrEmpty(builder.Configuration["connectionstrings:test-model"])) {
	Console.WriteLine("Configuring AvnDataGenie with local Ollama settings.");

	// You can customize the configuration here if needed
	config.LlmType = LlmType.Ollama;

	// use a regex to extract the URL from a connectionstring in this format:  "Endpoint=http://localhost:60581;Model=gemma3:1b"
	var reAspireUrlExtract = new System.Text.RegularExpressions.Regex(@"Endpoint=(?<url>[^;]+);Model=(?<model>.+)");
	var match = reAspireUrlExtract.Match(builder.Configuration["connectionstrings:test-model"] ??"");
	if (match.Success)
	{
		config.LlmEndpoint = match.Groups["url"].Value;
		config.ModelName = match.Groups["model"].Value;
	}
	else
	{	
		config.LlmEndpoint = builder.Configuration["connectionstrings:test-model"] ?? "http://localhost:11434"; // Example endpoint for Ollama
		config.ModelName = "gemma3:1b";
	}
	
	// Performance optimizations for Ollama
	config.RequestTimeoutSeconds = 60; // Shorter timeout for faster failures
	config.MaxTokens = 50000; // SQL statements are usually short
	config.Temperature = 0.1f; // Very low temperature for consistent, deterministic SQL output

	}

});

var app = builder.Build();

// Log startup for telemetry verification
var logger = app.Logger;
logger.LogInformation("AdminApp starting up - telemetry should be visible in Aspire Dashboard");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);

// Check both configuration and environment variable
var otlpFromConfig = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
var otlpFromEnv = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
logger.LogInformation("OTEL_EXPORTER_OTLP_ENDPOINT from Configuration: {ConfigEndpoint}", otlpFromConfig ?? "NOT SET");
logger.LogInformation("OTEL_EXPORTER_OTLP_ENDPOINT from Environment: {EnvEndpoint}", otlpFromEnv ?? "NOT SET");
logger.LogInformation("OTEL_SERVICE_NAME: {ServiceName}", Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "NOT SET");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error", createScopeForErrors: true);
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapDefaultEndpoints();

app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

app.Run();
