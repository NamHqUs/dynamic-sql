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
        Assert.That(sql.CommandText, Does.Contain("LIMIT 10"));
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
}
