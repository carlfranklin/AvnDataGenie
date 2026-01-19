namespace SchemaGenerator.Models;

/// <summary>
/// LLM configuration that provides business context and rules for SQL generation.
/// This metadata helps the LLM understand business terminology, required filters,
/// and how tables should be joined beyond what's in the database schema.
/// </summary>
public class LlmConfiguration
{
    /// <summary>
    /// Table-specific configurations with business names and descriptions.
    /// </summary>
    public List<TableConfiguration> TableConfigurations { get; set; } = new();
    
    /// <summary>
    /// Explicit join hints for relationships not captured by foreign keys.
    /// Useful for many-to-many relationships or business-logic joins.
    /// </summary>
    public List<JoinHint> JoinHints { get; set; } = new();
    
    /// <summary>
    /// Filters that must always be applied to certain tables.
    /// Example: Multi-tenant systems requiring TenantID filter, soft-delete flags.
    /// </summary>
    public List<RequiredFilter> RequiredFilters { get; set; } = new();
    
    /// <summary>
    /// Domain-specific business terminology mappings.
    /// Helps LLM understand industry jargon and company-specific terms.
    /// </summary>
    public List<BusinessTerm> BusinessTerms { get; set; } = new();
}

/// <summary>
/// Configuration for a specific table including friendly names and column metadata.
/// </summary>
public class TableConfiguration
{
    /// <summary>
    /// Database schema name (matches TableSchema.SchemaName).
    /// </summary>
    public string SchemaName { get; set; } = string.Empty;
    
    /// <summary>
    /// Table name (matches TableSchema.TableName).
    /// </summary>
    public string TableName { get; set; } = string.Empty;
    
    /// <summary>
    /// Business-friendly name for the table.
    /// Example: "SalesOrderHeader" -> "Sales Orders".
    /// </summary>
    public string? FriendlyName { get; set; }
    
    /// <summary>
    /// Plain English description of what this table contains.
    /// Example: "Contains all customer orders placed through the website".
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Comma-separated alternative names for this table.
    /// Example: "Orders, Purchases, Transactions".
    /// </summary>
    public string? Aliases { get; set; }
    
    /// <summary>
    /// Column-level configurations for this table.
    /// </summary>
    public List<ColumnConfiguration> ColumnConfigurations { get; set; } = new();
}

/// <summary>
/// Configuration for a specific column including business context.
/// </summary>
public class ColumnConfiguration
{
    /// <summary>
    /// Column name (matches ColumnSchema.ColumnName).
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;
    
    /// <summary>
    /// Business-friendly name for the column.
    /// Example: "CustID" -> "Customer ID".
    /// </summary>
    public string? FriendlyName { get; set; }
    
    /// <summary>
    /// Plain English description of what this column represents.
    /// Example: "Unique identifier for the customer account".
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Comma-separated alternative names for this column.
    /// Example: "Client ID, Account Number".
    /// </summary>
    public string? Aliases { get; set; }
    
    /// <summary>
    /// Indicates if this column contains Personally Identifiable Information.
    /// LLM should handle with care and may need to apply masking.
    /// </summary>
    public bool IsPii { get; set; }
    
    /// <summary>
    /// Indicates if this column has access restrictions.
    /// LLM should avoid including in queries without proper authorization.
    /// </summary>
    public bool IsRestricted { get; set; }
}

/// <summary>
/// Explicit hint for how to join two tables.
/// Used when foreign key relationships don't exist or need clarification.
/// </summary>
public class JoinHint
{
    /// <summary>
    /// Source table name in the join.
    /// </summary>
    public string FromTable { get; set; } = string.Empty;
    
    /// <summary>
    /// Column name in the source table.
    /// </summary>
    public string FromColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// Target table name in the join.
    /// </summary>
    public string ToTable { get; set; } = string.Empty;
    
    /// <summary>
    /// Column name in the target table.
    /// </summary>
    public string ToColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional context about when/how to use this join.
    /// Example: "Use for historical data only", "LEFT JOIN preferred".
    /// </summary>
    public string? Hint { get; set; }
}

/// <summary>
/// Filter that must always be applied when querying a specific table.
/// Critical for security, multi-tenancy, and soft-delete patterns.
/// </summary>
public class RequiredFilter
{
    /// <summary>
    /// Table name that requires this filter.
    /// </summary>
    public string TableName { get; set; } = string.Empty;
    
    /// <summary>
    /// Column name to filter on.
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of filter: "Tenant" (multi-tenancy), "SoftDelete" (IsDeleted=0), "Custom".
    /// </summary>
    public string FilterType { get; set; } = string.Empty;
    
    /// <summary>
    /// Explanation of why this filter is required.
    /// Example: "Ensures data isolation between tenants".
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Default filter value if applicable.
    /// Example: For soft delete: "0", for tenant: "@TenantID".
    /// </summary>
    public string? DefaultValue { get; set; }
}

/// <summary>
/// Business terminology mapping to help LLM understand domain-specific language.
/// </summary>
public class BusinessTerm
{
    /// <summary>
    /// The business term or jargon.
    /// Example: "Churn", "CLTV", "MRR".
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Clear definition of what this term means in the business context.
    /// Example: "Churn = Customer who cancelled their subscription".
    /// </summary>
    public string Definition { get; set; } = string.Empty;
    
    /// <summary>
    /// Example usage or calculation.
    /// Example: "Calculate as: COUNT(Customers WHERE Status='Cancelled')".
    /// </summary>
    public string? Example { get; set; }
}
