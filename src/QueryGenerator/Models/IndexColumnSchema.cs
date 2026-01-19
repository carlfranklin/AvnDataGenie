namespace SchemaGenerator.Models;

/// <summary>
/// Represents a single column within a database index.
/// Specifies the column name and sort direction for index operations.
/// </summary>
public class IndexColumnSchema
{
    /// <summary>
    /// Name of the column included in the index.
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;
    
    /// <summary>
    /// Sort direction for this column in the index.
    /// True = descending (DESC), False = ascending (ASC).
    /// </summary>
    public bool IsDescending { get; set; }
}
