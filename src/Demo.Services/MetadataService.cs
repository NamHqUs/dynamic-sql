using Demo.Contracts.Repositories;
using Demo.Contracts.Services;
using Namh.Data.DynamicSql;

namespace Demo.Services;

public sealed class MetadataService : IMetadataService
{
    private readonly IMetadataRepository _metadataRepository;
    private readonly IFunctionDefinitionService _functionDefinitionService;

    public MetadataService(
        IMetadataRepository metadataRepository,
        IFunctionDefinitionService functionDefinitionService)
    {
        _metadataRepository = metadataRepository;
        _functionDefinitionService = functionDefinitionService;
    }

    public IReadOnlyList<Entity> GetAll() => _metadataRepository.GetAll();

    public bool IsBuiltin(string entityName) => _metadataRepository.IsBuiltin(entityName);

    public Entity Create(Entity entity)
    {
        ValidateEntity(entity);
        var metadata = _metadataRepository.GetAll();
        if (metadata.Any(item => string.Equals(item.Name, entity.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"An entity named '{entity.Name}' already exists.");

        _metadataRepository.Save(entity);
        return entity;
    }

    public Entity Update(string entityName, Entity entity)
    {
        if (_metadataRepository.IsBuiltin(entityName))
            throw new InvalidOperationException($"Built-in entity '{entityName}' is read-only.");

        ValidateEntity(entity);
        var allItems = _metadataRepository.GetAll().ToList();
        var index = allItems.FindIndex(item => string.Equals(item.Name, entityName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            throw new KeyNotFoundException($"Entity '{entityName}' was not found.");
        if (allItems.Where((_, itemIndex) => itemIndex != index)
            .Any(item => string.Equals(item.Name, entity.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"An entity named '{entity.Name}' already exists.");

        allItems[index] = entity;
        _metadataRepository.Save(entity);
        if (!string.Equals(entityName, entity.Name, StringComparison.Ordinal))
            _metadataRepository.Delete(entityName);
        return entity;
    }

    public void Delete(string entityName)
    {
        if (_metadataRepository.IsBuiltin(entityName))
            throw new InvalidOperationException($"Built-in entity '{entityName}' is read-only.");

        _metadataRepository.Delete(entityName);
    }

    public IReadOnlyList<Entity> Reset() => _metadataRepository.Reset();

    public IMetaProvider GetMetaProvider()
        => new MetaProvider(_metadataRepository.GetAll(), _functionDefinitionService.GetAll().Select(e => e.ToFunctionDefinition()));

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
