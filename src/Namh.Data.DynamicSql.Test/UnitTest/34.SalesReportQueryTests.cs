using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
internal sealed class _34_SalesReportQueryTests : TestBase
{
    public static IEnumerable<TestCaseData> ReportQueries()
    {
        yield return Case(1, "MonthlyRevenueAndInvoiceVolume", new QueryDefinition
        {
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("DateFormat[InvoiceDate, <%Y-%m>] | Month, COUNT[] | InvoiceCount, SUM[TotalAmount] | Revenue, AVG[TotalAmount] | AverageOrderValue"),
            Filter = Filter("Status", BinaryOperator.NotEquals, "Draft"),
            Sorts = [Sort("Month")]
        }, 3, "DateFormat[InvoiceDate, <%Y-%m>]");

        yield return Case(2, "RevenueByCustomerSegmentAndRegion", new QueryDefinition
        {
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("Customer.Segment, Customer.Region, COUNT[] | InvoiceCount, SUM[TotalAmount] | Revenue"),
            Filter = Filter("Status", BinaryOperator.NotEquals, "Draft"),
            Sorts = [Sort("Revenue", QuerySortDirection.Descending)]
        }, null, "Customer.Segment", "Customer.Region");

        yield return Case(3, "TopCustomersByLifetimeValue", new QueryDefinition
        {
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("Customer.Id, Customer.Name, COUNT[] | Orders, SUM[TotalAmount] | LifetimeValue"),
            Filter = Filter("Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("LifetimeValue", QuerySortDirection.Descending)],
            PageSize = 10
        }, 10, "Customer.Id", "Customer.Name");

        yield return Case(4, "BestSellingProductsByUnitsAndRevenue", new QueryDefinition
        {
            Entity = "Products",
            Fields = QueryDefinitionParser.ParseProjections("Name, Category, SUM[InvoiceItems.Quantity] | UnitsSold, SUM[InvoiceItems.LineTotal] | Revenue"),
            Filter = Filter("InvoiceItems.Invoice.Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("Revenue", QuerySortDirection.Descending)]
        }, 9, "Id", "Name", "Category");

        yield return Case(5, "GrossMarginByProduct", new QueryDefinition
        {
            Entity = "Products",
            Fields = QueryDefinitionParser.ParseProjections("Name, SUM[InvoiceItems.Quantity] | UnitsSold, SUM[InvoiceItems.LineTotal - (InvoiceItems.Quantity * CostPrice)] | GrossProfit, ROUND[SUM[InvoiceItems.LineTotal - (InvoiceItems.Quantity * CostPrice)] * <100> / NULLIF[SUM[InvoiceItems.LineTotal], <0>], <2>] | MarginPercent"),
            Filter = Filter("InvoiceItems.Invoice.Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("GrossProfit", QuerySortDirection.Descending)]
        }, 9, "Id", "Name");

        yield return Case(7, "PaidCollectionTimeByPaymentMethod", new QueryDefinition
        {
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("PaymentMethod, COUNT[] | PaidInvoices, AVG[DateSub[InvoiceDate, PaidOn]] | AverageDaysToPay, SUM[TotalAmount] | Collected"),
            Filter = Filter("Status", BinaryOperator.Equals, "Paid")
        }, 3, "PaymentMethod");

        yield return Case(8, "SalesChannelPerformance", new QueryDefinition
        {
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("SalesChannel, COUNT[] | Orders, SUM[TotalAmount] | Revenue, AVG[TotalAmount] | AverageOrderValue"),
            Filter = Filter("Status", BinaryOperator.NotEquals, "Draft"),
            Sorts = [Sort("Revenue", QuerySortDirection.Descending)]
        }, 3, "SalesChannel");

        yield return Case(9, "ProductAttachAndCrossSellPairs", new QueryDefinition
        {
            Entity = "InvoiceItems",
            Fields = QueryDefinitionParser.ParseProjections("InvoiceId, ProductId, Product.Name | Product"),
            Sorts = [Sort("InvoiceId"), Sort("ProductId")]
        }, 57);

        yield return Case(10, "CustomersWithRepeatPurchases", new QueryDefinition
        {
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("Customer.Name, COUNT[] | OrderCount, Max(InvoiceDate) | LastOrderDate"),
            Filter = Filter("Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("LastOrderDate", QuerySortDirection.Descending)]
        }, 10, "Customer.Id", "Customer.Name");

        yield return Case(11, "CustomersWithNoPaidOrder", new QueryDefinition
        {
            Entity = "Customers",
            Fields = QueryDefinitionParser.ParseProjections("Id, Name, Segment, Region"),
            Sorts = [Sort("Name")]
        }, 10);

        yield return Case(12, "LowStockProductsAndInventoryValue", new QueryDefinition
        {
            Entity = "Products",
            Fields = QueryDefinitionParser.ParseProjections("Name, Category, StockQuantity, ReorderLevel, StockQuantity * CostPrice | InventoryCost"),
            Filter = Filter("StockQuantity", BinaryOperator.LessThanOrEqual, new FieldDefinition("ReorderLevel")),
            Sorts = [Sort("StockQuantity")]
        }, 0);

        yield return Case(13, "DiscountImpactByMonth", new QueryDefinition
        {
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("DateFormat[InvoiceDate, <%Y-%m>] | Month, SUM[DiscountAmount] | Discounts, SUM[TotalAmount] | NetRevenue, ROUND[SUM[DiscountAmount] * <100> / NULLIF[SUM[TotalAmount], <0>], <2>] | DiscountRate"),
            Sorts = [Sort("Month")]
        }, 4, "DateFormat[InvoiceDate, <%Y-%m>]");

        yield return Case(14, "RevenueByWeekday", new QueryDefinition
        {
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("DateFormat[InvoiceDate, <%w>] | Weekday, COUNT[] | Orders, SUM[TotalAmount] | Revenue"),
            Filter = Filter("Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("Revenue", QuerySortDirection.Descending)]
        }, null, "DateFormat[InvoiceDate, <%w>]");

        yield return Case(15, "InvoiceStatusFunnel", new QueryDefinition
        {
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("Status, COUNT[] | InvoiceCount, SUM[TotalAmount] | PipelineValue"),
            Sorts = [Sort("PipelineValue", QuerySortDirection.Descending)]
        }, 3, "Status");

        yield return Case(16, "AverageBasketComposition", new QueryDefinition
        {
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("InvoiceNumber, Customer.Name, COUNT[Items.Id] | LineCount, SUM[Items.Quantity] | Units, TotalAmount"),
            Sorts = [Sort("TotalAmount", QuerySortDirection.Descending)]
        }, 20, "Id", "InvoiceNumber", "Customer.Name", "TotalAmount");

        yield return Case(17, "CategoryRevenueAndProfitability", new QueryDefinition
        {
            Entity = "Products",
            Fields = QueryDefinitionParser.ParseProjections("Category, SUM[InvoiceItems.LineTotal] | Revenue, SUM[InvoiceItems.LineTotal - (InvoiceItems.Quantity * CostPrice)] | GrossProfit"),
            Filter = Filter("InvoiceItems.Invoice.Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("Revenue", QuerySortDirection.Descending)]
        }, 4, "Category");

        yield return Case(18, "MonthlyRevenueTrendWithPriorMonth", new QueryDefinition
        {
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("DateFormat[InvoiceDate, <%Y-%m>] | Month, SUM[TotalAmount] | Revenue"),
            Filter = Filter("Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("Month")]
        }, 2, "DateFormat[InvoiceDate, <%Y-%m>]");

        yield return Case(19, "CustomerRecencyFrequencyAndMonetaryValue", new QueryDefinition
        {
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("Customer.Name, Max(InvoiceDate) | LastPurchase, COUNT[] | Frequency, SUM[TotalAmount] | MonetaryValue"),
            Filter = Filter("Status", BinaryOperator.Equals, "Paid"),
            Sorts = [Sort("LastPurchase", QuerySortDirection.Descending), Sort("MonetaryValue", QuerySortDirection.Descending)]
        }, 10, "Customer.Id", "Customer.Name");

        yield return Case(20, "UnpaidRevenueExposureByCustomer", new QueryDefinition
        {
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("Customer.Name, Customer.Segment, COUNT[] | OpenInvoices, SUM[TotalAmount] | AmountDue, Min(DueDate) | OldestDueDate"),
            Filter = Filter("Status", BinaryOperator.Equals, "Sent"),
            Sorts = [Sort("AmountDue", QuerySortDirection.Descending)]
        }, 6, "Customer.Id", "Customer.Name", "Customer.Segment");
    }

    [TestCaseSource(nameof(ReportQueries))]
    public void QueryDtoExecutesSalesReport(
        QueryDefinition definition,
        string reportName,
        int? expectedRowCount,
        string[] groupBy)
    {
        var groupedAndPaged = groupBy.Length > 0 && (definition.Page is not null || definition.PageSize is not null);
        var queryDefinition = groupedAndPaged
            ? new QueryDefinition
            {
                Entity = definition.Entity,
                Fields = definition.Fields,
                Filter = definition.Filter,
                Sorts = definition.Sorts,
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

        Assert.That(sql.CommandText, Is.Not.Empty, reportName);
        if (expectedRowCount is int count)
            Assert.That(records, Has.Count.EqualTo(count), reportName);
        else
            Assert.That(records, Is.Not.Empty, reportName);

        if (reportName.StartsWith("9.", StringComparison.Ordinal))
            AssertProductPairCounts(records);
        else if (reportName.StartsWith("11.", StringComparison.Ordinal))
            AssertCustomersWithoutPaidOrders(records);
        else if (reportName.StartsWith("18.", StringComparison.Ordinal))
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
        var paidCustomerIds = _db.Query(new QueryDefinition
        {
            Entity = "Invoices",
            Fields = QueryDefinitionParser.ParseProjections("CustomerId"),
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

    private static TestCaseData Case(int number, string name, QueryDefinition query, int? expectedRowCount, params string[] groupBy)
        => new TestCaseData(query, name, expectedRowCount, groupBy)
            .SetName($"Demo{number}_{name}");

    private static FilterDefinition Filter(string field, BinaryOperator operation, object? value)
        => new(
            LogicalOperator.And,
            new FilterItemDto(
                null,
                new FieldDefinition(field),
                operation,
                value is IFieldDto fieldValue ? fieldValue : new ConstFieldDefinition(value)));

    private static SortDefinition Sort(string field, QuerySortDirection direction = QuerySortDirection.Ascending)
        => new(new FieldDefinition(field), direction);
}
