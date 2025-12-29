namespace QueryGenerator.Models;

public class IndexSchema
{
    public string IndexName { get; set; } = string.Empty;
    public bool IsUnique { get; set; }
    public bool IsPrimaryKey { get; set; }
    public string IndexType { get; set; } = string.Empty;
    public List<IndexColumnSchema> Columns { get; set; } = new();
}
