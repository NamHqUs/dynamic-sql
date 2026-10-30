using Microsoft.Data.Sqlite;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public sealed class _17_FieldMetadataTests
{
    [Test]
    public void RawSqlFieldRendersWithTheCurrentTableAlias()
    {
        var metadata = new MetaProvider(
        [
            new Entity(
                "OrderLine",
                [
                    new Field("Quantity", "Quantity"),
                    new Field("UnitPrice", "UnitPrice"),
                    new Field("LineTotal", "@Quantity * @UnitPrice", IsRawSql: true)
                ])
        ]);
        using var db = new DynamicSqlContext(new SqliteConnection(), metadata, new SqliteDialect());

        var sql = db.Query("OrderLine").Select("LineTotal").ToSql().CommandText;

        Assert.That(sql, Does.Contain("(t0.Quantity * t0.UnitPrice)"));
    }

    [Test]
    public void DbFieldRendersItsPhysicalColumnName()
    {
        var metadata = new MetaProvider(
        [
            new Entity("Customer", [new Field("DisplayName", "display_name")])
        ]);
        using var db = new DynamicSqlContext(new SqliteConnection(), metadata, new SqliteDialect());

        var sql = db.Query("Customer").Select("DisplayName").ToSql().CommandText;

        Assert.That(sql, Does.Contain("t0.\"display_name\""));
    }

    [Test]
    public void DbFieldUsesItsNameWhenNoPhysicalColumnNameIsSpecified()
    {
        var metadata = new MetaProvider(
        [
            new Entity("Customer", [new Field("DisplayName")])
        ]);
        using var db = new DynamicSqlContext(new SqliteConnection(), metadata, new SqliteDialect());

        var sql = db.Query("Customer").Select("DisplayName").ToSql().CommandText;

        Assert.That(sql, Does.Contain("t0.\"DisplayName\""));
    }
}
