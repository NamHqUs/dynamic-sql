#pragma warning disable IDE0130
using System.Text.Json.Serialization;

namespace Namh.Data.DynamicSql;

[JsonConverter(typeof(FieldDtoJsonConverter))]
public interface IFieldDto
{
    string ToText();
}

public record FieldDto(string FieldName) : IFieldDto
{
    public string ToText() => FieldName;
}

public record ConstFieldDto(object? Value) : IFieldDto
{
    public string ToText() => $"<{Value}>";
}
public record BinaryFieldDto(string Oper, IFieldDto Field) : IFieldDto
{
    public string ToText() => $" {Oper} {Field.ToText()}";
}
public record FunctionFieldDto(string FunctionName, IFieldDto[] Arguments) : IFieldDto
{
    public string ToText() => $"{FunctionName}[ {string.Join(", ", Arguments.Select(a => a.ToText()))} ]";
}
public record GroupFieldDto(IFieldDto[] Fields) : IFieldDto
{
    public string ToText() => $"( {string.Join("", Fields.Select(f => f.ToText()))} )";
}
