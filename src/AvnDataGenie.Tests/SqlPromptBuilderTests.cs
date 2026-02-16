using AvnDataGenie;

namespace AvnDataGenie.Tests;

public class SqlPromptBuilderTests
{
	#region Test Data Helpers

	private static string MinimalSchemaJson() => """
		{
			"databaseName": "TestDB",
			"serverName": "localhost",
			"tables": [
				{
					"schemaName": "dbo",
					"tableName": "Customers",
					"columns": [
						{ "columnName": "Id", "dataType": "int", "isNullable": false },
						{ "columnName": "Name", "dataType": "nvarchar", "maxLength": 100, "isNullable": false }
					],
					"primaryKey": { "constraintName": "PK_Customers", "columns": ["Id"] }
				}
			]
		}
		""";

	private static string MinimalConfigJson() => """
		{
			"tableConfigurations": []
		}
		""";

	private static string RichSchemaJson() => """
		{
			"databaseName": "SalesDB",
			"serverName": "prod-sql-01",
			"tables": [
				{
					"schemaName": "dbo",
					"tableName": "Orders",
					"aliases": ["Purchases"],
					"columns": [
						{ "columnName": "OrderId", "dataType": "int", "isNullable": false },
						{ "columnName": "CustomerId", "dataType": "int", "isNullable": false },
						{ "columnName": "Total", "dataType": "decimal", "isNullable": true }
					],
					"primaryKey": { "constraintName": "PK_Orders", "columns": ["OrderId"] },
					"foreignKeys": [
						{
							"constraintName": "FK_Orders_Customers",
							"referencedSchema": "dbo",
							"referencedTable": "Customers",
							"columns": ["CustomerId"],
							"referencedColumns": ["Id"]
						}
					]
				},
				{
					"schemaName": "dbo",
					"tableName": "Customers",
					"columns": [
						{ "columnName": "Id", "dataType": "int", "isNullable": false },
						{ "columnName": "Email", "dataType": "nvarchar", "maxLength": 256, "isNullable": false },
						{ "columnName": "SSN", "dataType": "char", "maxLength": 11, "isNullable": true }
					],
					"primaryKey": { "constraintName": "PK_Customers", "columns": ["Id"] }
				}
			]
		}
		""";

	private static string RichConfigJson() => """
		{
			"tableConfigurations": [
				{
					"schemaName": "dbo",
					"tableName": "Customers",
					"friendlyName": "Customer Records",
					"description": "All registered customers",
					"aliases": "Clients, Buyers",
					"columnConfigurations": [
						{
							"columnName": "SSN",
							"friendlyName": "Social Security Number",
							"aliases": "Social",
							"isPii": true,
							"isRestricted": true
						},
						{
							"columnName": "Email",
							"friendlyName": "Email Address",
							"description": "Primary contact email"
						}
					]
				}
			],
			"joinHints": [
				{
					"fromTable": "dbo.Orders",
					"fromColumn": "CustomerId",
					"toTable": "dbo.Customers",
					"toColumn": "Id",
					"hint": "Primary customer relationship"
				}
			],
			"requiredFilters": [
				"WHERE IsDeleted = 0"
			],
			"businessTerms": [
				"Revenue = SUM(Orders.Total)"
			]
		}
		""";

	#endregion

	#region Input Validation

	[Fact]
	public void CreateSystemPromptFromJson_NullSchema_ThrowsArgumentException()
	{
		var ex = Assert.Throws<ArgumentException>(
			() => SqlPromptBuilder.CreateSystemPromptFromJson(null!, MinimalConfigJson()));

		Assert.Equal("databaseSchemaJson", ex.ParamName);
	}

	[Fact]
	public void CreateSystemPromptFromJson_EmptySchema_ThrowsArgumentException()
	{
		var ex = Assert.Throws<ArgumentException>(
			() => SqlPromptBuilder.CreateSystemPromptFromJson("", MinimalConfigJson()));

		Assert.Equal("databaseSchemaJson", ex.ParamName);
	}

	[Fact]
	public void CreateSystemPromptFromJson_WhitespaceSchema_ThrowsArgumentException()
	{
		var ex = Assert.Throws<ArgumentException>(
			() => SqlPromptBuilder.CreateSystemPromptFromJson("   ", MinimalConfigJson()));

		Assert.Equal("databaseSchemaJson", ex.ParamName);
	}

