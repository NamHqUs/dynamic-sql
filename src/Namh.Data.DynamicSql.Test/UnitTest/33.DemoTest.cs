using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
using NUnit.Framework.Internal;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Namh.Data.DynamicSql.Test.UnitTest;


[TestFixture]
internal class _33_DemoTest : TestBase
{

    [Test]
    public void Demo6()
    {
        /// 6. Outstanding receivables and aging
        /// SELECT i.InvoiceNumber, c.Name, i.InvoiceDate, i.DueDate, i.TotalAmount,
        /// DateSub(i.DueDate, GETDATE()) AS DaysOverdue
        /// FROM Invoices i JOIN Customers c ON c.Id = i.CustomerId
        /// WHERE i.Status = 'Sent' ORDER BY DaysOverdue DESC;
        /// 

        QueryDefinition qureyBuilder = new()
        {
            Name = "6. Outstanding receivables and aging",
            Entity = "Invoices",
            Fields = [
                new (new FieldDefinition("InvoiceNumber")),
                new (new FieldDefinition("Customer.Name"), "Name"),
                new (new FieldDefinition("InvoiceDate")),
                new (new FieldDefinition("DueDate")),
                new (new FieldDefinition("TotalAmount")),
                new (new FunctionFieldDefinition("DateSub", [
                    new FieldDefinition("DueDate"),
                    new FunctionFieldDefinition("GETDATE", []),
                    ]), "DaysOverdue")
                ],
            Filter = new FilterDefinition(
                LogicalOperator.And,
                new FilterItemDto(
                    null,
                    new FieldDefinition("Status"),
                    BinaryOperator.Equals,
                    new ConstFieldDefinition("Sent"))),
            Sorts = [new(new FieldDefinition("DaysOverdue"), QuerySortDirection.Descending)]
        };

        var query = _db.Query(qureyBuilder);
        var data = query.ToList();

        Assert.That(data, Has.Count.EqualTo(6));
        Assert.That(data.Select(record => Convert.ToDouble(record["DaysOverdue"])),
            Is.Ordered.Descending);
        Assert.That(data.All(record => Convert.ToDouble(record["DaysOverdue"]) > 0), Is.True);
    }
    
    [Test]
    public void Demo7()
    {
        /// 7. Paid collection time by payment method
        /// SELECT PaymentMethod, COUNT(*) AS PaidInvoices,
        ///        AVG(julianday(PaidOn) - julianday(InvoiceDate)) AS AverageDaysToPay,
        ///        SUM(TotalAmount) AS Collected
        /// FROM Invoices WHERE Status = 'Paid' GROUP BY PaymentMethod;
        
        QueryDefinition query = new()
        {
            Name = "7. Paid collection time by payment method",
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("PaymentMethod, Count(), AVG[DateSub[InvoiceDate, PaidOn]] | AverageDaysToPay, Sum(TotalAmount) | Collected"),
            Filter = QueryDefinitionParser.ParseFilter("Status = 'Paid'"),
        };

        var queryable = _db.Query(query);
        var sql = queryable.ToSql();
        var data = queryable.ToList();

        Assert.That(sql.CommandText, Does.Contain("GROUP BY"));
        Assert.That(data, Has.Count.EqualTo(3));
        Assert.That(data.All(record => record.ContainsKey("Count()")), Is.True);
        Assert.That(data.Select(record => record["PaymentMethod"]).Distinct().Count(), Is.EqualTo(data.Count));
    }

    [Test]
    public void Demo9()
    {
        /// # 9. Product attach and cross-sell pairs
        /// SELECT p1.Name AS Product, p2.Name AS FrequentlyBoughtWith, COUNT(*) AS OrdersTogether
        /// FROM InvoiceItems a
        ///   JOIN InvoiceItems b ON a.InvoiceId = b.InvoiceId AND a.ProductId<b.ProductId
        ///   JOIN Products p1 ON p1.Id = a.ProductId
        ///   JOIN Products p2 ON p2.Id = b.ProductId
        /// GROUP BY a.ProductId, b.ProductId ORDER BY OrdersTogether DESC;

        QueryDefinition query = new()
        {
            Name = "9. Product attach and cross-sell pairs",
            Entity = "InvoiceItems",
            EntityAliases =
            [
                new("b", "InvoiceItems", [new("InvoiceId")])
            ],
            Fields = QueryDefinitionParser.ParseProjections("Product.Name|Product, b.Product.Name|FrequentlyBoughtWith, Count()|OrdersTogether"),
            Filter = QueryDefinitionParser.ParseFilter("Product.Id < b.Product.Id"),
            Sorts = [new(new FieldDefinition("OrdersTogether"), QuerySortDirection.Descending)]
        };

        var queryable = _db.Query(query);
        var sql = queryable.ToSql();
        var data = queryable.ToList();

        Assert.That(sql.CommandText, Does.Contain(
            "LEFT JOIN \"InvoiceItems\" t1 ON (t1.\"InvoiceId\" = t0.\"InvoiceId\")"));
        Assert.That(sql.CommandText, Does.Contain("GROUP BY"));
        Assert.That(sql.CommandText, Does.Contain("t2.\"Id\" < t3.\"Id\""));
        Assert.That(data, Is.Not.Empty);
        Assert.That(data.All(record =>
            record.ContainsKey("Product")
            && record.ContainsKey("FrequentlyBoughtWith")
            && record.ContainsKey("OrdersTogether")), Is.True);
        Assert.That(data.Select(record => Convert.ToInt64(record["OrdersTogether"])),
            Is.Ordered.Descending);
    }

