using Demo.Contracts.Application;
using Demo.Contracts.Dto;
using Demo.Contracts.Repositories;
using Demo.Contracts.Services;

namespace Demo.Services;

public sealed class QueryRequestService(
    IQueryRequestRepository repository,
    IUserSectionManager userSectionManager) : IQueryRequestService
{
    public IReadOnlyList<QueryRequestDto> List()
    {
        var sectionId = userSectionManager.GetSectionId();
        var builtin = repository.GetBuiltin();
        var builtinNames = builtin.Select(query => query.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var custom = repository.GetUserDefined(sectionId);
        var conflict = custom.FirstOrDefault(query => builtinNames.Contains(query.Name));
        if (conflict is not null)
            throw new InvalidDataException(
                $"Custom query request '{conflict.Name}' cannot replace a built-in request.");

        return builtin.Concat(custom).ToArray();
    }

    public void Save(string name, QueryRequestDto queryRequest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(queryRequest);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryRequest.Entity);
        if (!string.Equals(queryRequest.Name, name, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Query name must match the requested file name.", nameof(queryRequest));
        if (repository.GetBuiltin().Any(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Built-in query request '{name}' cannot be changed.");

        repository.Save(userSectionManager.GetSectionId(), queryRequest with { IsBuiltin = false });
    }

    public bool IsBuiltin(string name)
        => repository.GetBuiltin().Any(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

    public bool Delete(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return !IsBuiltin(name) && repository.Delete(userSectionManager.GetSectionId(), name);
    }

}
