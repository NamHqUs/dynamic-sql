using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public sealed class _17_FieldMetadataTests
{
    [Test]
    public void FieldVariantsRoundTripWithTheirDiscriminator()
    {
        var entity = new Entity(
            "OrderLine",
            [
                new Field("CustomerName", "customer_name"),
                new RawSqlField("LineTotal", "@Quantity * @UnitPrice"),
                new Field("Quantity")
            ]);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var json = JsonSerializer.Serialize(entity, options);
        var restored = JsonSerializer.Deserialize<Entity>(json, options)!;

        Assert.That(json, Does.Contain("\"kind\":\"dbField\""));
        Assert.That(json, Does.Contain("\"kind\":\"rawSqlField\""));
        Assert.That(json, Does.Contain("\"dbColumn\":null"));
        Assert.That(restored.Fields[0], Is.EqualTo(new Field("CustomerName", "customer_name")));
        Assert.That(restored.Fields[1], Is.EqualTo(new RawSqlField("LineTotal", "@Quantity * @UnitPrice")));
        Assert.That(restored.Fields[2], Is.EqualTo(new Field("Quantity")));
    }

    [Test]
    public void LegacyFieldMetadataDeserializesIntoTheNewFieldVariants()
    {
        const string json = """
            {
              "name": "OrderLine",
              "fields": [
                {
                  "name": "CustomerName",
                  "dataType": "String",
                  "fieldType": "DbColumn",
                  "rawSql": "customer_name"
                },
                {
                  "name": "LineTotal",
                  "dataType": 1,
                  "fieldType": 1,
                  "rawSql": "@Quantity * @UnitPrice"
                }
              ]
            }
            """;

        var entity = JsonSerializer.Deserialize<Entity>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.That(entity.Fields[0], Is.EqualTo(new Field("CustomerName", "customer_name")));
        Assert.That(entity.Fields[1], Is.EqualTo(new RawSqlField("LineTotal", "@Quantity * @UnitPrice")));
    }

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
                    new RawSqlField("LineTotal", "@Quantity * @UnitPrice")
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