    [Test]
    public void Demo9_Yml()
    {
        /// # 9. Product attach and cross-sell pairs
        /// SELECT p1.Name AS Product, p2.Name AS FrequentlyBoughtWith, COUNT(*) AS OrdersTogether
        /// FROM InvoiceItems a
        ///   JOIN InvoiceItems b ON a.InvoiceId = b.InvoiceId AND a.ProductId<b.ProductId
        ///   JOIN Products p1 ON p1.Id = a.ProductId
        ///   JOIN Products p2 ON p2.Id = b.ProductId
        /// GROUP BY a.ProductId, b.ProductId ORDER BY OrdersTogether DESC;

        string yml = """
            Name: 9. Product attach and cross-sell pairs
            Entity: InvoiceItems
            EntityAliases:
              - "b: InvoiceItems(InvoiceId)"
            Fields: 
              - Product.Name|Product 
              - b.Product.Name|FrequentlyBoughtWith
              - Count()|OrdersTogether
            Filter: Product.Id < b.Product.Id
            Sorts: OrdersTogether DESC
            """;

        QueryDefinition query = QueryDefinitionParser.ParseFromYml(yml);

        var data = _db.Query(query).ToList();
        Assert.That(data, Is.Not.Empty);
        var alias = query.EntityAliases.Single();
        Assert.That(alias.Alias, Is.EqualTo("b"));
        Assert.That(alias.RelationPath, Is.EqualTo("InvoiceItems"));
        Assert.That(alias.RelatedKeys.Single(), Is.EqualTo(new RelationItem("InvoiceId")));
        Assert.That(query.Fields, Has.Count.EqualTo(3));
        Assert.That(query.Sorts.Single().Direction, Is.EqualTo(QuerySortDirection.Descending));
    }

    [Test]
    public void ParseFromYml_AcceptsFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(path, "Entity: Products");

            var query = QueryDefinitionParser.ParseFromYml(new FileInfo(path));

            Assert.That(query.Entity, Is.EqualTo("Products"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Demo13()
    {
        /// 13. Discount impact by month
        /// SELECT DateFormat(InvoiceDate, '%Y-%m') AS Month, SUM(DiscountAmount) AS Discounts,
        ///        SUM(TotalAmount) AS NetRevenue,
        /// ROUND(SUM(DiscountAmount) * 100 / NULLIF(SUM(TotalAmount), 0), 2) AS DiscountRate
        /// FROM Invoices GROUP BY Month ORDER BY Month;

        QueryDefinition query = new()
        {
            Name = "13. Discount impact by month",
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("DateFormat(InvoiceDate, <%Y-%m>) | Month, SUM(DiscountAmount) | Discounts, SUM(TotalAmount) | NetRevenue, ROUND(SUM(DiscountAmount) * <100> / NULLIF(SUM(TotalAmount), <0>), <2>) | DiscountRate"),
            Sorts = [new(new FieldDefinition("Month"))]
        };
        var queryable = _db.Query(query);
        var sql = queryable.ToSql();
        var data = queryable.ToList();

        Assert.That(sql.CommandText, Does.Contain("GROUP BY"));
        Assert.That(data, Has.Count.EqualTo(4));
        Assert.That(data.Select(record => record["Month"]), Is.Ordered);
        Assert.That(data.All(record =>
            record.ContainsKey("Discounts")
            && record.ContainsKey("NetRevenue")
            && record.ContainsKey("DiscountRate")), Is.True);
    }

    [Test]
    public void Demo_Yaml_All()
    {
        var path = new DirectoryInfo("../../../../Demo.WebApi/FileStorage/QueryRequests").FullName;
        var files = Directory.GetFiles(path, "*.yml", SearchOption.AllDirectories);
        foreach(var file in files)
        {
            var query = QueryDefinitionParser.ParseFromYml(new FileInfo(file));
            var data = _db.Query(query).ToList();
            Assert.That(data.Count, Is.GreaterThanOrEqualTo(0));
        };
    }
}
