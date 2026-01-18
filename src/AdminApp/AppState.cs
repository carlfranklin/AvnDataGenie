using QueryGenerator.Models;

namespace AdminApp;

public class AppState
{
	public string SaveFilePath { get; set; } = string.Empty;
	public string SchemaJson { get; set; } = string.Empty;
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
}

public enum SetupStep
{
	Connect = 1,
	Generate = 2,
	Configure = 3,
	Test = 4
}
