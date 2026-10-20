using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
#if NET10_0
using Demo.Contracts.Application;
using Demo.Contracts.Dto;
using Demo.Contracts.Repositories;
using Demo.Repositories.FileStorage;
using Demo.Share;
using Demo.Services;
using Microsoft.Extensions.Configuration;
#endif

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
internal sealed class _35_RelationTests : TestBase
{
    [Test]
    public void QueryDefinitionCreatesIndependentRelationsWithDifferentKeyMappings()
    {
        var query = _db.Query(new QueryDefinition
        {
            Entity = "InvoiceItems",
            Relations =
            [
                new("Product1", "Product", [new("ProductId", "Id")]),
                new("Product2", "Product", [new("Id", "Id")])
            ],
            Projection = QueryDefinitionParser.ParseProjection(
                "Product.Name|MetadataProduct, Product1.Name|ProductByForeignKey, Product2.Name|ProductById"),
            Filter = QueryDefinitionParser.ParseFilter("Product1.Id < Product2.Id")
        });

        var sql = query.ToSql().CommandText;

        Assert.That(sql.Split("LEFT JOIN").Length - 1, Is.EqualTo(3));
        Assert.That(sql, Does.Contain("t1.\"Id\" = t0.\"ProductId\""));
        Assert.That(sql, Does.Contain("t2.\"Id\" = t0.\"Id\""));
        Assert.That(sql, Does.Contain("t3.\"Id\" = t0.\"ProductId\""));
        Assert.That(sql, Does.Contain("t1.\"Id\" < t2.\"Id\""));
    }

    [Test]
    public void QueryDefinitionYmlParsesRelations()
    {
        var definition = QueryDefinitionParser.ParseFromYml("""
            Entity: InvoiceItems
            Relations:
              - "Product2: Product(Id)"
            """);

        Assert.That(definition.Relations, Has.Count.EqualTo(1));
        Assert.That(definition.Relations[0].Name, Is.EqualTo("Product2"));
        Assert.That(definition.Relations[0].RelatedKeys[0], Is.EqualTo(new RelationItem("Id", "Id")));
    }

    [Test]
    public void RelationCanCloneNestedRelationPathForCrossEntityComparison()
    {
        var query = _db.Query(new QueryDefinition
        {
            Entity = "InvoiceItems",
            Relations = [new("OtherItem", "Invoice.Items", [new("Id", "InvoiceId")])],
            Projection = QueryDefinitionParser.ParseProjection(
                "Product.Name|Product, OtherItem.Product.Name|FrequentlyBoughtWith, Count()|OrdersTogether"),
            Filter = QueryDefinitionParser.ParseFilter("Product.Id < OtherItem.Product.Id")
        });

        var sql = query.ToSql().CommandText;

        Assert.That(sql, Does.Contain("t1.\"Id\" = t0.\"InvoiceId\""));
        Assert.That(sql, Does.Contain("t2.\"InvoiceId\" = t1.\"Id\""));
        Assert.That(sql, Does.Contain("t3.\"Id\" < t4.\"Id\""));
        Assert.That(sql, Does.Contain("GROUP BY"));
    }

    [Test]
    public void QueryDefinitionRequiresKeysForEveryRelation()
    {
        var query = _db.Query(new QueryDefinition
        {
            Entity = "InvoiceItems",
            Relations = [new("Product2", "Product", [])],
            Projection = QueryDefinitionParser.ParseProjection("Product2.Name")
        });

        var exception = Assert.Throws<FormatException>(() => query.ToSql());

        Assert.That(exception!.Message, Is.EqualTo("Relation [Product2] requires at least one relation key."));
    }
}
