using AdminApp;
using AdminApp.Components;
using AvnDataGenie;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

builder.Services.AddScoped<AppState>();

builder.Services.AddSingleton<QueryGenerator.Generator>();

Console.WriteLine("Configuring AvnDataGenie integration...");

// Test AvnDataGenie integration
builder.Services.AddAvnDataGenie(builder.Configuration, config =>
{

	Console.WriteLine("Configuring AvnDataGenie with Ollama settings.");

	// You can customize the configuration here if needed
	config.LlmType = LlmType.Ollama;

	// use a regex to extract the URL from a connectionstring in this format:  "Endpoint=http://localhost:60581;Model=gemma3:1b"
	var reAspireUrlExtract = new System.Text.RegularExpressions.Regex(@"Endpoint=(?<url>[^;]+);Model=(?<model>.+)");
	var match = reAspireUrlExtract.Match(builder.Configuration["connectionstrings:gemma"] ??"");
	if (match.Success)
	{
		config.LlmEndpoint = match.Groups["url"].Value;
		config.ModelName = match.Groups["model"].Value;
	}
	else
	{	
		config.LlmEndpoint = builder.Configuration["connectionstrings:gemma"]; // Example endpoint for Ollama
		config.ModelName = "gemma3:1b";
	}
	
	// Performance optimizations for Ollama
	config.RequestTimeoutSeconds = 60; // Shorter timeout for faster failures
	config.MaxTokens = 500; // SQL statements are usually short
	config.Temperature = 0.1f; // Very low temperature for consistent, deterministic SQL output
});

var app = builder.Build();

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
