using System.Text.Json;
using Namh.Data.DynamicSql;

namespace Demo.WebApi.Dto;

public sealed class FunctionDefinitionDto
{
    public string Name { get; set; } = "";
    public List<FunctionArgumentDefinitionDto> Arguments { get; set; } = [];
    public string ResultType { get; set; } = "";
    public bool IsAggregate { get; set; }
    public string Description { get; set; } = "";
    public string? DefaultSqlTemplate { get; set; }
    public Dictionary<string, string> SqlTemplates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public FunctionDefinition ToFunctionDefinition()
    {
        if (string.IsNullOrWhiteSpace(ResultType))
            throw new ArgumentException("A result type is required.", nameof(ResultType));
        if (!ResultTypes.TryGetValue(ResultType, out var resultType))
            throw new ArgumentException($"Unsupported result type [{ResultType}].", nameof(ResultType));

        var arguments = Arguments.Select(argument =>
        {
            if (argument.AllowedTokens is null || argument.AllowedTypes is null || argument.AllowedConstants is null)
                throw new ArgumentException($"Allowed values for argument [{argument.Name}] cannot be null.");
            if (argument.DefaultKind is null && argument.DefaultValue is not null)
                throw new ArgumentException($"Argument [{argument.Name}] has a default value without a default kind.");

            var allowedConstants = argument.AllowedConstants.Select(ToValue).ToArray();
            var defaultValue = argument.DefaultKind switch
            {
                null => null,
                FunctionArgumentDefaultKind.Constant when argument.DefaultValue is { } value =>
                    FunctionArgumentDefault.Constant(ToValue(value)),
                FunctionArgumentDefaultKind.SqlToken when argument.DefaultValue is { } value =>
                    FunctionArgumentDefault.SqlToken(ToStringValue(value, "SQL token")),
                FunctionArgumentDefaultKind.LogicalType when argument.DefaultValue is { } value =>
                    FunctionArgumentDefault.LogicalType(ToStringValue(value, "logical type")),
                _ => throw new ArgumentException($"A value is required for the default of argument [{argument.Name}].")
            };

            return argument.Kind switch
            {
                FunctionArgumentKind.Constant => FunctionArgumentDefinition.Constant(
                    argument.Name, allowedConstants, argument.Optional, defaultValue),
                FunctionArgumentKind.QueryExpression => FunctionArgumentDefinition.QueryExpression(
                    argument.Name, argument.Optional, defaultValue),
                FunctionArgumentKind.QueryExpressionOrSqlToken => FunctionArgumentDefinition.QueryExpressionOrSqlToken(
                    argument.Name, argument.AllowedTokens, argument.Optional, defaultValue),
                FunctionArgumentKind.SqlType => FunctionArgumentDefinition.SqlType(
                    argument.Name, argument.AllowedTypes, argument.Optional, defaultValue),
                _ => throw new ArgumentException($"Unsupported argument kind [{argument.Kind}].")
            };
        }).ToArray();

        return new FunctionDefinition(
            Name,
            arguments,
            resultType,
            Description,
            DefaultSqlTemplate,
            SqlTemplates,
            IsAggregate);
    }

    public static FunctionDefinitionDto FromFunctionDefinition(FunctionDefinition definition)
        => new()
        {
            Name = definition.Name,
            Arguments = definition.Arguments.Select(argument => new FunctionArgumentDefinitionDto
            {
                Name = argument.Name,
                Kind = argument.Kind,
                AllowedTokens = argument.AllowedTokens.ToList(),
                AllowedTypes = argument.AllowedTypes.ToList(),
                AllowedConstants = argument.AllowedConstants.Select(value => JsonSerializer.SerializeToElement(value)).ToList(),
                Optional = argument.Optional,
                DefaultKind = argument.DefaultValue?.Kind,
                DefaultValue = argument.DefaultValue is null
                    ? null
                    : JsonSerializer.SerializeToElement(argument.DefaultValue!.Value)
            }).ToList(),
            ResultType = ResultTypes.First(mapping => mapping.Value == definition.ResultType).Key,
            IsAggregate = definition.IsAggregate,
            Description = definition.Description,
            DefaultSqlTemplate = definition.DefaultSqlTemplate,
            SqlTemplates = new Dictionary<string, string>(definition.SqlTemplates, StringComparer.OrdinalIgnoreCase)
        };

    private static object? ToValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => value.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => throw new ArgumentException("Function constant values must be JSON strings, numbers, booleans, or null.")
        };

    private static string ToStringValue(JsonElement value, string description)
        => value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new ArgumentException($"The default {description} must be a non-empty string.");

    private static readonly IReadOnlyDictionary<string, Type> ResultTypes =
        new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["string"] = typeof(string),
            ["int"] = typeof(int),
            ["long"] = typeof(long),
            ["decimal"] = typeof(decimal),
            ["double"] = typeof(double),
            ["bool"] = typeof(bool),
            ["DateTime"] = typeof(DateTime)
        };
}

public sealed class FunctionArgumentDefinitionDto
{
    public string Name { get; set; } = "";
    public FunctionArgumentKind Kind { get; set; }
    public List<string> AllowedTokens { get; set; } = [];
    public List<string> AllowedTypes { get; set; } = [];
    public List<JsonElement> AllowedConstants { get; set; } = [];
    public bool Optional { get; set; }
    public FunctionArgumentDefaultKind? DefaultKind { get; set; }
    public JsonElement? DefaultValue { get; set; }
}
