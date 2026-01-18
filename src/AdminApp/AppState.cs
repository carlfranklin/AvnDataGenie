using QueryGenerator.Models;

namespace AdminApp;

public class AppState
{
	public string SaveFilePath { get; set; } = string.Empty;
	public string SchemaJson { get; set; } = string.Empty;
	public DatabaseSchema? DatabaseSchema { get; set; }
	public LlmConfiguration LlmConfiguration { get; set; } = new();
	public string ConfigSaveFilePath { get; set; } = string.Empty;

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
