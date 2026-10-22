#pragma warning disable IDE0130
using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
using System.Text.Json.Serialization;

namespace Namh.Data.DynamicSql;

[JsonConverter(typeof(FieldDefinitionJsonConverter))]
public interface IFieldDefinition
{
    string ToText(Dictionary<string, object>? tags = null);
}

public interface IMathFieldDefinition : IFieldDefinition { }

public record FieldDefinition(string FieldName) : IMathFieldDefinition
{
    public string ToText(Dictionary<string, object>? tags = null) => FieldName;
}

public record ConstFieldDefinition(object? Value) : IMathFieldDefinition
{
    public string ToText(Dictionary<string, object>? tags = null) => $"<{Value}>";
}
public record MathFieldDefinition(MatchOperation Oper, IMathFieldDefinition Field) : IMathFieldDefinition
{
    public string ToText(Dictionary<string, object>? tags = null) => $" {Oper.ToSymbol()} {Field.ToText(tags)}";
}
public record FunctionFieldDefinition(string FunctionName, IFieldDefinition[] Arguments) : IMathFieldDefinition
{
    public string ToText(Dictionary<string, object>? tags = null) => $"{FunctionName}({string.Join(", ", Arguments.Select(a => a.ToText(tags)))})";
}
public record GroupFieldDefintion(MatchOperation Oper, IFieldDefinition[] Fields) : IFieldDefinition
{
    public GroupFieldDefintion(IFieldDefinition[] fields)
        : this(MatchOperation.Add, fields)
    {
    }

    public string ToText(Dictionary<string, object>? tags = null)
    {
        tags ??= [];

        if (!tags.ContainsKey("group-level"))
            tags["group-level"] = 0;

        var level = (int)tags["group-level"];
        tags["group-level"] = level + 1;

        var text = string.Join("", Fields.Select((field, index) => field switch
        {
            MathFieldDefinition => field.ToText(tags),
            GroupFieldDefintion nested when index > 0
                => $" {nested.Oper.ToSymbol()} {nested.ToText(tags)}",
            _ when index > 0 => $" {Oper.ToSymbol()} {field.ToText(tags)}",
            _ => field.ToText(tags)
        }));

        tags["group-level"] = level;

        return level == 0 ? text : $"({text})";
    }
}

public enum MatchOperation
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulus,
    BitwiseAnd,
    BitwiseOr,
}

internal static class MatchOperationExtensions
{
    public static string ToSymbol(this MatchOperation operation)
        => operation switch
        {
            MatchOperation.Add => "+",
            MatchOperation.Subtract => "-",
            MatchOperation.Multiply => "*",
            MatchOperation.Divide => "/",
            MatchOperation.Modulus => "%",
            MatchOperation.BitwiseAnd => "&",
            MatchOperation.BitwiseOr => "|",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported match operation.")
        };

    public static MatchOperation Parse(string operation)
        => operation switch
        {
            "+" => MatchOperation.Add,
            "-" => MatchOperation.Subtract,
            "*" => MatchOperation.Multiply,
            "/" => MatchOperation.Divide,
            "%" => MatchOperation.Modulus,
            "&" => MatchOperation.BitwiseAnd,
            "|" => MatchOperation.BitwiseOr,
            _ => throw new FormatException($"Unsupported match operator [{operation}].")
        };

    public static MatchOperation ParseArithmetic(string operation)
    {
        var parsed = Parse(operation);
        return parsed.IsArithmetic()
            ? parsed
            : throw new FormatException($"Unsupported arithmetic operator [{operation}].");
    }

    public static bool IsArithmetic(this MatchOperation operation)
        => operation is MatchOperation.Add or MatchOperation.Subtract or MatchOperation.Multiply
            or MatchOperation.Divide or MatchOperation.Modulus;
}
