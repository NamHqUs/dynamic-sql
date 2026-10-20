#pragma warning disable IDE0130
using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
using System.Text.Json.Serialization;

namespace Namh.Data.DynamicSql;

[JsonConverter(typeof(FieldDefinitionJsonConverter))]
public interface IFieldDefinition
{
    string ToText(Dictionary<string, object>? tags = null);
}

public record FieldDefinition(string FieldName) : IFieldDefinition
{
    public string ToText(Dictionary<string, object>? tags = null) => FieldName;
}

public record ConstFieldDefinition(object? Value) : IFieldDefinition
{
    public string ToText(Dictionary<string, object>? tags = null) => $"<{Value}>";
}
public record BinaryFieldDefinition(string Oper, IFieldDefinition Field) : IFieldDefinition
{
    public string ToText(Dictionary<string, object>? tags = null) => $" {Oper} {Field.ToText(tags)}";
}
public record FunctionFieldDefinition(string FunctionName, IFieldDefinition[] Arguments) : IFieldDefinition
{
    public string ToText(Dictionary<string, object>? tags = null) => $"{FunctionName}({string.Join(", ", Arguments.Select(a => a.ToText(tags)))})";
}
public record GroupFieldDefintion(IFieldDefinition[] Fields) : IFieldDefinition
{
    public string ToText(Dictionary<string, object>? tags = null)
    {
        tags ??= [];

        if (!tags.ContainsKey("group-level"))
            tags["group-level"] = 0;

        var level = (int)tags["group-level"];
        tags["group-level"] = level + 1;

        var text = string.Join("", Fields.Select(f => f.ToText(tags)));

        tags["group-level"] = level;

        return level == 0 ? text : $"({text})";
    }
}
