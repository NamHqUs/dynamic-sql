using Namh.Data.DynamicSql.QueryBuilderDto;
using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;
using NUnit.Framework.Internal;
using System.Text.RegularExpressions;

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

        QueryDto qureyBuilder = new()
        {
            Name = "6. Outstanding receivables and aging",
            Entity = "Invoices",
            Fields = [
                new (new FieldDto("InvoiceNumber")),
                new (new FieldDto("Customer.Name"), "Name"),
                new (new FieldDto("InvoiceDate")),
                new (new FieldDto("DueDate")),
                new (new FieldDto("TotalAmount")),
                new (new FunctionFieldDto("DateSub", [
                    new FieldDto("DueDate"),
                    new FunctionFieldDto("GETDATE", []),
                    ]), "DaysOverdue")
                ],
            Filter = new FilterDto(
                LogicalOperator.And,
                new FilterItemDto(
                    null,
                    new FieldDto("Status"),
                    BinaryOperator.Equals,
                    new ConstFieldDto("Sent"))),
            Sorts = [new(new FieldDto("DaysOverdue"), QuerySortDirection.Descending)]
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
        
        QueryDto query = new()
        {
            Name = "7. Paid collection time by payment method",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("PaymentMethod, Count(), AVG[DateSub[InvoiceDate, PaidOn]] | AverageDaysToPay, Sum(TotalAmount) | Collected"),
            Filter = QueryDtoParser.ParseFilter("Status = 'Paid'"),
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
    public void Demo13()
    {
        /// 13. Discount impact by month
        /// SELECT DateFormat(InvoiceDate, '%Y-%m') AS Month, SUM(DiscountAmount) AS Discounts,
        ///        SUM(TotalAmount) AS NetRevenue,
        /// ROUND(SUM(DiscountAmount) * 100 / NULLIF(SUM(TotalAmount), 0), 2) AS DiscountRate
        /// FROM Invoices GROUP BY Month ORDER BY Month;

        QueryDto query = new()
        {
            Name = "13. Discount impact by month",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("DateFormat(InvoiceDate, <%Y-%m>) | Month, SUM(DiscountAmount) | Discounts, SUM(TotalAmount) | NetRevenue, ROUND(SUM(DiscountAmount) * <100> / NULLIF(SUM(TotalAmount), <0>), <2>) | DiscountRate"),
            Sorts = [new(new FieldDto("Month"))]
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
}
