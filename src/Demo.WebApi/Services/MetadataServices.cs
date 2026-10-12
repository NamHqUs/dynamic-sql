using Namh.Data.DynamicSql;

namespace Demo.WebApi.Services;

public static class MetadataServices
{
    private static List<Entity> _metadata = Reset();

    public static List<Entity> Reset()
    => _metadata = [
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
        => _metadata.ToList();

    public static Entity CreateMetadata(Entity entity)
    {
        ValidateEntity(entity);
        if (_metadata.Any(item => string.Equals(item.Name, entity.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"An entity named '{entity.Name}' already exists.");

        _metadata.Add(entity);
        return entity;
    }

    public static Entity UpdateMetadata(string entityName, Entity entity)
    {
        ValidateEntity(entity);
        var index = _metadata.FindIndex(item => string.Equals(item.Name, entityName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            throw new KeyNotFoundException($"Entity '{entityName}' was not found.");
        if (_metadata.Where((_, itemIndex) => itemIndex != index)
            .Any(item => string.Equals(item.Name, entity.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"An entity named '{entity.Name}' already exists.");

        _metadata[index] = entity;
        return entity;
    }

    public static void DeleteMetadata(string entityName)
    {
        var index = _metadata.FindIndex(item => string.Equals(item.Name, entityName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            throw new KeyNotFoundException($"Entity '{entityName}' was not found.");

        _metadata.RemoveAt(index);
    }

    private static void ValidateEntity(Entity entity)
    {
        if (entity is null || string.IsNullOrWhiteSpace(entity.Name))
            throw new ArgumentException("Entity name is required.");
        if (entity.Fields is null || entity.Fields.Count == 0)
            throw new ArgumentException("An entity must contain at least one field.");
        if (entity.Fields.Any(field => string.IsNullOrWhiteSpace(field.Name)))
            throw new ArgumentException("Every field must have a name.");
        if (entity.Fields.Select(field => field.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entity.Fields.Count)
            throw new ArgumentException("Field names must be unique within an entity.");
    }

    public static IMetaProvider GetMetaProvider()
        => new MetaProvider(_metadata);
}
