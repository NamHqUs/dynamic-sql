namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public class _14_QueryDefinitionTests : TestBase
{
    [Test]
    public void DefinitionBuildsParameterizedSql()
    {
        var definition = new QueryDefinition
        {
            Entity = "User",
            Columns =
            [
                new QueryColumn("Name"),
                new QueryColumn("Manager.Name", "ManagerName")
            ],
            Filters =
            [
                new QueryFilter("Age", QueryFilterOperator.GreaterThan, 18)
            ],
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
        var definition = new QueryDefinition
        {
            Entity = "User",
            Columns = [new QueryColumn("Name")],
            Filters =
            [
                new QueryFilter("Name", QueryFilterOperator.Equals, "Alice")
            ]
        };

        var records = _db.Query(definition).ToList();

        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0]["Name"], Is.EqualTo("Alice"));
    }

    [Test]
    public void DefinitionConnectsStructuredFiltersWithLogicalOperators()
    {
        var definition = new QueryDefinition
        {
            Entity = "User",
            Filters =
            [
                new QueryFilter("Name", QueryFilterOperator.Equals, "Alice"),
                new QueryFilter("Age", QueryFilterOperator.GreaterThan, 18, LogicalOperator: QueryFilterLogicalOperator.Or),
                new QueryFilter("Name", QueryFilterOperator.Equals, "Bob", LogicalOperator: QueryFilterLogicalOperator.And),
                new QueryFilter("SchoolId", QueryFilterOperator.Equals, 12, Enabled: false)
            ]
        };

        var sql = _db.Query(definition).ToSql();

        Assert.That(sql.CommandText, Does.Contain("OR"));
        Assert.That(sql.CommandText, Does.Contain("AND"));
        Assert.That(sql.CommandText, Does.Not.Contain("= 12"));
    }

    [Test]
    public void DefinitionRejectsInvalidPaging()
    {
        var definition = new QueryDefinition
        {
            Entity = "User",
            Page = 0,
            PageSize = 10
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => _db.Query(definition));
    }

    [Test]
    public void JsonDefinitionSupportsAliasesLikeGroupingSortingAndPaging()
    {
        const string json = """
            {
              "entity": "User",
              "columns": ["Name", "Manager.Name|Manager-Name"],
              "filter": "[Name] like 'Dav%' && ([Manager.Name] == 'Alice' || [Members.TotalMembers] < (12 + [Age]))",
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

        Assert.Throws<FormatException>(() => QueryDefinition.FromJson(json));
    }
}