	[Fact]
	public void CreateSystemPromptFromJson_NullConfig_ThrowsArgumentException()
	{
		var ex = Assert.Throws<ArgumentException>(
			() => SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), null!));

		Assert.Equal("llmConfigJson", ex.ParamName);
	}

	[Fact]
	public void CreateSystemPromptFromJson_EmptyConfig_ThrowsArgumentException()
	{
		var ex = Assert.Throws<ArgumentException>(
			() => SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), ""));

		Assert.Equal("llmConfigJson", ex.ParamName);
	}

	[Fact]
	public void CreateSystemPromptFromJson_InvalidSchemaJson_ThrowsJsonException()
	{
		Assert.ThrowsAny<System.Text.Json.JsonException>(
			() => SqlPromptBuilder.CreateSystemPromptFromJson("not json", MinimalConfigJson()));
	}

	[Fact]
	public void CreateSystemPromptFromJson_InvalidConfigJson_ThrowsJsonException()
	{
		Assert.ThrowsAny<System.Text.Json.JsonException>(
			() => SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), "{{bad}}"));
	}

	#endregion

	#region Core Instructions

	[Fact]
	public void CreateSystemPromptFromJson_MinimalInputs_ContainsCoreInstructions()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), MinimalConfigJson());

		Assert.Contains("You are a SQL query generator.", result);
		Assert.Contains("EXACTLY ONE SQL Server SELECT statement", result);
		Assert.Contains("Hard rules:", result);
		Assert.Contains("T-SQL SELECT statement", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_MinimalInputs_ContainsOutputFormat()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), MinimalConfigJson());

		Assert.Contains("Output format:", result);
		Assert.Contains("SQL only. No markdown. No explanation. No JSON.", result);
	}

	#endregion

	#region Database Context

	[Fact]
	public void CreateSystemPromptFromJson_IncludesDatabaseName()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), MinimalConfigJson());

		Assert.Contains("Name: TestDB", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_IncludesServerName()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), MinimalConfigJson());

		Assert.Contains("Server: localhost", result);
	}

	#endregion

	#region Schema Rendering

	[Fact]
	public void CreateSystemPromptFromJson_RendersTableWithSchemaQualification()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), MinimalConfigJson());

		Assert.Contains("[dbo].[Customers]", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_RendersColumns()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), MinimalConfigJson());

		Assert.Contains("[Id] : int", result);
		Assert.Contains("[Name] : nvarchar(100)", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_RendersNotNullFlag()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), MinimalConfigJson());

		// Id and Name are NOT NULL
		Assert.Contains("NOT NULL", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_RendersPrimaryKey()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), MinimalConfigJson());

		Assert.Contains("PrimaryKey: ([Id])", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_RendersForeignKeys()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(RichSchemaJson(), RichConfigJson());

		Assert.Contains("[dbo].[Orders].[CustomerId] -> [dbo].[Customers].[Id]", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_RendersTableAliasesFromSchema()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(RichSchemaJson(), MinimalConfigJson());

		Assert.Contains("Purchases", result);
	}

	#endregion

	#region LLM Config: Business Metadata

	[Fact]
	public void CreateSystemPromptFromJson_RendersFriendlyName()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(RichSchemaJson(), RichConfigJson());

		Assert.Contains("FriendlyName: Customer Records", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_RendersDescription()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(RichSchemaJson(), RichConfigJson());

		Assert.Contains("Description: All registered customers", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_RendersConfigAliases()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(RichSchemaJson(), RichConfigJson());

		Assert.Contains("Clients", result);
		Assert.Contains("Buyers", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_RendersPiiFlag()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(RichSchemaJson(), RichConfigJson());

		Assert.Contains("PII", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_RendersRestrictedFlag()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(RichSchemaJson(), RichConfigJson());

		Assert.Contains("RESTRICTED", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_RendersColumnFriendlyName()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(RichSchemaJson(), RichConfigJson());

		Assert.Contains("FriendlyName: Social Security Number", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_RendersColumnDescription()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(RichSchemaJson(), RichConfigJson());

		Assert.Contains("Description: Primary contact email", result);
	}

	#endregion

	#region LLM Config: Join Hints, Filters, Business Terms

	[Fact]
	public void CreateSystemPromptFromJson_RendersJoinHints()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(RichSchemaJson(), RichConfigJson());

		Assert.Contains("Join Hints:", result);
		Assert.Contains("dbo.Orders.[CustomerId] -> dbo.Customers.[Id]", result);
		Assert.Contains("// Primary customer relationship", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_RendersRequiredFilters()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(RichSchemaJson(), RichConfigJson());

		Assert.Contains("Required Filters:", result);
		Assert.Contains("WHERE IsDeleted = 0", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_RendersBusinessTerms()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(RichSchemaJson(), RichConfigJson());

		Assert.Contains("Business Terms:", result);
		Assert.Contains("Revenue = SUM(Orders.Total)", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_NoJoinHints_OmitsSection()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), MinimalConfigJson());

		Assert.DoesNotContain("Join Hints:", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_NoRequiredFilters_OmitsSection()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), MinimalConfigJson());

		Assert.DoesNotContain("Required Filters:", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_NoBusinessTerms_OmitsSection()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), MinimalConfigJson());

		Assert.DoesNotContain("Business Terms:", result);
	}

	#endregion

	#region Table/Column Limits

	[Fact]
	public void CreateSystemPromptFromJson_MaxTablesLimit_TruncatesTables()
	{
		var schemaJson = """
			{
				"databaseName": "BigDB",
				"serverName": "localhost",
				"tables": [
					{ "schemaName": "dbo", "tableName": "Table1", "columns": [] },
					{ "schemaName": "dbo", "tableName": "Table2", "columns": [] },
					{ "schemaName": "dbo", "tableName": "Table3", "columns": [] }
				]
			}
			""";

		var result = SqlPromptBuilder.CreateSystemPromptFromJson(schemaJson, MinimalConfigJson(), maxTablesToInclude: 2);

		Assert.Contains("[dbo].[Table1]", result);
		Assert.Contains("[dbo].[Table2]", result);
		Assert.DoesNotContain("[dbo].[Table3]", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_MaxColumnsLimit_TruncatesColumns()
	{
		var schemaJson = """
			{
				"databaseName": "DB",
				"serverName": "srv",
				"tables": [
					{
						"schemaName": "dbo",
						"tableName": "Wide",
						"columns": [
							{ "columnName": "Col1", "dataType": "int", "isNullable": false },
							{ "columnName": "Col2", "dataType": "int", "isNullable": false },
							{ "columnName": "Col3", "dataType": "int", "isNullable": false }
						]
					}
				]
			}
			""";

		var result = SqlPromptBuilder.CreateSystemPromptFromJson(schemaJson, MinimalConfigJson(),
			maxColumnsPerTableToInclude: 2);

		Assert.Contains("[Col1]", result);
		Assert.Contains("[Col2]", result);
		Assert.DoesNotContain("[Col3]", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_ZeroMaxTables_RendersNoTables()
	{
		var result = SqlPromptBuilder.CreateSystemPromptFromJson(MinimalSchemaJson(), MinimalConfigJson(),
			maxTablesToInclude: 0);

		Assert.DoesNotContain("[dbo].[Customers]", result);
	}

	#endregion

	#region JSON Flexibility

	[Fact]
	public void CreateSystemPromptFromJson_CaseInsensitivePropertyNames_Works()
	{
		// PascalCase property names instead of camelCase
		var schema = """
			{
				"DatabaseName": "CaseTest",
				"ServerName": "srv",
				"Tables": []
			}
			""";

		var result = SqlPromptBuilder.CreateSystemPromptFromJson(schema, MinimalConfigJson());

		Assert.Contains("Name: CaseTest", result);
	}

	[Fact]
	public void CreateSystemPromptFromJson_TrailingCommas_Works()
	{
		var schema = """
			{
				"databaseName": "CommaTest",
				"serverName": "srv",
				"tables": [],
			}
			""";

		var result = SqlPromptBuilder.CreateSystemPromptFromJson(schema, MinimalConfigJson());

		Assert.Contains("Name: CommaTest", result);
	}

	#endregion
}
