using Microsoft.Data.SqlClient;
using SchemaGenerator.Models;

namespace SchemaGenerator;

/// <summary>
/// Database schema extraction engine that reads SQL Server metadata.
/// Queries system catalogs and information schema views to build a complete
/// DatabaseSchema object representing tables, columns, keys, and indexes.
/// </summary>
public class Generator
{
	/// <summary>
	/// Generates a complete database schema by interrogating SQL Server metadata.
	/// Connects to the specified database and extracts all structural information
	/// needed for LLM-based query generation.
	/// </summary>
	/// <param name="connectionString">SQL Server connection string with database specified</param>
	/// <returns>Complete DatabaseSchema with all tables, columns, keys, and indexes</returns>
	/// <exception cref="SqlException">Thrown when database connection or query execution fails</exception>
	public async Task<DatabaseSchema> GenerateDatabaseSchemaAsync(string connectionString)
	{
		var schema = new DatabaseSchema();

		// Open connection to target database
		using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		// Capture database identification from active connection
		schema.DatabaseName = connection.Database;
		schema.ServerName = connection.DataSource;
		schema.GeneratedAt = DateTime.UtcNow;

		// Get list of all user tables (excludes system tables)
		var tables = await GetTablesAsync(connection);

		// For each table, extract complete metadata
		foreach (var table in tables)
		{
			var tableSchema = new TableSchema
			{
				SchemaName = table.SchemaName,
				TableName = table.TableName,
				Columns = await GetColumnsAsync(connection, table.SchemaName, table.TableName),
				PrimaryKey = await GetPrimaryKeyAsync(connection, table.SchemaName, table.TableName),
				ForeignKeys = await GetForeignKeysAsync(connection, table.SchemaName, table.TableName),
				Indexes = await GetIndexesAsync(connection, table.SchemaName, table.TableName)
			};

			schema.Tables.Add(tableSchema);
		}

		return schema;
	}

	/// <summary>
	/// Retrieves list of all user tables in the database.
	/// Excludes system tables and views, returning only base tables.
	/// </summary>
	/// <param name="connection">Active SQL Server connection</param>
	/// <returns>List of schema/table name tuples</returns>
	async Task<List<(string SchemaName, string TableName)>> GetTablesAsync(SqlConnection connection)
	{
		var tables = new List<(string, string)>();

		// Query INFORMATION_SCHEMA to get all base tables (not views)
		var query = @"
        SELECT TABLE_SCHEMA, TABLE_NAME 
        FROM INFORMATION_SCHEMA.TABLES 
        WHERE TABLE_TYPE = 'BASE TABLE'
        ORDER BY TABLE_SCHEMA, TABLE_NAME";

		using var command = new SqlCommand(query, connection);
		using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			tables.Add((reader.GetString(0), reader.GetString(1)));
		}

