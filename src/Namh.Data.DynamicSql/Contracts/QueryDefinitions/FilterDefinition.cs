using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

[JsonConverter(typeof(FilterDefinitionJsonConverter))]
public interface IFilterDefintion
{
    string ToText();
}

[JsonConverter(typeof(GroupFilterDtoJsonConverter))]
public class FilterDefinition(LogicalOperator logicalOperator = LogicalOperator.And, params IFilterDefintion[] filter) : IFilterDefintion
{
    public LogicalOperator LogicalOperation { get; set; } = logicalOperator;
    public string Note { get; set; } = string.Empty;
    public List<IFilterDefintion> Filters { get; set; } = filter.ToList();

    public string ToText()
    {
        var text = new System.Text.StringBuilder();
        for (var index = 0; index < Filters.Count; index++)
        {
            var filter = Filters[index];
            if (index > 0)
            {
                var logicalOperation = filter is FilterItemDefinition { LogicalOperator: not null } item
                    ? item.LogicalOperator.Value
                    : LogicalOperation;
                text.Append(' ')
                    .Append(logicalOperation.ToString().ToUpperInvariant())
                    .Append(' ');
            }

            if (filter is FilterDefinition)
                text.Append('(').Append(filter.ToText()).Append(')');
            else
                text.Append(filter.ToText());
        }

        return text.ToString();
    }
}

[JsonConverter(typeof(FilterItemDefinitionJsonConverter))]
public record FilterItemDefinition(
    LogicalOperator? LogicalOperator,
    IFieldDefinition Left,
    BinaryOperator Operation,
    IFieldDefinition? Right = null) : IFilterDefintion
{
    public bool Parameterized { get; init; }
    public string? OperatorId { get; init; }
    public string? CustomSqlTemplate { get; init; }

    public string ToText()
        => $"{Left.ToText()} {OperatorId ?? FormatOperator(Operation)} {Right?.ToText() ?? "<NULL>"}";

    private static string FormatOperator(BinaryOperator operation)
        => operation switch
        {
            BinaryOperator.Equals => "=",
            BinaryOperator.NotEquals => "<>",
            BinaryOperator.Like => "LIKE",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.LessThan => "<",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.LessThanOrEqual => "<=",
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
}

public enum BinaryOperator
{
    Equals,
    NotEquals,
    Like,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual
}

public enum LogicalOperator
{
    And,
    Or
}
