using Namh.Data.DynamicSql;

namespace Demo.WebApi.Services;
public static class MetadataServices
    {
        private static List<Entity> _metadata =
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

    public static List<Entity> GetMetadata()
        => _metadata;

    public static IMetaProvider GetMetaProvider()
        => new MetaProvider(_metadata);
}
