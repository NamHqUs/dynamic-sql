using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;

namespace Namh.Data.DynamicSql.Test;

[TestFixture]
internal class _23_FilterParserTest : TestBase
{
    [Test]
    public void Filter_FromText()
    {
        var query = new QueryDto
        {
            Entity = "Invoices",
            Fields = ProjectionDto.FromTextList("Year(InvoiceDate) | Year, Month(InvoiceDate) | Month, Count() | InvoiceCount, Sum(TotalAmount) | Revenue, Average(TotalAmount) | AverageOrderValue"),
            Filter = FilterDtoParser.Parse("Status <> <Draft> And Sum(TotalAmount) > 200"),
        };

        var queryText = query.ToString();
        Assert.That(queryText, Does.Contain("Filters: Status <> <Draft> AND Sum(TotalAmount) > <200>"));

        var data = _db.Query(query).ToList();
    }
}
