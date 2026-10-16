using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
internal sealed class _34_SalesReportQueryTests : TestBase
{
    public static IEnumerable<TestCaseData> ReportQueries()
    {
        yield return Case(1, "MonthlyRevenueAndInvoiceVolume", new QueryDto
        {
            Name = "1. Monthly revenue and invoice volume",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("DateFormat[InvoiceDate, <%Y-%m>] | Month, COUNT[] | InvoiceCount, SUM[TotalAmount] | Revenue, AVG[TotalAmount] | AverageOrderValue"),
            Filter = Filter("Status", BinaryOperator.NotEquals, "Draft"),
            Sorts = [Sort("Month")]
        }, 3, "DateFormat[InvoiceDate, <%Y-%m>]");

        yield return Case(2, "RevenueByCustomerSegmentAndRegion", new QueryDto
        {
            Name = "2. Revenue by customer segment and region",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("Customer.Segment, Customer.Region, COUNT[] | InvoiceCount, SUM[TotalAmount] | Revenue"),
            Filter = Filter("Status", BinaryOperator.NotEquals, "Draft"),
            Sorts = [Sort("Revenue", QuerySortDirection.Descending)]
        }, null, "Customer.Segment", "Customer.Region");

        yield return Case(3, "TopCustomersByLifetimeValue", new QueryDto
        {
            Name = "3. Top customers by lifetime value",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("Customer.Id, Customer.Name, COUNT[] | Orders, SUM[TotalAmount] | LifetimeValue"),
            Filter = Filter("Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("LifetimeValue", QuerySortDirection.Descending)],
            PageSize = 10
        }, 10, "Customer.Id", "Customer.Name");

        yield return Case(4, "BestSellingProductsByUnitsAndRevenue", new QueryDto
        {
            Name = "4. Best-selling products by units and revenue",
            Entity = "Products",
            Fields = QueryDtoParser.ParseProjections("Name, Category, SUM[InvoiceItems.Quantity] | UnitsSold, SUM[InvoiceItems.LineTotal] | Revenue"),
            Filter = Filter("InvoiceItems.Invoice.Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("Revenue", QuerySortDirection.Descending)]
        }, 9, "Id", "Name", "Category");

        yield return Case(5, "GrossMarginByProduct", new QueryDto
        {
            Name = "5. Gross margin by product",
            Entity = "Products",
            Fields = QueryDtoParser.ParseProjections("Name, SUM[InvoiceItems.Quantity] | UnitsSold, SUM[InvoiceItems.LineTotal - (InvoiceItems.Quantity * CostPrice)] | GrossProfit, ROUND[SUM[InvoiceItems.LineTotal - (InvoiceItems.Quantity * CostPrice)] * <100> / NULLIF[SUM[InvoiceItems.LineTotal], <0>], <2>] | MarginPercent"),
            Filter = Filter("InvoiceItems.Invoice.Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("GrossProfit", QuerySortDirection.Descending)]
        }, 9, "Id", "Name");

        yield return Case(7, "PaidCollectionTimeByPaymentMethod", new QueryDto
        {
            Name = "7. Paid collection time by payment method",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("PaymentMethod, COUNT[] | PaidInvoices, AVG[DateSub[InvoiceDate, PaidOn]] | AverageDaysToPay, SUM[TotalAmount] | Collected"),
            Filter = Filter("Status", BinaryOperator.Equals, "Paid")
        }, 3, "PaymentMethod");

        yield return Case(8, "SalesChannelPerformance", new QueryDto
        {
            Name = "8. Sales-channel performance",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("SalesChannel, COUNT[] | Orders, SUM[TotalAmount] | Revenue, AVG[TotalAmount] | AverageOrderValue"),
            Filter = Filter("Status", BinaryOperator.NotEquals, "Draft"),
            Sorts = [Sort("Revenue", QuerySortDirection.Descending)]
        }, 3, "SalesChannel");

        yield return Case(9, "ProductAttachAndCrossSellPairs", new QueryDto
        {
            Name = "9. Product attach and cross-sell pairs",
            Entity = "InvoiceItems",
            Fields = QueryDtoParser.ParseProjections("InvoiceId, ProductId, Product.Name | Product"),
            Sorts = [Sort("InvoiceId"), Sort("ProductId")]
        }, 57);

        yield return Case(10, "CustomersWithRepeatPurchases", new QueryDto
        {
            Name = "10. Customers with repeat purchases",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("Customer.Name, COUNT[] | OrderCount, Max(InvoiceDate) | LastOrderDate"),
            Filter = Filter("Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("LastOrderDate", QuerySortDirection.Descending)]
        }, 10, "Customer.Id", "Customer.Name");

        yield return Case(11, "CustomersWithNoPaidOrder", new QueryDto
        {
            Name = "11. Customers with no paid order",
            Entity = "Customers",
            Fields = QueryDtoParser.ParseProjections("Id, Name, Segment, Region"),
            Sorts = [Sort("Name")]
        }, 10);

        yield return Case(12, "LowStockProductsAndInventoryValue", new QueryDto
        {
            Name = "12. Low-stock products and inventory value",
            Entity = "Products",
            Fields = QueryDtoParser.ParseProjections("Name, Category, StockQuantity, ReorderLevel, StockQuantity * CostPrice | InventoryCost"),
            Filter = Filter("StockQuantity", BinaryOperator.LessThanOrEqual, new FieldDto("ReorderLevel")),
            Sorts = [Sort("StockQuantity")]
        }, 0);

        yield return Case(13, "DiscountImpactByMonth", new QueryDto
        {
            Name = "13. Discount impact by month",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("DateFormat[InvoiceDate, <%Y-%m>] | Month, SUM[DiscountAmount] | Discounts, SUM[TotalAmount] | NetRevenue, ROUND[SUM[DiscountAmount] * <100> / NULLIF[SUM[TotalAmount], <0>], <2>] | DiscountRate"),
            Sorts = [Sort("Month")]
        }, 4, "DateFormat[InvoiceDate, <%Y-%m>]");

        yield return Case(14, "RevenueByWeekday", new QueryDto
        {
            Name = "14. Revenue by weekday",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("DateFormat[InvoiceDate, <%w>] | Weekday, COUNT[] | Orders, SUM[TotalAmount] | Revenue"),
            Filter = Filter("Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("Revenue", QuerySortDirection.Descending)]
        }, null, "DateFormat[InvoiceDate, <%w>]");

        yield return Case(15, "InvoiceStatusFunnel", new QueryDto
        {
            Name = "15. Invoice status funnel",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("Status, COUNT[] | InvoiceCount, SUM[TotalAmount] | PipelineValue"),
            Sorts = [Sort("PipelineValue", QuerySortDirection.Descending)]
        }, 3, "Status");

        yield return Case(16, "AverageBasketComposition", new QueryDto
        {
            Name = "16. Average basket composition",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("InvoiceNumber, Customer.Name, COUNT[Items.Id] | LineCount, SUM[Items.Quantity] | Units, TotalAmount"),
            Sorts = [Sort("TotalAmount", QuerySortDirection.Descending)]
        }, 20, "Id", "InvoiceNumber", "Customer.Name", "TotalAmount");

        yield return Case(17, "CategoryRevenueAndProfitability", new QueryDto
        {
            Name = "17. Category revenue and profitability",
            Entity = "Products",
            Fields = QueryDtoParser.ParseProjections("Category, SUM[InvoiceItems.LineTotal] | Revenue, SUM[InvoiceItems.LineTotal - (InvoiceItems.Quantity * CostPrice)] | GrossProfit"),
            Filter = Filter("InvoiceItems.Invoice.Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("Revenue", QuerySortDirection.Descending)]
        }, 4, "Category");

        yield return Case(18, "MonthlyRevenueTrendWithPriorMonth", new QueryDto
        {
            Name = "18. Monthly revenue trend with prior month",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("DateFormat[InvoiceDate, <%Y-%m>] | Month, SUM[TotalAmount] | Revenue"),
            Filter = Filter("Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("Month")]
        }, 2, "DateFormat[InvoiceDate, <%Y-%m>]");

        yield return Case(19, "CustomerRecencyFrequencyAndMonetaryValue", new QueryDto
        {
            Name = "19. Customer recency, frequency, and monetary value",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("Customer.Name, Max(InvoiceDate) | LastPurchase, COUNT[] | Frequency, SUM[TotalAmount] | MonetaryValue"),
            Filter = Filter("Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("LastPurchase", QuerySortDirection.Descending), Sort("MonetaryValue", QuerySortDirection.Descending)]
        }, 10, "Customer.Id", "Customer.Name");

        yield return Case(20, "UnpaidRevenueExposureByCustomer", new QueryDto
        {
            Name = "20. Unpaid revenue exposure by customer",
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("Customer.Name, Customer.Segment, COUNT[] | OpenInvoices, SUM[TotalAmount] | AmountDue, Min(DueDate) | OldestDueDate"),
            Filter = Filter("Status", BinaryOperator.Equals, "Sent"),
            Sorts = [Sort("AmountDue", QuerySortDirection.Descending)]
        }, 6, "Customer.Id", "Customer.Name", "Customer.Segment");
    }

    [TestCaseSource(nameof(ReportQueries))]
    public void QueryDtoExecutesSalesReport(QueryDto definition, int? expectedRowCount, string[] groupBy)
    {
        var groupedAndPaged = groupBy.Length > 0 && (definition.Page is not null || definition.PageSize is not null);
        var queryDefinition = groupedAndPaged
            ? new QueryDto
            {
                Name = definition.Name,
                Entity = definition.Entity,
                Fields = definition.Fields,
                Filter = definition.Filter,
                Sorts = definition.Sorts,
                IncludeArchive = definition.IncludeArchive
            }
            : definition;
        var query = _db.Query(queryDefinition);
        if (groupBy.Length > 0)
            query = query.GroupBy(groupBy);
        if (groupedAndPaged)
        {
            var page = definition.Page ?? 1;
            var pageSize = definition.PageSize ?? 50;
            query = query.Skip((page - 1) * pageSize).TakeQuery(pageSize);
        }

        var sql = query.ToSql();
        var records = query.ToList();

        Assert.That(sql.CommandText, Is.Not.Empty, definition.Name);
        if (expectedRowCount is int count)
            Assert.That(records, Has.Count.EqualTo(count), definition.Name);
        else
            Assert.That(records, Is.Not.Empty, definition.Name);

        if (definition.Name!.StartsWith("9.", StringComparison.Ordinal))
            AssertProductPairCounts(records);
        else if (definition.Name.StartsWith("11.", StringComparison.Ordinal))
            AssertCustomersWithoutPaidOrders(records);
        else if (definition.Name.StartsWith("18.", StringComparison.Ordinal))
            AssertMonthlyRevenueChanges(records);
    }

    private void AssertProductPairCounts(IReadOnlyList<DataRecord> invoiceItems)
    {
        var pairCounts = invoiceItems
            .GroupBy(record => Convert.ToInt32(record["InvoiceId"]))
            .SelectMany(invoice => ProductPairs(invoice.Select(record => Convert.ToInt32(record["ProductId"]))))
            .GroupBy(pair => pair)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.That(pairCounts[(1, 2)], Is.EqualTo(2));
    }

    private void AssertCustomersWithoutPaidOrders(IReadOnlyList<DataRecord> customers)
    {
        var paidCustomerIds = _db.Query(new QueryDto
        {
            Entity = "Invoices",
            Fields = QueryDtoParser.ParseProjections("CustomerId"),
            Filter = Filter("Status", BinaryOperator.Equals, "Paid")
        }).ToList()
            .Select(record => Convert.ToInt32(record["CustomerId"]))
            .ToHashSet();

        var customersWithoutPaidOrders = customers
            .Where(record => !paidCustomerIds.Contains(Convert.ToInt32(record["Id"])))
            .Select(record => record["Name"]);

        Assert.That(customersWithoutPaidOrders, Is.Empty);
    }

    private static void AssertMonthlyRevenueChanges(IReadOnlyList<DataRecord> records)
    {
        Assert.That(records.Select(record => record["Month"]), Is.EqualTo(new object?[] { "2025-02", "2025-03" }));

        var februaryRevenue = Convert.ToDecimal(records[0]["Revenue"]);
        var marchRevenue = Convert.ToDecimal(records[1]["Revenue"]);
        Assert.That(februaryRevenue, Is.EqualTo(2364m));
        Assert.That(marchRevenue - februaryRevenue, Is.EqualTo(-650m));
    }

    private static IEnumerable<(int, int)> ProductPairs(IEnumerable<int> productIds)
    {
        var products = productIds.Order().ToArray();
        for (var first = 0; first < products.Length; first++)
        for (var second = first + 1; second < products.Length; second++)
            yield return (products[first], products[second]);
    }

    private static TestCaseData Case(int number, string name, QueryDto query, int? expectedRowCount, params string[] groupBy)
        => new TestCaseData(query, expectedRowCount, groupBy)
            .SetName($"Demo{number}_{name}");

    private static FilterDto Filter(string field, BinaryOperator operation, object? value)
        => new(
            LogicalOperator.And,
            new FilterItemDto(
                null,
                new FieldDto(field),
                operation,
                value is IFieldDto fieldValue ? fieldValue : new ConstFieldDto(value)));

    private static SortDto Sort(string field, QuerySortDirection direction = QuerySortDirection.Ascending)
        => new(new FieldDto(field), direction);
}
