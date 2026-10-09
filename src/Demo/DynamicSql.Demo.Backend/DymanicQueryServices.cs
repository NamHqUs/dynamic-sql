#!
#:project ../../Namh.Data.DynamicSql/Namh.Data.DynamicSql.csproj
#:include Shared.cs

using System.Data;
using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.Model;


public static class DynamicQueryServices
{
    private static readonly IDbConnection _cn = DbConnection.Value;
    private static readonly Entity[] _metadata = LoadMetadata().ToArray();
    public static Entity[] GetMetadata() => _metadata;

    public static async Task<TableDataDto> ExecuteQueryAsync(QueryDefinition query)
    {
        var context = new DynamicSqlContext(_cn, _metadata);
        var result = await new DynamicSqlContext(_cn, _metadata).Query(query).ToListAsync();

        return new TableDataDto(
            Name: query.Entity,
            Columns: string.Join(",",  query.Columns.Select(c => c.Alias)),
            Rows: result.Select(row => string.Join(",", row.Select(v => v.Value))).ToArray());
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
                new("UnitPrice", DataType.Number),
                new("StockQuantity", DataType.Number),
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
                    Name: "InvoiceItems",
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

