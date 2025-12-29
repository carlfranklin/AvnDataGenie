using Microsoft.Data.SqlClient;
using QueryGenerator.Models;

namespace QueryGenerator;

public class Generator
{
	async Task<DatabaseSchema> GenerateDatabaseSchemaAsync(string connectionString)
	{
		var schema = new DatabaseSchema();

		using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		schema.DatabaseName = connection.Database;
		schema.ServerName = connection.DataSource;
		schema.GeneratedAt = DateTime.UtcNow;

		var tables = await GetTablesAsync(connection);

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

	async Task<List<(string SchemaName, string TableName)>> GetTablesAsync(SqlConnection connection)
	{
		var tables = new List<(string, string)>();

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

	async Task<List<ColumnSchema>> GetColumnsAsync(SqlConnection connection, string schemaName, string tableName)
	{
		var columns = new List<ColumnSchema>();

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
				MaxLength = reader.IsDBNull(2) ? null : reader.GetInt32(2),
				NumericPrecision = reader.IsDBNull(3) ? null : reader.GetByte(3),
				NumericScale = reader.IsDBNull(4) ? null : reader.GetInt32(4),
				IsNullable = reader.GetString(5) == "YES",
				DefaultValue = reader.IsDBNull(6) ? null : reader.GetString(6),
				OrdinalPosition = reader.GetInt32(7)
			});
		}

		return columns;
	}

	async Task<PrimaryKeySchema?> GetPrimaryKeyAsync(SqlConnection connection, string schemaName, string tableName)
	{
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

		while (await reader.ReadAsync())
		{
			constraintName ??= reader.GetString(0);
			columns.Add(reader.GetString(1));
		}

		return constraintName != null ? new PrimaryKeySchema
		{
			ConstraintName = constraintName,
			Columns = columns
		} : null;
	}

	async Task<List<ForeignKeySchema>> GetForeignKeysAsync(SqlConnection connection, string schemaName, string tableName)
	{
		var foreignKeys = new List<ForeignKeySchema>();

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

		var fkDict = new Dictionary<string, ForeignKeySchema>();

		while (await reader.ReadAsync())
		{
			var constraintName = reader.GetString(0);

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

			fkDict[constraintName].Columns.Add(reader.GetString(3));
			fkDict[constraintName].ReferencedColumns.Add(reader.GetString(4));
		}

		return fkDict.Values.ToList();
	}

	async Task<List<IndexSchema>> GetIndexesAsync(SqlConnection connection, string schemaName, string tableName)
	{
		var indexes = new List<IndexSchema>();

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

		var indexDict = new Dictionary<string, IndexSchema>();

		while (await reader.ReadAsync())
		{
			var indexName = reader.GetString(0);

			if (!indexDict.ContainsKey(indexName))
			{
				indexDict[indexName] = new IndexSchema
				{
					IndexName = indexName,
					IsUnique = reader.GetBoolean(1),
					IsPrimaryKey = reader.GetBoolean(2),
					IndexType = reader.GetString(3),
					Columns = new List<IndexColumnSchema>()
				};
			}

			indexDict[indexName].Columns.Add(new IndexColumnSchema
			{
				ColumnName = reader.GetString(4),
				IsDescending = reader.GetBoolean(5)
			});
		}

		return indexDict.Values.ToList();
	}
}
