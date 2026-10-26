using Demo.Contracts.Application;
using Demo.Contracts.Dto;
using Demo.Contracts.Repositories;
using Demo.Contracts.Services;

namespace Demo.Services;

public sealed class QueryRequestService(
    IQueryRequestRepository repository,
    IUserSectionManager userSectionManager) : IQueryRequestService
{
    public IReadOnlyList<QueryDto> List()
    {
        var sectionId = userSectionManager.GetSectionId();
        var builtin = repository.GetBuiltin();
        var builtinNames = builtin.Select(query => query.MainQuery.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var custom = repository.GetUserDefined(sectionId);
        var conflict = custom.FirstOrDefault(query => builtinNames.Contains(query.MainQuery.Name));
        if (conflict is not null)
            throw new InvalidDataException(
                $"Custom query request '{conflict.MainQuery.Name}' cannot replace a built-in request.");

        return builtin.Concat(custom).ToArray();
    }

    public void Save(string id, QueryDto query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.MainQuery);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.MainQuery.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.MainQuery.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.MainQuery.Entity);
        if (!string.Equals(query.MainQuery.Id, id, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Query context ID must match the requested file name.", nameof(query));
        if (repository.GetBuiltin().Any(item =>
            string.Equals(item.MainQuery.Id, id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Built-in query request '{id}' cannot be changed.");

        repository.Save(userSectionManager.GetSectionId(), query with { IsBuiltin = false });
    }

    public bool IsBuiltin(string id)
        => repository.GetBuiltin().Any(item =>
            string.Equals(item.MainQuery.Id, id, StringComparison.OrdinalIgnoreCase));

    public bool Delete(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return !IsBuiltin(id) && repository.Delete(userSectionManager.GetSectionId(), id);
    }

}
