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
                new QueryColumn("Name"),
                new QueryColumn("Manager.Name", "ManagerName")
            ],
            Filter = new BinaryFilter("Age", QueryFilterOperator.GreaterThan, 18),
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
        var definition = new QueryRequest
        {
            Entity = "User",
            Columns = [new QueryColumn("Name")],
            Filter = new BinaryFilter("Name", QueryFilterOperator.Equals, "Alice")
        };

        var records = _db.Query(definition).ToList();

        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0]["Name"], Is.EqualTo("Alice"));
    }

    [Test]
    public void DefinitionConnectsStructuredFiltersWithLogicalOperators()
    {
        var definition = new QueryRequest
        {
            Entity = "User",
            Filter = new LogicalFilter(
                new LogicalFilter(
                    new BinaryFilter("Name", QueryFilterOperator.Equals, "Alice"),
                    QueryFilterLogicalOperator.Or,
                    new BinaryFilter("Age", QueryFilterOperator.GreaterThan, 18)),
                QueryFilterLogicalOperator.And,
                new BinaryFilter("Name", QueryFilterOperator.Equals, "Bob"))
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
            Columns = [new QueryColumn("Id"), new QueryColumn("Name")],
            Filter = new LogicalFilter(
                new LogicalFilter(
                    new BinaryFilter("Id", QueryFilterOperator.GreaterThan, 3),
                    QueryFilterLogicalOperator.And,
                    new BinaryFilter("Invoices.Id", QueryFilterOperator.LessThan, 3)),
                QueryFilterLogicalOperator.And,
                new LogicalFilter(
                    new BinaryFilter("Name", QueryFilterOperator.Equals, "abc"),
                    QueryFilterLogicalOperator.Or,
                    new BinaryFilter("Age", QueryFilterOperator.GreaterThan, 18))),
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
                "field": "Name",
                "operator": 0,
                "value": "Alice",
                "parameterized": false,
                "enabled": true
              }
            }
            """;

        var definition = System.Text.Json.JsonSerializer.Deserialize<QueryRequest>(
            json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.That(definition.Filter, Is.TypeOf<BinaryFilter>());
        Assert.That(definition.Filter!.ToString(), Does.Contain("Alice"));
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
}
