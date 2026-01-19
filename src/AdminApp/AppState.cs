using System.Text.Json;
using Microsoft.JSInterop;
using SchemaGenerator.Models;

namespace AdminApp;

/// <summary>
/// Central application state manager that holds all configuration and runtime data.
/// Scoped service - one instance per SignalR circuit/user session.
/// </summary>
public class AppState
{
	// Browser storage integration
	private IJSRuntime? _jsRuntime;
	private const string StorageKey = "AppState_QueryHistory";
	private bool _isInitialized = false;

	/// <summary>
	/// Initialize the AppState with JSRuntime for browser localStorage access.
	/// Only initializes once per instance to prevent duplicate initialization.
	/// </summary>
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

	/// <summary>
	/// Load query history from browser's localStorage.
	/// Called once when the app first renders.
	/// </summary>
	public async Task LoadStateAsync()
	{
		if (_jsRuntime == null) 
		{
			Console.WriteLine("AppState: Cannot load - JSRuntime not initialized");
			return;
		}

		try
		{
			// Retrieve JSON string from localStorage
			var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
			Console.WriteLine($"Loading state... JSON length: {json?.Length ?? 0}");
			
			if (!string.IsNullOrEmpty(json))
			{
				// Deserialize query history list
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

	/// <summary>
	/// Save query history to browser's localStorage.
	/// Called whenever query history is modified.
	/// </summary>
	public async Task SaveStateAsync()
	{
		if (_jsRuntime == null) 
		{
			Console.WriteLine("AppState: Cannot save - JSRuntime not initialized");
			return;
		}

		try
		{
			// Serialize only the query history (lightweight)
			var json = JsonSerializer.Serialize(QueryHistory);
			Console.WriteLine($"Saving query history with {QueryHistory.Count} queries, JSON length: {json.Length}");
			
			// Store in browser's localStorage
			await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
			Console.WriteLine("Query history saved successfully.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error saving state: {ex.Message}");
		}
	}

	// File paths for persisting configuration to disk
	public string SaveFilePath { get; set; } = string.Empty;
	public string ConfigSaveFilePath { get; set; } = string.Empty;
	
	// Database connection and schema
	public string SchemaJson { get; set; } = string.Empty;  // Raw JSON of database schema
	public string ConnectionString { get; set; } = string.Empty;  // SQL Server connection string
	public DatabaseSchema? DatabaseSchema { get; set; }  // Parsed database schema object
	
	// LLM configuration for query generation
	public LlmConfiguration LlmConfiguration { get; set; } = new();

	// File metadata for tracking changes
	public DateTime? SchemaFileLastModified { get; set; }
	public DateTime? ConfigFileLastModified { get; set; }
	
	// Computed properties for file existence checks
	public bool SchemaFileExists => !string.IsNullOrEmpty(SaveFilePath) && File.Exists(SaveFilePath);
	public bool ConfigFileExists => !string.IsNullOrEmpty(ConfigSaveFilePath) && File.Exists(ConfigSaveFilePath);
	public string SchemaFileName => string.IsNullOrEmpty(SaveFilePath) ? "Not configured" : Path.GetFileName(SaveFilePath);
	public string ConfigFileName => string.IsNullOrEmpty(ConfigSaveFilePath) ? "Not configured" : Path.GetFileName(ConfigSaveFilePath);

	// Test/runtime tracking
	public bool TestCompleted { get; set; } = false;  // Has user tested query generation?
	public DateTime? LastTestRun { get; set; }  // When was last test performed?
	public int? LastTestQueryCount { get; set; }  // How many queries have been tested?

	// Setup progress indicators
	public bool HasConnectionString => !string.IsNullOrEmpty(SchemaJson);
	public bool HasSchema => DatabaseSchema != null;
	public bool HasConfiguration => LlmConfiguration.TableConfigurations.Any();
	public bool ConfigurationSaved { get; set; } = false;

	/// <summary>
	/// Determines the current step in the setup wizard based on completed items.
	/// </summary>
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

	// Configuration summary statistics
	public int ConfiguredTablesCount => LlmConfiguration.TableConfigurations.Count;
	public int JoinHintsCount => LlmConfiguration.JoinHints.Count;
	public int RequiredFiltersCount => LlmConfiguration.RequiredFilters.Count;
	public int BusinessTermsCount => LlmConfiguration.BusinessTerms.Count;

	/// <summary>
	/// Refresh file metadata by checking disk for file existence and modification times.
	/// </summary>
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

	// Event notification for UI updates
	public event Action? OnChange;
	
	/// <summary>
	/// Notify subscribers that state has changed (triggers UI re-render).
	/// </summary>
	public void NotifyStateChanged() => OnChange?.Invoke();
	
	// Query generation results
	public string SQLString { get; set; } = string.Empty;  // Generated SQL query
	public string NaturalLanguageQuery { get; set; } = string.Empty;  // Original NL query

	// Query history (persisted to localStorage)
	public List<string> QueryHistory { get; set; } = new();
	
	/// <summary>
	/// Add a query to the history list, maintaining most recent first order.
	/// Automatically saves to localStorage.
	/// </summary>
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
		
		// Keep only the last 20 queries to prevent unbounded growth
		if (QueryHistory.Count > 20)
		{
			QueryHistory.RemoveAt(QueryHistory.Count - 1);
		}
		
		Console.WriteLine($"AppState: Query history updated, count: {QueryHistory.Count}");
		
		// Persist to browser storage
		await SaveStateAsync();
	}

}

/// <summary>
/// Setup wizard step enumeration.
/// </summary>
public enum SetupStep
{
	Connect = 1,
	Generate = 2,
	Configure = 3,
	Test = 4
}
