using Demo.ServiceContracts.Repositories;
using Demo.ServiceContracts.Services;
using Namh.Data.DynamicSql;

namespace Demo.Services;

public sealed class MetadataService : IMetadataService
{
    private readonly IMetadataRepository _metadataRepository;
    private readonly IFunctionDefinitionService _functionDefinitionService;
    private List<Entity> _metadata;

    public MetadataService(
        IMetadataRepository metadataRepository,
        IFunctionDefinitionService functionDefinitionService)
    {
        _metadataRepository = metadataRepository;
        _functionDefinitionService = functionDefinitionService;
        _metadata = LoadMetadata();
    }

    public IReadOnlyList<Entity> GetMetadata() => _metadata.ToArray();

    public Entity CreateMetadata(Entity entity)
    {
        ValidateEntity(entity);
        if (_metadata.Any(item => string.Equals(item.Name, entity.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"An entity named '{entity.Name}' already exists.");

        _metadata.Add(entity);
        _metadataRepository.Save(entity);
        return entity;
    }

    public Entity UpdateMetadata(string entityName, Entity entity)
    {
        ValidateEntity(entity);
        var index = _metadata.FindIndex(item => string.Equals(item.Name, entityName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            throw new KeyNotFoundException($"Entity '{entityName}' was not found.");
        if (_metadata.Where((_, itemIndex) => itemIndex != index)
            .Any(item => string.Equals(item.Name, entity.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"An entity named '{entity.Name}' already exists.");

        _metadata[index] = entity;
        _metadataRepository.Save(entity);
        if (!string.Equals(entityName, entity.Name, StringComparison.Ordinal))
            _metadataRepository.Delete(entityName);
        return entity;
    }

    public void DeleteMetadata(string entityName)
    {
        var index = _metadata.FindIndex(item => string.Equals(item.Name, entityName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            throw new KeyNotFoundException($"Entity '{entityName}' was not found.");

        _metadataRepository.Delete(entityName);
        _metadata.RemoveAt(index);
    }

    public IReadOnlyList<Entity> Reset() => _metadata = DefaultMetadata();

    public IMetaProvider GetMetaProvider()
        => new MetaProvider(_metadata, _functionDefinitionService.GetCustomFunctionDefinitions());

    private List<Entity> LoadMetadata()
    {
        var entities = DefaultMetadata().ToDictionary(entity => entity.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var entity in _metadataRepository.GetAll())
            entities[entity.Name] = entity;
        return entities.Values.ToList();
    }

    private static List<Entity> DefaultMetadata() =>
    [
        new(
            Name: "Customers",
            Fields:
            [
                new("Id", DataType.Number), new("Name"), new("Email"), new("Phone"), new("Address"),
                new("Segment"), new("Region"), new("CustomerSince", DataType.Datetime),
                new("CreatedOn", DataType.Datetime), new("UpdatedOn", DataType.Datetime), new("DeletedOn", DataType.Datetime)
            ],
            Relations:
            [
                new("Invoices", "Invoices", RelationType.Collection, [new("Id", "CustomerId")])
            ]),
        new(
            Name: "Products",
            Fields:
            [
                new("Id", DataType.Number), new("Name"), new("Description"), new("Category"),
                new("UnitPrice", DataType.Number), new("CostPrice", DataType.Number),
                new("StockQuantity", DataType.Number), new("ReorderLevel", DataType.Number),
                new("CreatedOn", DataType.Datetime), new("UpdatedOn", DataType.Datetime), new("DeletedOn", DataType.Datetime)
            ],
            Relations:
            [
                new("InvoiceItems", "InvoiceItems", RelationType.Collection, [new("Id", "ProductId")])
            ]),
        new(
            Name: "Invoices",
            Fields:
            [
                new("Id", DataType.Number), new("CustomerId", DataType.Number), new("InvoiceNumber"),
                new("InvoiceDate", DataType.Datetime), new("DueDate", DataType.Datetime), new("Status"),
                new("SalesChannel"), new("PaymentMethod"), new("PaidOn", DataType.Datetime),
                new("DiscountAmount", DataType.Number), new("TaxAmount", DataType.Number),
                new("TotalAmount", DataType.Number), new("CreatedOn", DataType.Datetime),
                new("UpdatedOn", DataType.Datetime), new("DeletedOn", DataType.Datetime)
            ],
            Relations:
            [
                new("Customer", "Customers", RelationType.Lookup, [new("CustomerId", "Id")]),
                new("Items", "InvoiceItems", RelationType.Collection, [new("Id", "InvoiceId")])
            ]),
        new(
            Name: "InvoiceItems",
            Fields:
            [
                new("Id", DataType.Number), new("InvoiceId", DataType.Number), new("ProductId", DataType.Number),
                new("Quantity", DataType.Number), new("UnitPrice", DataType.Number),
                new("DiscountAmount", DataType.Number), new("LineTotal", DataType.Number),
                new("CreatedOn", DataType.Datetime), new("UpdatedOn", DataType.Datetime), new("DeletedOn", DataType.Datetime)
            ],
            Relations:
            [
                new("Invoice", "Invoices", RelationType.Lookup, [new("InvoiceId", "Id")]),
                new("Product", "Products", RelationType.Lookup, [new("ProductId", "Id")])
            ])
    ];

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
}
