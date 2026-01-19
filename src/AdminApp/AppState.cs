using System.Text.Json;
using Microsoft.JSInterop;
using QueryGenerator.Models;

namespace AdminApp;

public class AppState
{
	private IJSRuntime? _jsRuntime;
	private const string StorageKey = "AppState_QueryHistory";
	private bool _isInitialized = false;

	public void Initialize(IJSRuntime jsRuntime)
	{
		if (_isInitialized)
		{
			Console.WriteLine("AppState: Already initialized, skipping");
			return;
		}
		
		Console.WriteLine("AppState: Initializing with JSRuntime");
		_jsRuntime = jsRuntime;
		_isInitialized = true;
	}

	public async Task LoadStateAsync()
	{
		if (_jsRuntime == null) 
		{
			Console.WriteLine("AppState: Cannot load - JSRuntime not initialized");
			return;
		}

		try
		{
			var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
			Console.WriteLine($"Loading state... JSON length: {json?.Length ?? 0}");
			
			if (!string.IsNullOrEmpty(json))
			{
				var history = JsonSerializer.Deserialize<List<string>>(json);
				if (history != null)
				{
					QueryHistory = history;
					Console.WriteLine($"State loaded successfully. Query history count: {QueryHistory.Count}");
				}
			}
			else
			{
				Console.WriteLine("No saved state found.");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error loading state: {ex.Message}");
		}
	}

	public async Task SaveStateAsync()
	{
		if (_jsRuntime == null) 
		{
			Console.WriteLine("AppState: Cannot save - JSRuntime not initialized");
			return;
		}

		try
		{
			var json = JsonSerializer.Serialize(QueryHistory);
			Console.WriteLine($"Saving query history with {QueryHistory.Count} queries, JSON length: {json.Length}");
			await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
			Console.WriteLine("Query history saved successfully.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error saving state: {ex.Message}");
		}
	}

	public string SaveFilePath { get; set; } = string.Empty;
	public string SchemaJson { get; set; } = string.Empty;
	public string ConnectionString { get; set; } = string.Empty;
	public DatabaseSchema? DatabaseSchema { get; set; }
	public LlmConfiguration LlmConfiguration { get; set; } = new();
	public string ConfigSaveFilePath { get; set; } = string.Empty;

	// File status tracking
	public DateTime? SchemaFileLastModified { get; set; }
	public DateTime? ConfigFileLastModified { get; set; }
	public bool SchemaFileExists => !string.IsNullOrEmpty(SaveFilePath) && File.Exists(SaveFilePath);
	public bool ConfigFileExists => !string.IsNullOrEmpty(ConfigSaveFilePath) && File.Exists(ConfigSaveFilePath);
	public string SchemaFileName => string.IsNullOrEmpty(SaveFilePath) ? "Not configured" : Path.GetFileName(SaveFilePath);
	public string ConfigFileName => string.IsNullOrEmpty(ConfigSaveFilePath) ? "Not configured" : Path.GetFileName(ConfigSaveFilePath);

	// Test status tracking
	public bool TestCompleted { get; set; } = false;
	public DateTime? LastTestRun { get; set; }
	public int? LastTestQueryCount { get; set; }

	// Setup progress tracking
	public bool HasConnectionString => !string.IsNullOrEmpty(SchemaJson);
	public bool HasSchema => DatabaseSchema != null;
	public bool HasConfiguration => LlmConfiguration.TableConfigurations.Any();
	public bool ConfigurationSaved { get; set; } = false;

	public SetupStep CurrentStep
	{
		get
		{
			if (!HasSchema) return SetupStep.Connect;
			if (!HasConfiguration) return SetupStep.Configure;
			if (!ConfigurationSaved) return SetupStep.Configure;
			return SetupStep.Test;
		}
	}

	// Configuration summary helpers
	public int ConfiguredTablesCount => LlmConfiguration.TableConfigurations.Count;
	public int JoinHintsCount => LlmConfiguration.JoinHints.Count;
	public int RequiredFiltersCount => LlmConfiguration.RequiredFilters.Count;
	public int BusinessTermsCount => LlmConfiguration.BusinessTerms.Count;

	public void RefreshFileStatus()
	{
		if (SchemaFileExists)
		{
			SchemaFileLastModified = File.GetLastWriteTime(SaveFilePath);
		}
		if (ConfigFileExists)
		{
			ConfigFileLastModified = File.GetLastWriteTime(ConfigSaveFilePath);
		}
	}

	public event Action? OnChange;
	public void NotifyStateChanged() => OnChange?.Invoke();
	
	public string SQLString { get; set; } = string.Empty;
	public string NaturalLanguageQuery { get; set; } = string.Empty;

	// Query history tracking
	public List<string> QueryHistory { get; set; } = new();
	
	public async Task AddQueryToHistoryAsync(string query)
	{
		Console.WriteLine($"AppState: AddQueryToHistoryAsync called with: {query}");
		
		if (string.IsNullOrWhiteSpace(query))
		{
			Console.WriteLine("AppState: Query is null/whitespace, skipping");
			return;
		}
		
		// Remove if already exists to avoid duplicates
		QueryHistory.Remove(query);
		
		// Add to the beginning of the list (most recent first)
		QueryHistory.Insert(0, query);
		
		// Keep only the last 20 queries
		if (QueryHistory.Count > 20)
		{
			QueryHistory.RemoveAt(QueryHistory.Count - 1);
		}
		
		Console.WriteLine($"AppState: Query history updated, count: {QueryHistory.Count}");
		await SaveStateAsync();
	}

}

public enum SetupStep
{
	Connect = 1,
	Generate = 2,
	Configure = 3,
	Test = 4
}
