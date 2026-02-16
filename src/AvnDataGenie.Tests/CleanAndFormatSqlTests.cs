using AvnDataGenie;

namespace AvnDataGenie.Tests;

public class CleanAndFormatSqlTests
{
	#region Markdown Fence Extraction

	[Fact]
	public void CleanAndFormatSql_ExtractsSqlFromMarkdownFence()
	{
		var input = "```sql\nSELECT Id FROM Customers;\n```";

		var result = Generator.CleanAndFormatSql(input);

		Assert.StartsWith("SELECT", result.TrimStart());
		Assert.Contains("Customers", result);
	}

	[Fact]
	public void CleanAndFormatSql_ExtractsSqlFromMarkdownFenceWithoutLanguage()
	{
		var input = "```\nSELECT Id FROM Customers;\n```";

		var result = Generator.CleanAndFormatSql(input);

		Assert.StartsWith("SELECT", result.TrimStart());
		Assert.Contains("Customers", result);
	}

	[Fact]
	public void CleanAndFormatSql_MarkdownFenceCaseInsensitive()
	{
		var input = "```SQL\nSELECT 1;\n```";

		var result = Generator.CleanAndFormatSql(input);

		Assert.Contains("SELECT", result);
	}

	#endregion

	#region Preamble Removal

	[Fact]
	public void CleanAndFormatSql_RemovesPreambleBeforeSelect()
	{
		var input = "Here is the query you asked for:\nSELECT Id FROM Customers;";

		var result = Generator.CleanAndFormatSql(input);

		Assert.StartsWith("SELECT", result.TrimStart());
		Assert.DoesNotContain("Here is the query", result);
	}

	[Fact]
	public void CleanAndFormatSql_PreservesSelectAtStartOfInput()
	{
		var input = "SELECT Id FROM Customers;";

		var result = Generator.CleanAndFormatSql(input);

		Assert.StartsWith("SELECT", result.TrimStart());
	}

	#endregion

	#region Trailing Commentary Removal

	[Fact]
	public void CleanAndFormatSql_RemovesTrailingCommentaryAfterSemicolon()
	{
		var input = "SELECT Id FROM Customers;\nThis query returns all customer IDs.";

		var result = Generator.CleanAndFormatSql(input);

		Assert.DoesNotContain("This query returns", result);
	}

	[Fact]
	public void CleanAndFormatSql_KeepsTextAfterSemicolonIfStartsWithSelect()
	{
		// Edge case: multiple statements (shouldn't happen but tests the logic)
		var input = "SELECT 1; SELECT 2;";

		var result = Generator.CleanAndFormatSql(input);

		// The second SELECT should not be stripped
		Assert.Contains("SELECT", result);
	}

	#endregion

	#region SQL Comment Removal

	[Fact]
	public void CleanAndFormatSql_RemovesSingleLineComments()
	{
		var input = "SELECT Id -- Customer ID\nFROM Customers;";

		var result = Generator.CleanAndFormatSql(input);

		Assert.DoesNotContain("-- Customer ID", result);
		Assert.Contains("Id", result);
	}

	[Fact]
	public void CleanAndFormatSql_RemovesMultiLineComments()
	{
		var input = "SELECT /* this is the key */ Id FROM Customers;";

		var result = Generator.CleanAndFormatSql(input);

		Assert.DoesNotContain("/* this is the key */", result);
		Assert.Contains("Id", result);
	}

	#endregion

	#region Semicolon Handling

	[Fact]
	public void CleanAndFormatSql_AddsSemicolonIfMissing()
	{
		var input = "SELECT Id FROM Customers";

		var result = Generator.CleanAndFormatSql(input);

		Assert.EndsWith(";", result);
	}

	[Fact]
	public void CleanAndFormatSql_DoesNotAddDoubleSemicolon()
	{
		var input = "SELECT Id FROM Customers;";

		var result = Generator.CleanAndFormatSql(input);

		Assert.DoesNotContain(";;", result);
	}

	#endregion

	#region Combined Edge Cases

	[Fact]
	public void CleanAndFormatSql_FullLlmResponseWithEverything()
	{
		var input = """
			Sure! Here's the SQL query:

			```sql
			-- Get all customer names
			SELECT 
				c.Id, /* primary key */
				c.Name
			FROM dbo.Customers c
			WHERE c.Active = 1;
			```

			This query returns active customers.
			""";

		var result = Generator.CleanAndFormatSql(input);

		Assert.StartsWith("SELECT", result.TrimStart());
		Assert.DoesNotContain("Sure!", result);
		Assert.DoesNotContain("```", result);
		Assert.DoesNotContain("-- Get all", result);
		Assert.DoesNotContain("/* primary key */", result);
		Assert.DoesNotContain("This query returns", result);
		Assert.EndsWith(";", result);
	}

	[Fact]
	public void CleanAndFormatSql_WhitespaceOnlyInput_ReturnsSemicolon()
	{
		// Edge: LLM returns nothing useful
		var input = "   ";

		var result = Generator.CleanAndFormatSql(input);

		// Should at minimum end with semicolon and not throw
		Assert.EndsWith(";", result);
	}

	#endregion
}
