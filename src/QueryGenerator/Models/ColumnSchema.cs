namespace SchemaGenerator.Models;

/// <summary>
/// Represents a single column's metadata within a database table.
/// Contains data type, constraints, and LLM-friendly aliases.
/// </summary>
public class ColumnSchema
{
    /// <summary>
    /// Column name as defined in the database (e.g., "CustomerID", "FirstName").
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;
    
    /// <summary>
    /// SQL Server data type (e.g., "varchar", "int", "datetime2", "decimal").
    /// </summary>
    public string DataType { get; set; } = string.Empty;
    
    /// <summary>
    /// Alternative names for this column that the LLM can recognize.
    /// Example: "CustomerID" might have aliases ["ClientID", "CustID"].
    /// </summary>
    public string[] Aliases { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Maximum character length for string types (varchar, nvarchar, char).
    /// Null for non-string types or MAX columns.
    /// </summary>
    public int? MaxLength { get; set; }
    
    /// <summary>
    /// Total number of digits for numeric types (decimal, numeric).
    /// Null for non-numeric types.
    /// </summary>
    public byte? NumericPrecision { get; set; }
    
    /// <summary>
    /// Number of decimal places for numeric types (decimal, numeric).
    /// Null for non-numeric types.
    /// </summary>
    public int? NumericScale { get; set; }
    
    /// <summary>
    /// Whether NULL values are allowed in this column.
    /// Important for generating correct WHERE clauses and handling missing data.
    /// </summary>
    public bool IsNullable { get; set; }
    
    /// <summary>
    /// Default value expression if one is defined.
    /// Example: "getdate()", "0", "'N/A'".
    /// </summary>
    public string? DefaultValue { get; set; }
    
    /// <summary>
    /// Column's position in the table (1-based).
    /// Used for maintaining consistent column ordering.
    /// </summary>
    public int OrdinalPosition { get; set; }
}
