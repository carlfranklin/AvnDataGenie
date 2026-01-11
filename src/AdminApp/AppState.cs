using QueryGenerator.Models;

namespace AdminApp;

public class AppState
{
	public string SaveFilePath { get; set; } = string.Empty;
	public string SchemaJson { get; set; } = string.Empty;
	public DatabaseSchema? DatabaseSchema { get; set; }
	public LlmConfiguration LlmConfiguration { get; set; } = new();
	public string ConfigSaveFilePath { get; set; } = string.Empty;
}
