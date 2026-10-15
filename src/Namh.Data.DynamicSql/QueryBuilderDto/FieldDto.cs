#pragma warning disable IDE0130
using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;
using System.Net.Security;
using System.Text.Json.Serialization;
using System.Xml.Schema;

namespace Namh.Data.DynamicSql;

[JsonConverter(typeof(FieldDtoJsonConverter))]
public interface IFieldDto
{
    string ToText(Dictionary<string, object>? tags = null);
}

public record FieldDto(string FieldName) : IFieldDto
{
    public string ToText(Dictionary<string, object>? tags = null) => FieldName;
}

public record ConstFieldDto(object? Value) : IFieldDto
{
    public string ToText(Dictionary<string, object>? tags = null) => $"<{Value}>";
}
public record BinaryFieldDto(string Oper, IFieldDto Field) : IFieldDto
{
    public string ToText(Dictionary<string, object>? tags = null) => $" {Oper} {Field.ToText(tags)}";
}
public record FunctionFieldDto(string FunctionName, IFieldDto[] Arguments) : IFieldDto
{
    public string ToText(Dictionary<string, object>? tags = null) => $"{FunctionName}({string.Join(", ", Arguments.Select(a => a.ToText(tags)))})";
}
public record GroupFieldDto(IFieldDto[] Fields) : IFieldDto
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
