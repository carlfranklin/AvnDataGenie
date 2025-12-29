namespace QueryGenerator.Models;

public class DatabaseSchema
{
    public string DatabaseName { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public List<TableSchema> Tables { get; set; } = new();
}
