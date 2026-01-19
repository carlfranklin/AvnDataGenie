namespace SchemaGenerator.Models;

/// <summary>
/// Represents a table's primary key constraint.
/// Identifies the unique identifier column(s) for each row.
/// </summary>
public class PrimaryKeySchema
{
    /// <summary>
    /// Name of the primary key constraint (e.g., "PK_Customers").
    /// </summary>
    public string ConstraintName { get; set; } = string.Empty;
    
    /// <summary>
    /// Column names that comprise the primary key.
    /// For composite keys, multiple columns are listed in key order.
    /// </summary>
    public List<string> Columns { get; set; } = new();
}
