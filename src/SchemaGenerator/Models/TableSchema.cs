namespace SchemaGenerator.Models;

/// <summary>
/// Represents a database table's structure including columns, keys, and indexes.
/// Used as metadata for LLM-based query generation.
/// </summary>
public class TableSchema
{
    /// <summary>
    /// Database schema name (e.g., "dbo", "sales", "hr").
    /// </summary>
    public string SchemaName { get; set; } = string.Empty;
    
    /// <summary>
    /// Table name (e.g., "Customers", "Orders", "Products").
    /// </summary>
    public string TableName { get; set; } = string.Empty;
    
    /// <summary>
    /// Alternative names for this table that the LLM can recognize.
    /// Example: "Customer" table might have aliases ["Clients", "Buyers"].
    /// </summary>
    public string[] Aliases { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// All columns in this table with their data types and constraints.
    /// </summary>
    public List<ColumnSchema> Columns { get; set; } = new();
    
    /// <summary>
    /// Primary key definition if one exists.
    /// Null for tables without primary keys.
    /// </summary>
    public PrimaryKeySchema? PrimaryKey { get; set; }
    
    /// <summary>
    /// Foreign key relationships to other tables.
    /// Used by LLM to understand join requirements.
    /// </summary>
    public List<ForeignKeySchema> ForeignKeys { get; set; } = new();
    
    /// <summary>
    /// Indexes defined on this table.
    /// Helps LLM optimize queries for performance.
    /// </summary>
    public List<IndexSchema> Indexes { get; set; } = new();
}
