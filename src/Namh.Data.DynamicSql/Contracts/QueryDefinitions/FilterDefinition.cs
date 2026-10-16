using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

[JsonConverter(typeof(FilterDefinitionJsonConverter))]
public interface IFilterDto
{
    string ToText();
}

[JsonConverter(typeof(GroupFilterDtoJsonConverter))]
public class FilterDefinition(LogicalOperator logicalOperator = LogicalOperator.And, params IFilterDto[] filter) : IFilterDto
{
    public LogicalOperator LogicalOperation { get; set; } = logicalOperator;
    public string Note { get; set; } = string.Empty;
    public List<IFilterDto> Filters { get; set; } = filter.ToList();

    public string ToText()
    {
        var text = new System.Text.StringBuilder();
        for (var index = 0; index < Filters.Count; index++)
        {
            var filter = Filters[index];
            if (index > 0)
            {
                var logicalOperation = filter is FilterItemDto { LogicalOperator: not null } item
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

[JsonConverter(typeof(FilterItemDtoJsonConverter))]
public record FilterItemDto(
    LogicalOperator? LogicalOperator,
    IFieldDto Left,
    BinaryOperator Operation,
    IFieldDto? Right = null) : IFilterDto
{
    public bool Parameterized { get; init; }

    public string ToText()
        => $"{Left.ToText()} {FormatOperator(Operation)} {Right?.ToText() ?? "<NULL>"}";

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
