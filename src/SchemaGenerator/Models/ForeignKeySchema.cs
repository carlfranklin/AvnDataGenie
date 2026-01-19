namespace SchemaGenerator.Models;

/// <summary>
/// Represents a foreign key constraint between two tables.
/// Defines the relationship structure for joins and data integrity.
/// </summary>
public class ForeignKeySchema
{
    /// <summary>
    /// Name of the foreign key constraint (e.g., "FK_Orders_Customers").
    /// </summary>
    public string ConstraintName { get; set; } = string.Empty;
    
    /// <summary>
    /// Schema name of the referenced (parent) table.
    /// </summary>
    public string ReferencedSchema { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the referenced (parent) table.
    /// </summary>
    public string ReferencedTable { get; set; } = string.Empty;
    
    /// <summary>
    /// Column names in the child table that form the foreign key.
    /// For composite keys, multiple columns are listed in order.
    /// </summary>
    public List<string> Columns { get; set; } = new();
    
    /// <summary>
    /// Column names in the parent table that are referenced.
    /// Typically the parent table's primary key columns.
    /// </summary>
    public List<string> ReferencedColumns { get; set; } = new();
}
