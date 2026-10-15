namespace Namh.Data.DynamicSql;

public interface IMetaProvider
{
    Entity Get(string entityName);

    FunctionDefinition GetFunction(string functionName)
        => BuiltInFunctionDefinitions.GetFunction(functionName);

    IReadOnlyCollection<FunctionDefinition> GetFunctions()
        => BuiltInFunctionDefinitions.AllFunctions.ToArray();

    bool TryGetFunction(string functionName, out FunctionDefinition? function)
        => BuiltInFunctionDefinitions.TryGetFunction(functionName, out function);

    SqlTypeDefinition GetSqlType(string logicalTypeName)
        => BuiltInFunctionDefinitions.GetSqlType(logicalTypeName);

    IReadOnlyCollection<SqlTypeDefinition> GetSqlTypes()
        => BuiltInFunctionDefinitions.AllSqlTypes.ToArray();
}

public class MetaProvider : IMetaProvider
{
    private readonly Dictionary<string, Entity> _entities;
    private readonly Dictionary<string, FunctionDefinition> _functions;
    private readonly Dictionary<string, SqlTypeDefinition> _sqlTypes;

    public MetaProvider(
        IEnumerable<Entity> entities,
        IEnumerable<FunctionDefinition>? functions = null,
        IEnumerable<SqlTypeDefinition>? sqlTypes = null)
    {
        ArgumentNullException.ThrowIfNull(entities);

        _entities = entities.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        _functions = BuiltInFunctionDefinitions.AllFunctions
            .ToDictionary(function => function.Name, StringComparer.OrdinalIgnoreCase);
        _sqlTypes = BuiltInFunctionDefinitions.AllSqlTypes
            .ToDictionary(type => type.Name, StringComparer.OrdinalIgnoreCase);

        var customFunctions = functions?.ToArray() ?? [];
        var customTypes = sqlTypes?.ToArray() ?? [];
        if (customFunctions
            .GroupBy(function => function.Name, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
            throw new ArgumentException("Function names must be unique.", nameof(functions));
        if (customTypes
            .GroupBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
            throw new ArgumentException("Logical SQL type names must be unique.", nameof(sqlTypes));

        foreach (var sqlType in customTypes)
            _sqlTypes[sqlType.Name] = sqlType;

        foreach (var function in customFunctions)
        {
            foreach (var argument in function.Arguments.Where(argument => argument.Kind == FunctionArgumentKind.SqlType))
            foreach (var typeName in argument.AllowedTypes)
            {
                if (!_sqlTypes.ContainsKey(typeName))
                    throw new ArgumentException(
                        $"Function [{function.Name}] references undefined logical SQL type [{typeName}].",
                        nameof(functions));
            }

            _functions[function.Name] = function;
        }
    }

    public Entity Get(string entityName)
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

    public SqlTypeDefinition GetSqlType(string logicalTypeName)
        => !string.IsNullOrWhiteSpace(logicalTypeName) && _sqlTypes.TryGetValue(logicalTypeName, out var sqlType)
            ? sqlType
            : throw new FormatException($"Unsupported logical SQL type [{logicalTypeName}].");

    public IReadOnlyCollection<SqlTypeDefinition> GetSqlTypes()
        => _sqlTypes.Values.ToArray();
}
