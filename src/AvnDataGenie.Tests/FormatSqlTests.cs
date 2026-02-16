using AvnDataGenie;

namespace AvnDataGenie.Tests;

public class FormatSqlTests
{
	#region FormatSql

	[Fact]
	public void FormatSql_NullInput_ReturnsNull()
	{
		var result = Generator.FormatSql(null!);
		Assert.Null(result);
	}

	[Fact]
	public void FormatSql_EmptyInput_ReturnsEmpty()
	{
		var result = Generator.FormatSql("");
		Assert.Equal("", result);
	}

	[Fact]
	public void FormatSql_WhitespaceInput_ReturnsWhitespace()
	{
		var result = Generator.FormatSql("   ");
		Assert.Equal("   ", result);
	}

	[Fact]
	public void FormatSql_SelectFromOnSingleLine_AddsLineBreaks()
	{
		var input = "SELECT Id, Name FROM Customers WHERE Active = 1";

		var result = Generator.FormatSql(input);

		Assert.Contains("\n", result);
		// SELECT and FROM should be on different lines
		var lines = result.Split('\n');
		Assert.True(lines.Length > 1, "Expected multi-line output");
	}

	[Fact]
	public void FormatSql_JoinKeywords_AreIndented()
	{
		var input = "SELECT o.Id FROM Orders o INNER JOIN Customers c ON o.CustomerId = c.Id";

		var result = Generator.FormatSql(input);

		// JOIN should appear on an indented line
		var joinLine = result.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("JOIN") || l.Contains("JOIN"));
		Assert.NotNull(joinLine);
		var indent = joinLine.Length - joinLine.TrimStart().Length;
		Assert.True(indent >= 4, $"Expected JOIN indent >= 4, got {indent}");
	}

	[Fact]
	public void FormatSql_MajorKeywords_StartOnNewLines()
	{
		var input = "SELECT Id FROM Customers WHERE Active = 1 ORDER BY Name";

		var result = Generator.FormatSql(input);

		var lines = result.Split('\n').Select(l => l.TrimStart()).ToArray();
		Assert.Contains(lines, l => l.StartsWith("FROM"));
		Assert.Contains(lines, l => l.StartsWith("WHERE"));
		Assert.Contains(lines, l => l.StartsWith("ORDER BY"));
	}

	[Fact]
	public void FormatSql_SelectTopN_HandledCorrectly()
	{
		var input = "SELECT TOP(10) Id, Name FROM Customers";

		var result = Generator.FormatSql(input);

		Assert.Contains("SELECT TOP(10)", result);
	}

	[Fact]
	public void FormatSql_SelectDistinct_HandledCorrectly()
	{
		var input = "SELECT DISTINCT Name FROM Customers";

		var result = Generator.FormatSql(input);

		Assert.Contains("SELECT DISTINCT", result);
	}

	[Fact]
	public void FormatSql_GroupByHaving_OnSeparateLines()
	{
		var input = "SELECT CustomerId, COUNT(*) as Cnt FROM Orders GROUP BY CustomerId HAVING COUNT(*) > 5";

		var result = Generator.FormatSql(input);

		var lines = result.Split('\n').Select(l => l.TrimStart()).ToArray();
		Assert.Contains(lines, l => l.StartsWith("GROUP BY"));
		Assert.Contains(lines, l => l.StartsWith("HAVING"));
	}

	[Fact]
	public void FormatSql_ON_IsIndentedUnderJoin()
	{
		var input = "SELECT o.Id FROM Orders o INNER JOIN Customers c ON o.CustomerId = c.Id";

		var result = Generator.FormatSql(input);

		var onLine = result.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("ON "));
		Assert.NotNull(onLine);
		// ON should be more indented than JOIN
		var onIndent = onLine.Length - onLine.TrimStart().Length;
		Assert.True(onIndent >= 8, $"Expected ON indent >= 8, got {onIndent}");
	}

	[Fact]
	public void FormatSql_CollapsesMultipleWhitespace()
	{
		var input = "SELECT    Id    FROM    Customers";

		var result = Generator.FormatSql(input);

		Assert.DoesNotContain("    Id    ", result);
	}

	#endregion

	#region FormatSelectColumns

	[Fact]
	public void FormatSelectColumns_CommaInSelectClause_AddsNewlines()
	{
		var input = "SELECT Id, Name, Email FROM Customers";

		var result = Generator.FormatSelectColumns(input);

		// After each comma in SELECT clause, there should be a newline
		Assert.Contains(",\n", result);
	}

	[Fact]
	public void FormatSelectColumns_CommaInsideFunction_NoNewline()
	{
		var input = "SELECT SUBSTRING(Name, 1, 10), Id FROM Customers";

		var result = Generator.FormatSelectColumns(input);

		// Comma inside SUBSTRING() should NOT get a newline
		Assert.Contains("SUBSTRING(Name, 1, 10)", result);
	}

	[Fact]
	public void FormatSelectColumns_CommaInWhereClause_NoNewline()
	{
		// Commas in IN() clause after FROM should not be split
		var input = "SELECT Id FROM Customers WHERE Id IN (1, 2, 3)";

		var result = Generator.FormatSelectColumns(input);

		// The commas in IN (1, 2, 3) should not get newlines since they're after FROM
		Assert.Contains("IN (1, 2, 3)", result);
	}

	[Fact]
	public void FormatSelectColumns_NestedFunctions_RespectParenDepth()
	{
		var input = "SELECT COALESCE(NULLIF(Name, ''), 'Unknown'), Id FROM T";

		var result = Generator.FormatSelectColumns(input);

		// The comma between two args of COALESCE should not get newline
		Assert.Contains("COALESCE(NULLIF(Name, ''), 'Unknown')", result);
	}

	[Fact]
	public void FormatSelectColumns_NoFromKeyword_TreatsEverythingAsSelect()
	{
		// Degenerate case: no FROM keyword
		var input = "SELECT 1, 2, 3";

		var result = Generator.FormatSelectColumns(input);

		Assert.Contains(",\n", result);
	}

	#endregion
}
