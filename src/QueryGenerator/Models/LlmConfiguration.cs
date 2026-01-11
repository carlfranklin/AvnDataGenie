namespace QueryGenerator.Models;

public class LlmConfiguration
{
    public List<TableConfiguration> TableConfigurations { get; set; } = new();
    public List<JoinHint> JoinHints { get; set; } = new();
    public List<RequiredFilter> RequiredFilters { get; set; } = new();
    public List<BusinessTerm> BusinessTerms { get; set; } = new();
}

public class TableConfiguration
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string? FriendlyName { get; set; }
    public string? Description { get; set; }
    public string? Aliases { get; set; }
    public List<ColumnConfiguration> ColumnConfigurations { get; set; } = new();
}

public class ColumnConfiguration
{
    public string ColumnName { get; set; } = string.Empty;
    public string? FriendlyName { get; set; }
    public string? Description { get; set; }
    public string? Aliases { get; set; }
    public bool IsPii { get; set; }
    public bool IsRestricted { get; set; }
}

public class JoinHint
{
    public string FromTable { get; set; } = string.Empty;
    public string FromColumn { get; set; } = string.Empty;
    public string ToTable { get; set; } = string.Empty;
    public string ToColumn { get; set; } = string.Empty;
    public string? Hint { get; set; }
}

public class RequiredFilter
{
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string FilterType { get; set; } = string.Empty; // "Tenant", "SoftDelete", "Custom"
    public string? Description { get; set; }
    public string? DefaultValue { get; set; }
}

public class BusinessTerm
{
    public string Name { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string? Example { get; set; }
}
