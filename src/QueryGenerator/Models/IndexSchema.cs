namespace SchemaGenerator.Models;

/// <summary>
/// Represents a database index for query optimization.
/// Helps LLM understand which columns are indexed for better query planning.
/// </summary>
public class IndexSchema
{
    /// <summary>
    /// Name of the index (e.g., "IX_Orders_CustomerId").
    /// </summary>
    public string IndexName { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this is a unique index (enforces uniqueness constraint).
    /// </summary>
    public bool IsUnique { get; set; }
    
    /// <summary>
    /// Whether this index is the primary key index.
    /// </summary>
    public bool IsPrimaryKey { get; set; }
    
    /// <summary>
    /// Type of index (e.g., "CLUSTERED", "NONCLUSTERED", "COLUMNSTORE").
    /// </summary>
    public string IndexType { get; set; } = string.Empty;
    
    /// <summary>
    /// Columns included in the index with their sort order.
    /// Order matters for composite indexes.
    /// </summary>
    public List<IndexColumnSchema> Columns { get; set; } = new();
}
