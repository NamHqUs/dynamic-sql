using System.Text.Json;
using Namh.Data.DynamicSql;

namespace Demo.WebApi.Dto;

public sealed class FunctionDefinitionDto
{
    public string Name { get; set; } = "";
    public List<FunctionArgumentDefinitionDto> Arguments { get; set; } = [];
    public string ResultType { get; set; } = "";
    public bool CanDelete { get; set; }
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
            var defaultValue = argument.DefaultValue is { } value
                ? ToValue(value, argument.DataType)
                : null;
            return new FunctionArgumentDefinition(argument.Name, argument.DataType, defaultValue);
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
                DataType = argument.DataType,
                DefaultValue = argument.DefaultValue is null
                    ? null
                    : JsonSerializer.SerializeToElement(argument.DefaultValue)
            }).ToList(),
            ResultType = ResultTypes.First(mapping => mapping.Value == definition.ResultType).Key,
            IsAggregate = definition.IsAggregate,
            Description = definition.Description,
            DefaultSqlTemplate = definition.DefaultSqlTemplate,
            SqlTemplates = new Dictionary<string, string>(definition.SqlTemplates, StringComparer.OrdinalIgnoreCase)
        };

    private static object ToValue(JsonElement value, DataType dataType)
        => value.ValueKind == JsonValueKind.String && value.GetString() == "*"
            ? "*"
            : dataType switch
        {
            DataType.String when value.ValueKind == JsonValueKind.String => value.GetString()!,
            DataType.Number when value.ValueKind == JsonValueKind.Number => value.GetDecimal(),
            DataType.Datetime when value.ValueKind == JsonValueKind.String &&
                value.TryGetDateTime(out var dateTime) => dateTime,
            DataType.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False =>
                value.GetBoolean(),
            _ => throw new ArgumentException($"The default value must match argument data type [{dataType}].")
        };

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
    public DataType DataType { get; set; }
    public JsonElement? DefaultValue { get; set; }
}
