namespace QueryGenerator.Models;

public class TableSchema
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public List<ColumnSchema> Columns { get; set; } = new();
    public PrimaryKeySchema? PrimaryKey { get; set; }
    public List<ForeignKeySchema> ForeignKeys { get; set; } = new();
    public List<IndexSchema> Indexes { get; set; } = new();
}