		return tables;
	}

	/// <summary>
	/// Retrieves all column definitions for a specific table.
	/// Includes data types, nullability, defaults, and ordinal positions.
	/// </summary>
	/// <param name="connection">Active SQL Server connection</param>
	/// <param name="schemaName">Schema name (e.g., "dbo")</param>
	/// <param name="tableName">Table name</param>
	/// <returns>List of ColumnSchema objects ordered by position in table</returns>
	async Task<List<ColumnSchema>> GetColumnsAsync(SqlConnection connection, string schemaName, string tableName)
	{
		var columns = new List<ColumnSchema>();

		// Query INFORMATION_SCHEMA.COLUMNS for complete column metadata
		var query = @"
        SELECT 
            c.COLUMN_NAME,
            c.DATA_TYPE,
            c.CHARACTER_MAXIMUM_LENGTH,
            c.NUMERIC_PRECISION,
            c.NUMERIC_SCALE,
            c.IS_NULLABLE,
            c.COLUMN_DEFAULT,
            c.ORDINAL_POSITION
        FROM INFORMATION_SCHEMA.COLUMNS c
        WHERE c.TABLE_SCHEMA = @SchemaName AND c.TABLE_NAME = @TableName
        ORDER BY c.ORDINAL_POSITION";

		using var command = new SqlCommand(query, connection);
		command.Parameters.AddWithValue("@SchemaName", schemaName);
		command.Parameters.AddWithValue("@TableName", tableName);

		using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			columns.Add(new ColumnSchema
			{
				ColumnName = reader.GetString(0),
				DataType = reader.GetString(1),
				MaxLength = reader.IsDBNull(2) ? null : reader.GetInt32(2),  // For varchar, nvarchar, etc.
				NumericPrecision = reader.IsDBNull(3) ? null : reader.GetByte(3),  // For decimal, numeric
				NumericScale = reader.IsDBNull(4) ? null : reader.GetInt32(4),  // Decimal places
				IsNullable = reader.GetString(5) == "YES",
				DefaultValue = reader.IsDBNull(6) ? null : reader.GetString(6),
				OrdinalPosition = reader.GetInt32(7)
			});
		}

		return columns;
	}

	/// <summary>
	/// Retrieves the primary key constraint for a table, if one exists.
	/// Handles composite primary keys (multiple columns).
	/// </summary>
	/// <param name="connection">Active SQL Server connection</param>
	/// <param name="schemaName">Schema name</param>
	/// <param name="tableName">Table name</param>
	/// <returns>PrimaryKeySchema if table has a PK, otherwise null</returns>
	async Task<PrimaryKeySchema?> GetPrimaryKeyAsync(SqlConnection connection, string schemaName, string tableName)
	{
		// Join TABLE_CONSTRAINTS and KEY_COLUMN_USAGE to get PK details
		var query = @"
        SELECT 
            kc.CONSTRAINT_NAME,
            kc.COLUMN_NAME
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kc 
            ON tc.CONSTRAINT_NAME = kc.CONSTRAINT_NAME 
            AND tc.TABLE_SCHEMA = kc.TABLE_SCHEMA
            AND tc.TABLE_NAME = kc.TABLE_NAME
        WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            AND tc.TABLE_SCHEMA = @SchemaName 
            AND tc.TABLE_NAME = @TableName
        ORDER BY kc.ORDINAL_POSITION";

		using var command = new SqlCommand(query, connection);
		command.Parameters.AddWithValue("@SchemaName", schemaName);
		command.Parameters.AddWithValue("@TableName", tableName);

		using var reader = await command.ExecuteReaderAsync();

		string? constraintName = null;
		var columns = new List<string>();

		// Collect all columns that comprise the primary key
		while (await reader.ReadAsync())
		{
			constraintName ??= reader.GetString(0);  // Constraint name (same for all rows)
			columns.Add(reader.GetString(1));  // Column name
		}

		// Return null if table has no primary key
		return constraintName != null ? new PrimaryKeySchema
		{
			ConstraintName = constraintName,
			Columns = columns
		} : null;
	}

	/// <summary>
	/// Retrieves all foreign key relationships for a table.
	/// Handles composite foreign keys and multi-column relationships.
	/// </summary>
	/// <param name="connection">Active SQL Server connection</param>
	/// <param name="schemaName">Schema name</param>
	/// <param name="tableName">Table name</param>
	/// <returns>List of ForeignKeySchema objects representing relationships to other tables</returns>
	async Task<List<ForeignKeySchema>> GetForeignKeysAsync(SqlConnection connection, string schemaName, string tableName)
	{
		var foreignKeys = new List<ForeignKeySchema>();

		// Query sys.foreign_keys and sys.foreign_key_columns for FK metadata
		// System views provide more reliable data than INFORMATION_SCHEMA for FKs
		var query = @"
        SELECT 
            fk.name AS CONSTRAINT_NAME,
            OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS REFERENCED_SCHEMA,
            OBJECT_NAME(fk.referenced_object_id) AS REFERENCED_TABLE,
            COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS COLUMN_NAME,
            COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS REFERENCED_COLUMN
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc 
            ON fk.object_id = fkc.constraint_object_id
        WHERE OBJECT_SCHEMA_NAME(fk.parent_object_id) = @SchemaName 
            AND OBJECT_NAME(fk.parent_object_id) = @TableName
        ORDER BY fk.name, fkc.constraint_column_id";

		using var command = new SqlCommand(query, connection);
		command.Parameters.AddWithValue("@SchemaName", schemaName);
		command.Parameters.AddWithValue("@TableName", tableName);

		using var reader = await command.ExecuteReaderAsync();

		// Use dictionary to group columns by constraint name (for composite FKs)
		var fkDict = new Dictionary<string, ForeignKeySchema>();

		while (await reader.ReadAsync())
		{
			var constraintName = reader.GetString(0);

			// Create new FK entry if this is the first column in the constraint
			if (!fkDict.ContainsKey(constraintName))
			{
				fkDict[constraintName] = new ForeignKeySchema
				{
					ConstraintName = constraintName,
					ReferencedSchema = reader.GetString(1),
					ReferencedTable = reader.GetString(2),
					Columns = new List<string>(),
					ReferencedColumns = new List<string>()
				};
			}

			// Add this column pair to the FK (handles composite keys)
			fkDict[constraintName].Columns.Add(reader.GetString(3));
			fkDict[constraintName].ReferencedColumns.Add(reader.GetString(4));
		}

		return fkDict.Values.ToList();
	}

	/// <summary>
	/// Retrieves all indexes defined on a table.
	/// Includes clustered, nonclustered, and unique indexes with their column composition.
	/// </summary>
	/// <param name="connection">Active SQL Server connection</param>
	/// <param name="schemaName">Schema name</param>
	/// <param name="tableName">Table name</param>
	/// <returns>List of IndexSchema objects with column-level details</returns>
	async Task<List<IndexSchema>> GetIndexesAsync(SqlConnection connection, string schemaName, string tableName)
	{
		var indexes = new List<IndexSchema>();

		// Query sys.indexes and sys.index_columns for index metadata
		// Excludes heap indexes (name IS NULL)
		var query = @"
        SELECT 
            i.name AS INDEX_NAME,
            i.is_unique AS IS_UNIQUE,
            i.is_primary_key AS IS_PRIMARY_KEY,
            i.type_desc AS INDEX_TYPE,
            COL_NAME(ic.object_id, ic.column_id) AS COLUMN_NAME,
            ic.is_descending_key AS IS_DESCENDING
        FROM sys.indexes i
        INNER JOIN sys.index_columns ic 
            ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        WHERE OBJECT_SCHEMA_NAME(i.object_id) = @SchemaName 
            AND OBJECT_NAME(i.object_id) = @TableName
            AND i.name IS NOT NULL
        ORDER BY i.name, ic.key_ordinal";

		using var command = new SqlCommand(query, connection);
		command.Parameters.AddWithValue("@SchemaName", schemaName);
		command.Parameters.AddWithValue("@TableName", tableName);

		using var reader = await command.ExecuteReaderAsync();

		// Use dictionary to group columns by index name (for composite indexes)
		var indexDict = new Dictionary<string, IndexSchema>();

		while (await reader.ReadAsync())
		{
			var indexName = reader.GetString(0);

			// Create new index entry if this is the first column in the index
			if (!indexDict.ContainsKey(indexName))
			{
				indexDict[indexName] = new IndexSchema
				{
					IndexName = indexName,
					IsUnique = reader.GetBoolean(1),
					IsPrimaryKey = reader.GetBoolean(2),
					IndexType = reader.GetString(3),  // CLUSTERED, NONCLUSTERED, etc.
					Columns = new List<IndexColumnSchema>()
				};
			}

			// Add this column to the index with its sort direction
			indexDict[indexName].Columns.Add(new IndexColumnSchema
			{
				ColumnName = reader.GetString(4),
				IsDescending = reader.GetBoolean(5)  // true = DESC, false = ASC
			});
		}

		return indexDict.Values.ToList();
	}
}
