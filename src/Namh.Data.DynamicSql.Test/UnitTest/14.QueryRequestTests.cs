using System.Text.RegularExpressions;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public class _14_QueryRequestTests : TestBase
{
    [Test]
    public void DefinitionBuildsParameterizedSql()
    {
        var definition = new QueryRequest
        {
            Entity = "User",
            Columns =
            [
                new FieldTranslator("Name"),
                new FieldTranslator("Manager.Name", "ManagerName")
            ],
            Filter = new QueryFilter(
                LogicalOperator.And,
                new QueryFilterItem(LogicalOperator.And, "Age", BinaryOperator.GreaterThan, 18)),
            Sorts =
            [
                new QuerySort("Name")
            ],
            Page = 1,
            PageSize = 10
        };

        var sql = _db.Query(definition).ToSql();

        Assert.That(sql.CommandText, Does.Contain("SELECT"));
        Assert.That(sql.CommandText, Does.Contain("ORDER BY"));
        Assert.That(sql.CommandText, Does.Contain("FETCH NEXT 10 ROWS ONLY"));
        Assert.That(sql.CommandText, Does.Contain("Age"));
    }

    [Test]
    public void AggregateFilterUsesHavingInsteadOfScalarSubquery()
    {
        var definition = new QueryRequest
        {
            Entity = "User",
            Columns =
            [
                new FieldTranslator("Name"),
                new FieldTranslator("Average(Age)")
            ],
            Filter = new QueryFilter(
                LogicalOperator.And,
                new QueryFilterItem(
                    LogicalOperator.And,
                    "Average(Age)",
                    BinaryOperator.GreaterThan,
                    12)),
            PageSize = 50
        };

        var sql = _db.Query(definition).ToSql().CommandText;

        Assert.That(sql, Does.Contain("GROUP BY t0.[Name]"));
        Assert.That(sql, Does.Contain("HAVING (AVG (t0.[Age]) >"));
        Assert.That(sql, Does.Not.Contain("WHERE (("));
        Assert.That(sql, Does.Not.Contain("SELECT AVG"));
    }

    [Test]
    public void DefinitionExecutesWithProjectionAndFilter()
    {
        var definition = new QueryRequest
        {
            Entity = "User",
            Columns = [new FieldTranslator("Name")],
            Filter = new QueryFilter(
                LogicalOperator.And,
                new QueryFilterItem(LogicalOperator.And, "Name", BinaryOperator.Equals, "Alice"))
        };

        var records = _db.Query(definition).ToList();

        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0]["Name"], Is.EqualTo("Alice"));
    }

    [Test]
    public void DefinitionSupportsAggregateOnRootEntity()
    {
        var definition = new QueryRequest
        {
            Entity = "User",
            Columns =
            [
                new FieldTranslator("Name"),
                new FieldTranslator("Count()", "InvoiceCount"),
                new FieldTranslator("Sum(Age)", "Revenue"),
                new FieldTranslator("Average(Age)", "AverageOrderValue")
            ]
        };

        var sql = _db.Query(definition).ToSql();
        var records = _db.Query(definition).ToList();

        Assert.That(sql.CommandText, Does.Contain("GROUP BY"));
        Assert.That(sql.CommandText, Does.Match(@"(?i)\bCOUNT\s*\(\s*\*\s*\)"));
        Assert.That(sql.CommandText, Does.Match(@"(?i)\bSUM\s*\("));
        Assert.That(sql.CommandText, Does.Match(@"(?i)\bAVG\s*\("));
        Assert.That(sql.CommandText, Does.Not.Match(@"(?i)\bSELECT\s+COUNT\s*\(\s*\*\s*\)"));
        Assert.That(sql.CommandText, Does.Not.Contain("Average("));
        Assert.That(records, Is.Not.Empty);
        Assert.That(records[0]["InvoiceCount"], Is.EqualTo(1L));
    }

    [Test]
    public void DefinitionSortsByCollectionAggregate()
    {
        var definition = new QueryRequest
        {
            Entity = "User",
            Columns =
            [
                new FieldTranslator("Name"),
                new FieldTranslator("Sum(Members.Age)")
            ],
            Sorts = [new QuerySort("Sum(Members.Age)")]
        };

        var sql = _db.Query(definition).ToSql();
        var records = _db.Query(definition).ToList();

        Assert.That(sql.CommandText, Does.Contain("ORDER BY Sum ("));
        Assert.That(sql.CommandText, Does.Not.Contain("ORDER BY (\n    SELECT"));
        Assert.That(records.Select(record => record["Name"]),
            Is.EqualTo(new object?[] { "Charlie", "Eve", "Frank", "Bob", "David", "Alice" }));
    }

    [Test]
    public void DefinitionConnectsStructuredFiltersWithLogicalOperators()
    {
        var definition = new QueryRequest
        {
            Entity = "User",
            Filter = new QueryFilter(
                LogicalOperator.And,
                new QueryFilter(
                    LogicalOperator.Or,
                    new QueryFilterItem(LogicalOperator.Or, "Name", BinaryOperator.Equals, "Alice"),
                    new QueryFilterItem(LogicalOperator.Or, "Age", BinaryOperator.GreaterThan, 18)),
                new QueryFilterItem(LogicalOperator.And, "Name", BinaryOperator.Equals, "Bob"))
        };

        var sql = _db.Query(definition).ToSql();

        Assert.That(sql.CommandText, Does.Contain("OR"));
        Assert.That(sql.CommandText, Does.Contain("AND"));
        Assert.That(sql.CommandText, Does.Not.Contain("= 12"));
    }

    [Test]
    public void DefinitionRejectsInvalidPaging()
    {
        var definition = new QueryRequest
        {
            Entity = "User",
            Page = 0,
            PageSize = 10
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => _db.Query(definition));
    }

    [Test]
    public void DefinitionFormatsAsReadableText()
    {
        var definition = new QueryRequest
        {
            Entity = "User",
            Columns = [new FieldTranslator("Id"), new FieldTranslator("Name")],
            Filter = new QueryFilter(
                LogicalOperator.And,
                new QueryFilter(
                    LogicalOperator.And,
                    new QueryFilterItem(LogicalOperator.And, "Id", BinaryOperator.GreaterThan, 3),
                    new QueryFilterItem(LogicalOperator.And, "Invoices.Id", BinaryOperator.LessThan, 3)),
                new QueryFilter(
                    LogicalOperator.Or,
                    new QueryFilterItem(LogicalOperator.Or, "Name", BinaryOperator.Equals, "abc"),
                    new QueryFilterItem(LogicalOperator.Or, "Age", BinaryOperator.GreaterThan, 18))),
            Sorts = [new QuerySort("Name")],
            Page = 1,
            PageSize = 25,
            IncludeArchive = true
        };

        var expected = string.Join(Environment.NewLine,
            "Entity: User",
            "Columns: Id, Name",
            "Filters: (Id > 3 AND Invoices.Id < 3) AND (Name = \"abc\" OR Age > 18)",
            "Sorts: Name ASC",
            "Page: 1",
            "PageSize: 25",
            "IncludeArchive: True");

        Assert.That(definition.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void DefinitionFormatsEmptyValuesAsBlank()
    {
        var definition = new QueryRequest { Entity = "User" };

        var expected = string.Join(Environment.NewLine,
            "Entity: User",
            "Columns: ",
            "Filters: ",
            "Sorts: ",
            "Page: ",
            "PageSize: ",
            "IncludeArchive: False");

        Assert.That(definition.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void DefinitionJsonDeserializesRecursiveFilterValues()
    {
        const string json = """
            {
              "entity": "User",
              "filter": {
                "logicalOperation": 0,
                "filters": [{
                  "logicalOperator": 0,
                  "field": "Name",
                  "operation": 0,
                  "value": "Alice",
                  "parameterized": false
                }]
              }
            }
            """;

        var definition = System.Text.Json.JsonSerializer.Deserialize<QueryRequest>(
            json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.That(definition.Filter, Is.TypeOf<QueryFilter>());
        var filter = definition.Filter!.Filters.Single() as QueryFilterItem;
        Assert.That(filter, Is.Not.Null);
        Assert.That(filter!.Field, Is.EqualTo("Name"));
        Assert.That(filter.Value?.ToString(), Is.EqualTo("Alice"));
    }

    [Test]
    public void JsonDefinitionSupportsAliasesLikeGroupingSortingAndPaging()
    {
        const string json = """
            {
              "entity": "User",
              "columns": ["Name", "Manager.Name|Manager-Name"],
              "filter": "[Name] like 'Dav%' && ([Manager.Name] == 'Alice' || [Members.TotalMembers] < 12)",
              "sorts": ["-Manager.Name"],
              "page": 1,
              "pageSize": 50
            }
            """;

        var sql = _db.QueryJson(json).ToSql();

        Assert.That(sql.Parameters.Select(parameter => parameter.Value),
            Is.EquivalentTo(new object?[] { "Dav%", "Alice", 12 }));

        var data = _db.QueryJson(json).ToList();

        string[] expectedValues = ["David - Bob"];
        var actualValues = data.Select(e => $"{e["Name"]} - {e["Manager-Name"] ?? "NULL"}").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    public void JsonDefinitionRejectsInvalidFilter()
    {
        const string json = """{"entity":"User","filter":"Name like"}""";

        Assert.Throws<FormatException>(() => QueryRequest.FromJson(json));
        Assert.Throws<FormatException>(() =>
            QueryRequest.FromJson("""{"entity":"User","filter":"Age > (12 + OtherAge)"}"""));
    }

    [Test]
    public void JsonDefinitionParsesEscapedSingleQuotesInFilterValues()
    {
        var definition = QueryRequest.FromJson("""{"entity":"User","filter":"Name == 'O''Neil'"}""");
        var filter = (QueryFilterItem)definition.Filter!.Filters[0];

        Assert.That(filter.Value, Is.EqualTo("O'Neil"));
    }

}
