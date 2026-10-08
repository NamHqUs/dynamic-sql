using Microsoft.Data.Sqlite;
using Namh.Data.DynamicSql.Model;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public class SqlDialectTests
{
    private static readonly IMetaProvider Metadata = new MetaProvider(
    [
        new Entity("User", [new("Id"), new("Name")])
    ]);

    [Test]
    public void SqlServerDialectUsesTopAndBracketIdentifiers()
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection();
        using var db = new DynamicSqlContext(connection, Metadata, new SqlServerDialect());

        var sql = db.Query("User").Select("Id").ToString();

        Assert.That(sql, Does.Contain("SELECT t0.[Id]"));
        Assert.That(sql, Does.Contain("FROM dbo.[User] t0"));
        Assert.That(new SqlServerDialect().RenderTakePrefix(2), Is.EqualTo("TOP 2 "));
    }

    [Test]
    public void SqliteDialectUsesLimitOffsetAndQuotedIdentifiers()
    {
        using var connection = new SqliteConnection();
        connection.ConnectionString = "Data Source=:memory:";
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE \"User\" (\"Id\" INTEGER, \"Name\" TEXT);";
            command.ExecuteNonQuery();
            command.CommandText = "INSERT INTO \"User\" (\"Id\", \"Name\") VALUES (1, 'Alice'), (2, 'Bob'), (3, 'Charlie');";
            command.ExecuteNonQuery();
        }

        using var db = new DynamicSqlContext(connection, Metadata, new SqliteDialect());
        var data = db.Query("User").Select("Id").Skip(1).ToList();

        Assert.That(data.Select(e => e["Id"]).ToArray(), Is.EqualTo(new object?[] { 2L, 3L }));
        Assert.That(new SqliteDialect().RenderPaging(3, 2), Is.EqualTo(" LIMIT 2 OFFSET 3"));
    }
}
