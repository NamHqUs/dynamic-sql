using NUnit.Framework.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Namh.Data.DynamicSql.Test.UnitTest;


[TestFixture]
internal class _33_DemoTest : TestBase
{

    [Test]
    public void Demo6()
    {
        /// 6. Outstanding receivables and aging
        /// SELECT i.InvoiceNumber, c.Name, i.InvoiceDate, i.DueDate, i.TotalAmount,
        /// DATEDIFF(DAY, i.DueDate, GETDATE()) AS DaysOverdue
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
                new (new FunctionFieldDto("DATEDIFF", [
                    new FunctionFieldDto("DAY", []),
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
}
