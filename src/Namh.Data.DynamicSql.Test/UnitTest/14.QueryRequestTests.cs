namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public class _14_QueryRequestTests : TestBase
{
    [Test]
    public void DefinitionBuildsParameterizedSql()
    {
        var definition = new QueryRequestDto
        {
            Entity = "User",
            Columns =
            [
                new QueryColumn("Name"),
                new QueryColumn("Manager.Name", "ManagerName")
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
    public void DefinitionExecutesWithProjectionAndFilter()
    {
        var definition = new QueryRequestDto
        {
            Entity = "User",
            Columns = [new QueryColumn("Name")],
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
        var definition = new QueryRequestDto
        {
            Entity = "User",
            Columns =
            [
                new QueryColumn("Name"),
                new QueryColumn("Count()", "InvoiceCount"),
                new QueryColumn("Sum(Age)", "Revenue"),
                new QueryColumn("Average(Age)", "AverageOrderValue")
            ]
        };

        var sql = _db.Query(definition).ToSql();
        var records = _db.Query(definition).ToList();

        Assert.That(sql.CommandText, Does.Contain("GROUP BY"));
        Assert.That(sql.CommandText, Does.Contain("Count(*)"));
        Assert.That(sql.CommandText, Does.Contain("SUM("));
        Assert.That(sql.CommandText, Does.Contain("AVG("));
        Assert.That(sql.CommandText, Does.Not.Contain("SELECT Count(*)"));
        Assert.That(sql.CommandText, Does.Not.Contain("Average("));
        Assert.That(records, Is.Not.Empty);
        Assert.That(records[0]["InvoiceCount"], Is.EqualTo(1L));
    }

    [Test]
    public void DefinitionConnectsStructuredFiltersWithLogicalOperators()
    {
        var definition = new QueryRequestDto
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
        var definition = new QueryRequestDto
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
        var definition = new QueryRequestDto
        {
            Entity = "User",
            Columns = [new QueryColumn("Id"), new QueryColumn("Name")],
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

        var definition = System.Text.Json.JsonSerializer.Deserialize<QueryRequestDto>(
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

        Assert.Throws<FormatException>(() => QueryRequestDto.FromJson(json));
        Assert.Throws<FormatException>(() =>
            QueryRequestDto.FromJson("""{"entity":"User","filter":"Age > (12 + OtherAge)"}"""));
    }
}
