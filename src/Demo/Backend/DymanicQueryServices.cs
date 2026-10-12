#!
#:project ../../Namh.Data.DynamicSql/Namh.Data.DynamicSql.csproj
#:include Shared.cs

using System.Data;
using System.Text.Json;
using Namh.Data.DynamicSql;


public static class DynamicQueryServices
{
    private static readonly IDbConnection _cn = DbConnection.Value;
    private static readonly Entity[] _metadata = LoadMetadata().ToArray();
    public static Entity[] GetMetadata() => _metadata;

    public static string GetSqlQueryStringAsync(QueryRequestDto query)
    {
        Console.WriteLine($"\n{query}\n");
        var context = new DynamicSqlContext(_cn, _metadata);
        var result = context.Query(query).ToSql();
        return result.CommandText;
    }

    public static async Task<TableDataDto> ExecuteQueryAsync(QueryRequestDto query)
    {
        Console.WriteLine($"\n{query}\n");
        var context = new DynamicSqlContext(_cn, _metadata);
        var result = await new DynamicSqlContext(_cn, _metadata).Query(query).ToListAsync();

        var rows = result.Select(row => string.Join(",", row.Select(v => v.Value))).ToArray();
        Console.WriteLine(string.Join("\n", rows));

        return new TableDataDto(
            Name: query.Entity,
            Columns: string.Join(",", query.Columns.Select(c => c.Alias ?? c.Field)),
            Rows: rows);
    }


    private static List<Entity> LoadMetadata() => 
    [
        new(
            Name: "Customers",
            Fields: [
                new("Id", DataType.Number),
                new("Name"),
                new("Email"),
                new("Phone"),
                new("Address"),
                new("Segment"),
                new("Region"),
                new("CustomerSince", DataType.Datetime),
                new("CreatedOn", DataType.Datetime),
                new("UpdatedOn", DataType.Datetime),
                new("DeletedOn", DataType.Datetime)
            ],
            Relations: [
                new(
                    Name: "Invoices",
                    RelatedEntity: "Invoices",
                    Type: RelationType.Collection,
                    RelatedKeys: [new("Id", "CustomerId")])
            ]),
        new(
            Name: "Products",
            Fields: [
                new("Id", DataType.Number),
                new("Name"),
                new("Description"),
                new("Category"),
                new("UnitPrice", DataType.Number),
                new("CostPrice", DataType.Number),
                new("StockQuantity", DataType.Number),
                new("ReorderLevel", DataType.Number),
                new("CreatedOn", DataType.Datetime),
                new("UpdatedOn", DataType.Datetime),
                new("DeletedOn", DataType.Datetime)
            ],
            Relations: [
                new(
                    Name: "InvoiceItems",
                    RelatedEntity: "InvoiceItems",
                    Type: RelationType.Collection,
                    RelatedKeys: [new("Id", "ProductId")])
            ]),
        new(
            Name: "Invoices",
            Fields: [
                new("Id", DataType.Number),
                new("CustomerId", DataType.Number),
                new("InvoiceNumber"),
                new("InvoiceDate", DataType.Datetime),
                new("DueDate", DataType.Datetime),
                new("Status"),
                new("SalesChannel"),
                new("PaymentMethod"),
                new("PaidOn", DataType.Datetime),
                new("DiscountAmount", DataType.Number),
                new("TaxAmount", DataType.Number),
                new("TotalAmount", DataType.Number),
                new("CreatedOn", DataType.Datetime),
                new("UpdatedOn", DataType.Datetime),
                new("DeletedOn", DataType.Datetime)
            ],
            Relations: [
                new(
                    Name: "Customer",
                    RelatedEntity: "Customers",
                    Type: RelationType.Lookup,
                    RelatedKeys: [new("CustomerId", "Id")]),
                new(
                    Name: "Items",
                    RelatedEntity: "InvoiceItems",
                    Type: RelationType.Collection,
                    RelatedKeys: [new("Id", "InvoiceId")])
            ]),
        new(
            Name: "InvoiceItems",
            Fields: [
                new("Id", DataType.Number),
                new("InvoiceId", DataType.Number),
                new("ProductId", DataType.Number),
                new("Quantity", DataType.Number),
                new("UnitPrice", DataType.Number),
                new("DiscountAmount", DataType.Number),
                new("LineTotal", DataType.Number),
                new("CreatedOn", DataType.Datetime),
                new("UpdatedOn", DataType.Datetime),
                new("DeletedOn", DataType.Datetime)
            ],
            Relations: [
                new(
                    Name: "Invoice",
                    RelatedEntity: "Invoices",
                    Type: RelationType.Lookup,
                    RelatedKeys: [new("InvoiceId", "Id")]),
                new(
                    Name: "Product",
                    RelatedEntity: "Products",
                    Type: RelationType.Lookup,
                    RelatedKeys: [new("ProductId", "Id")])
            ])
    ];
}
