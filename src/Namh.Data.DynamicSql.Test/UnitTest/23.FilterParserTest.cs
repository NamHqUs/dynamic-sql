using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

namespace Namh.Data.DynamicSql.Test;

[TestFixture]
internal class _23_FilterParserTest : TestBase
{
    [Test]
    public void Filter_FromText()
    {
        var query = new QueryDefinition
        {
            Entity = "Invoices",
            Projection = QueryDefinitionParser.ParseProjection("Year(InvoiceDate) | Year, Month(InvoiceDate) | Month, Count() | InvoiceCount, Sum(TotalAmount) | Revenue, Average(TotalAmount) | AverageOrderValue"),
            Filter = QueryDefinitionParser.ParseFilter("Status <> <Draft> And Sum(TotalAmount) > 200"),
        };

        var queryText = query.ToString();
        Assert.That(queryText, Does.Contain("Filter: Status <> <Draft> AND Sum(TotalAmount) > <200>"));

        var data = _db.Query(query).ToList();
        Assert.That(data.Count, Is.GreaterThan(0));
    }
}
