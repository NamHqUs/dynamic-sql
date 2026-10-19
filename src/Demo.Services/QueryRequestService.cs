using Demo.Contracts.Application;
using Demo.Contracts.Dto;
using Demo.Contracts.Repositories;
using Demo.Contracts.Services;
using Namh.Data.DynamicSql;

namespace Demo.Services;

public sealed class QueryRequestService(
    IQueryRequestRepository repository,
    IUserSectionManager userSectionManager) : IQueryRequestService
{
    public IReadOnlyList<SavedQueryRequestDto> List()
    {
        var sectionId = userSectionManager.GetSectionId();
        var builtin = repository.GetBuiltin();
        var builtinNames = builtin.Select(GetName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var custom = repository.GetUserDefined(sectionId);
        var conflict = custom.FirstOrDefault(query => builtinNames.Contains(GetName(query)));
        if (conflict is not null)
            throw new InvalidDataException(
                $"Custom query request '{GetName(conflict)}' cannot replace a built-in request.");

        return builtin.Select(query => new SavedQueryRequestDto(GetName(query), query, true))
            .Concat(custom.Select(query => new SavedQueryRequestDto(GetName(query), query, false)))
            .ToArray();
    }

    public void Save(string name, QueryDefinition query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(query);
        if (!string.Equals(query.Name, name, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Query name must match the requested file name.", nameof(query));
        if (repository.GetBuiltin().Any(item =>
            string.Equals(GetName(item), name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Built-in query request '{name}' cannot be changed.");

        repository.Save(userSectionManager.GetSectionId(), query);
    }

    public bool IsBuiltin(string name)
        => repository.GetBuiltin().Any(item =>
            string.Equals(GetName(item), name, StringComparison.OrdinalIgnoreCase));

    public bool Delete(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return !IsBuiltin(name) && repository.Delete(userSectionManager.GetSectionId(), name);
    }

    private static string GetName(QueryDefinition query)
        => !string.IsNullOrWhiteSpace(query.Name)
            ? query.Name
            : throw new InvalidDataException("Query request is missing its name.");
}
