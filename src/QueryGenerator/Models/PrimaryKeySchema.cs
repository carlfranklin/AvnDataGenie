namespace QueryGenerator.Models;

public class PrimaryKeySchema
{
    public string ConstraintName { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
}
