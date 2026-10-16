namespace Namh.Data.DynamicSql;

public interface IMetaProvider
{
    Entity GetEntity(string entityName);

    FunctionDefinition GetFunction(string functionName);

    bool TryGetFunction(string functionName, out FunctionDefinition? function);
}

public class MetaProvider : IMetaProvider
{
    private readonly Dictionary<string, Entity> _entities;
    private readonly Dictionary<string, FunctionDefinition> _functions;

    public MetaProvider(
        IEnumerable<Entity> entities,
        IEnumerable<FunctionDefinition>? functions = null)
    {
        ArgumentNullException.ThrowIfNull(entities);

        _entities = entities.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        _functions = BuiltInFunctionDefinitions.AllFunctions
            .ToDictionary(function => function.Name, StringComparer.OrdinalIgnoreCase);

        var customFunctions = functions?.ToArray() ?? [];
        if (customFunctions
            .GroupBy(function => function.Name, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
            throw new ArgumentException("Function names must be unique.", nameof(functions));

        foreach (var function in customFunctions)
            _functions[function.Name] = function;
    }

    public Entity GetEntity(string entityName)
    {
        if (_entities.TryGetValue(entityName, out Entity? entity))
            return entity;

        throw new EntityNotFoundException(entityName);
    }

    public FunctionDefinition GetFunction(string functionName)
        => !string.IsNullOrWhiteSpace(functionName) && _functions.TryGetValue(functionName, out var function)
            ? function
            : throw new FormatException($"Unsupported function or argument count [{functionName}].");

    public IReadOnlyCollection<FunctionDefinition> GetFunctions()
        => _functions.Values.ToArray();

    public bool TryGetFunction(string functionName, out FunctionDefinition? function)
    {
        if (!string.IsNullOrWhiteSpace(functionName) &&
            _functions.TryGetValue(functionName, out var found))
        {
            function = found;
            return true;
        }

        function = null;
        return false;
    }

    public MetaProvider RegisterFunctions(params FunctionDefinition[] functions)
    {
        ArgumentNullException.ThrowIfNull(functions);

        foreach (var function in functions)
            _functions[function.Name] = function;
        return this;
    }
}
