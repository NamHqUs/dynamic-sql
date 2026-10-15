using System.Collections.ObjectModel;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public enum FunctionArgumentKind
{
    Constant,
    QueryExpression,
    QueryExpressionOrSqlToken,
    SqlType
}

public enum FunctionArgumentDefaultKind
{
    Constant,
    SqlToken,
    LogicalType
}

public sealed record FunctionArgumentDefault
{
    private FunctionArgumentDefault(FunctionArgumentDefaultKind kind, object? value)
    {
        Kind = kind;
        Value = value;
    }

    public FunctionArgumentDefaultKind Kind { get; }
    public object? Value { get; }

    public static FunctionArgumentDefault Constant(object? value)
        => new(FunctionArgumentDefaultKind.Constant, value);

    public static FunctionArgumentDefault SqlToken(string value)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A SQL token is required.", nameof(value))
            : new(FunctionArgumentDefaultKind.SqlToken, value);

    public static FunctionArgumentDefault LogicalType(string value)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A logical type is required.", nameof(value))
            : new(FunctionArgumentDefaultKind.LogicalType, value);
}

public sealed record FunctionArgumentDefinition
{
    private FunctionArgumentDefinition(
        string name,
        FunctionArgumentKind kind,
        IReadOnlyList<string> allowedTokens,
        IReadOnlyList<string> allowedTypes,
        IReadOnlyList<object?> allowedConstants,
        bool optional,
        FunctionArgumentDefault? defaultValue)
    {
        ArgumentNullException.ThrowIfNull(allowedTokens);
        ArgumentNullException.ThrowIfNull(allowedTypes);
        ArgumentNullException.ThrowIfNull(allowedConstants);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An argument name is required.", nameof(name));
        if (!optional && defaultValue is not null)
            throw new ArgumentException("A required argument cannot have a default.", nameof(defaultValue));
        if (defaultValue is { Kind: FunctionArgumentDefaultKind.Constant, Value: null })
            throw new ArgumentException("A constant default value cannot be null.", nameof(defaultValue));
        if (kind == FunctionArgumentKind.Constant && allowedConstants.Count == 0)
            throw new ArgumentException("A constant argument must declare at least one allowed value.", nameof(allowedConstants));
        if (allowedConstants.Any(value => value is null))
            throw new ArgumentException("Allowed constant values cannot contain null.", nameof(allowedConstants));
        if (kind == FunctionArgumentKind.QueryExpressionOrSqlToken && allowedTokens.Count == 0)
            throw new ArgumentException("A SQL-token argument must declare at least one allowed token.", nameof(allowedTokens));
        if (kind == FunctionArgumentKind.SqlType && allowedTypes.Count == 0)
            throw new ArgumentException("A SQL-type argument must declare at least one logical type.", nameof(allowedTypes));
        if (!IsValidDefault(kind, allowedTokens, allowedTypes, allowedConstants, defaultValue))
            throw new ArgumentException("The default is not allowed for this argument definition.", nameof(defaultValue));

        Name = name;
        Kind = kind;
        AllowedTokens = Array.AsReadOnly(allowedTokens.ToArray());
        AllowedTypes = Array.AsReadOnly(allowedTypes.ToArray());
        AllowedConstants = Array.AsReadOnly(allowedConstants.ToArray());
        Optional = optional;
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public FunctionArgumentKind Kind { get; }
    public IReadOnlyList<string> AllowedTokens { get; }
    public IReadOnlyList<string> AllowedTypes { get; }
    public IReadOnlyList<object?> AllowedConstants { get; }
    public bool Optional { get; }
    public FunctionArgumentDefault? DefaultValue { get; }

    public static FunctionArgumentDefinition Constant(
        string name,
        IReadOnlyList<object?> allowedValues,
        bool optional = false,
        FunctionArgumentDefault? defaultValue = null)
        => new(name, FunctionArgumentKind.Constant, [], [], allowedValues, optional, defaultValue);

    public static FunctionArgumentDefinition QueryExpression(
        string name,
        bool optional = false,
        FunctionArgumentDefault? defaultValue = null)
        => new(name, FunctionArgumentKind.QueryExpression, [], [], [], optional, defaultValue);

    public static FunctionArgumentDefinition QueryExpressionOrSqlToken(
        string name,
        IReadOnlyList<string> allowedTokens,
        bool optional = false,
        FunctionArgumentDefault? defaultValue = null)
        => new(name, FunctionArgumentKind.QueryExpressionOrSqlToken, allowedTokens, [], [], optional, defaultValue);

    public static FunctionArgumentDefinition SqlType(
        string name,
        IReadOnlyList<string> allowedTypes,
        bool optional = false,
        FunctionArgumentDefault? defaultValue = null)
        => new(name, FunctionArgumentKind.SqlType, [], allowedTypes, [], optional, defaultValue);

    private static bool IsValidDefault(
        FunctionArgumentKind kind,
        IReadOnlyList<string> allowedTokens,
        IReadOnlyList<string> allowedTypes,
        IReadOnlyList<object?> allowedConstants,
        FunctionArgumentDefault? defaultValue)
    {
        if (defaultValue is null)
            return true;

        return (kind, defaultValue.Kind, defaultValue.Value) switch
        {
            (FunctionArgumentKind.Constant, FunctionArgumentDefaultKind.Constant, var value) =>
                allowedConstants.Contains(value),
            (FunctionArgumentKind.QueryExpression, FunctionArgumentDefaultKind.Constant, _) => true,
            (FunctionArgumentKind.QueryExpressionOrSqlToken, FunctionArgumentDefaultKind.Constant, _) => true,
            (FunctionArgumentKind.QueryExpressionOrSqlToken, FunctionArgumentDefaultKind.SqlToken, string token) =>
                allowedTokens.Contains(token, StringComparer.OrdinalIgnoreCase),
            (FunctionArgumentKind.SqlType, FunctionArgumentDefaultKind.LogicalType, string type) =>
                allowedTypes.Contains(type, StringComparer.OrdinalIgnoreCase),
            _ => false
        };
    }
}

public sealed class FunctionDefinition
{
    private readonly IReadOnlyDictionary<string, string> _sqlTemplates;

