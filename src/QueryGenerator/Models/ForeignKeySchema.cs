namespace QueryGenerator.Models;

public class ForeignKeySchema
{
    public string ConstraintName { get; set; } = string.Empty;
    public string ReferencedSchema { get; set; } = string.Empty;
    public string ReferencedTable { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public List<string> ReferencedColumns { get; set; } = new();
}
