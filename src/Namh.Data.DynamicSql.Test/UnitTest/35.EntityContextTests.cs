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
internal sealed class _35_EntityContextTests : TestBase
{
    [Test]
    public void QueryDefinitionCreatesIndependentContextsWithDifferentKeyMappings()
    {
        var query = _db.Query(new QueryDefinition
        {
            Entity = "InvoiceItems",
            EntityAliases =
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
    public void QueryDefinitionYmlParsesEntityAliases()
    {
        var definition = QueryDefinitionParser.ParseFromYml("""
            Entity: InvoiceItems
            EntityAliases:
              - "Product2: Product(Id)"
            """);

        Assert.That(definition.EntityAliases, Has.Count.EqualTo(1));
        Assert.That(definition.EntityAliases[0].Alias, Is.EqualTo("Product2"));
        Assert.That(definition.EntityAliases[0].RelatedKeys[0], Is.EqualTo(new RelationItem("Id", "Id")));
    }

    [Test]
    public void EntityAliasCanCloneNestedRelationPathForCrossEntityComparison()
    {
        var query = _db.Query(new QueryDefinition
        {
            Entity = "InvoiceItems",
            EntityAliases = [new("OtherItem", "Invoice.Items", [new("Id", "InvoiceId")])],
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
    public void QueryDefinitionRequiresKeysForEveryEntityAlias()
    {
        var query = _db.Query(new QueryDefinition
        {
            Entity = "InvoiceItems",
            EntityAliases = [new("Product2", "Product", [])],
            Projection = QueryDefinitionParser.ParseProjection("Product2.Name")
        });

        var exception = Assert.Throws<FormatException>(() => query.ToSql());

        Assert.That(exception!.Message, Is.EqualTo("Entity alias [Product2] requires at least one relation key."));
    }
}