    public FunctionDefinition(
        string name,
        IReadOnlyList<FunctionArgumentDefinition> arguments,
        Type resultType,
        string description,
        string? defaultSqlTemplate = null,
        IReadOnlyDictionary<string, string>? sqlTemplates = null,
        bool isAggregate = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A function name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(resultType);
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("A function description is required.", nameof(description));
        if (string.IsNullOrWhiteSpace(defaultSqlTemplate) && (sqlTemplates is null || sqlTemplates.Count == 0))
            throw new ArgumentException("A default or at least one dialect-specific SQL template is required.");
        if (defaultSqlTemplate is not null && string.IsNullOrWhiteSpace(defaultSqlTemplate))
            throw new ArgumentException("The default SQL template cannot be empty.", nameof(defaultSqlTemplate));
        if (arguments.Any(argument => argument is null))
            throw new ArgumentException("Function arguments cannot contain null definitions.", nameof(arguments));
        if (arguments.Select(argument => argument.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != arguments.Count)
            throw new ArgumentException("Function argument names must be unique.", nameof(arguments));

        var optionalArgumentSeen = false;
        foreach (var argument in arguments)
        {
            optionalArgumentSeen |= argument.Optional;
            if (!argument.Optional && optionalArgumentSeen)
                throw new ArgumentException("Required arguments cannot follow optional arguments.", nameof(arguments));
        }

        Name = name;
        Arguments = Array.AsReadOnly(arguments.ToArray());
        ResultType = resultType;
        IsAggregate = isAggregate;
        Description = description;
        if (defaultSqlTemplate is not null)
            ValidateTemplate(defaultSqlTemplate, arguments.Count, "default");
        DefaultSqlTemplate = defaultSqlTemplate;

        var templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (dialect, template) in sqlTemplates ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(dialect))
                throw new ArgumentException("Dialect template keys cannot be empty.", nameof(sqlTemplates));
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentException($"The SQL template for [{dialect}] cannot be empty.", nameof(sqlTemplates));
            ValidateTemplate(template, arguments.Count, dialect);
            templates.Add(dialect, template);
        }

        _sqlTemplates = new ReadOnlyDictionary<string, string>(templates);
    }

    public string Name { get; }
    public IReadOnlyList<FunctionArgumentDefinition> Arguments { get; }
    public Type ResultType { get; }
    public bool IsAggregate { get; }
    public string Description { get; }
    public string? DefaultSqlTemplate { get; }
    public IReadOnlyDictionary<string, string> SqlTemplates => _sqlTemplates;

    public string GetSqlTemplate(string dialect)
        => _sqlTemplates.TryGetValue(dialect, out var template)
            ? template
            : DefaultSqlTemplate
                ?? throw new FormatException($"Function [{Name}] has no SQL template for dialect [{dialect}].");

    private static void ValidateTemplate(string template, int argumentCount, string dialect)
    {
        for (var index = 0; index < template.Length; index++)
        {
            if (template[index] == '}')
                throw new ArgumentException($"The SQL template for [{dialect}] contains an unmatched closing brace.");
            if (template[index] != '{')
                continue;

            var close = template.IndexOf('}', index + 1);
            if (close < 0 ||
                !int.TryParse(template.AsSpan(index + 1, close - index - 1), out var argumentIndex) ||
                argumentIndex < 1 ||
                argumentIndex > argumentCount)
                throw new ArgumentException($"The SQL template for [{dialect}] contains an invalid argument placeholder.");

            index = close;
        }
    }
}

public sealed class SqlTypeDefinition
{
    private readonly IReadOnlyDictionary<string, string> _sqlTypes;

    public SqlTypeDefinition(
        string name,
        IReadOnlyDictionary<string, string>? sqlTypes = null,
        string? defaultSqlType = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A logical SQL type name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(defaultSqlType) && (sqlTypes is null || sqlTypes.Count == 0))
            throw new ArgumentException("A default or at least one dialect-specific SQL type mapping is required.");
        if (defaultSqlType is not null && string.IsNullOrWhiteSpace(defaultSqlType))
            throw new ArgumentException("The default SQL type cannot be empty.", nameof(defaultSqlType));
        if (sqlTypes?.Any(mapping =>
            string.IsNullOrWhiteSpace(mapping.Key) || string.IsNullOrWhiteSpace(mapping.Value)) == true)
            throw new ArgumentException("SQL type mappings cannot be empty.", nameof(sqlTypes));

        Name = name;
        DefaultSqlType = defaultSqlType;
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (dialect, sqlType) in sqlTypes ?? new Dictionary<string, string>())
            mappings.Add(dialect, sqlType);
        _sqlTypes = new ReadOnlyDictionary<string, string>(mappings);
    }

    public string Name { get; }
    public string? DefaultSqlType { get; }
    public IReadOnlyDictionary<string, string> SqlTypes => _sqlTypes;

    public string GetSqlType(string dialect)
        => _sqlTypes.TryGetValue(dialect, out var sqlType)
            ? sqlType
            : DefaultSqlType
                ?? throw new FormatException($"Logical SQL type [{Name}] has no mapping for dialect [{dialect}].");
}
