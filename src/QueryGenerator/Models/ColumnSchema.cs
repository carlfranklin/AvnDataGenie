namespace QueryGenerator.Models;

public class ColumnSchema
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public int? MaxLength { get; set; }
    public byte? NumericPrecision { get; set; }
    public int? NumericScale { get; set; }
    public bool IsNullable { get; set; }
    public string? DefaultValue { get; set; }
    public int OrdinalPosition { get; set; }
}
