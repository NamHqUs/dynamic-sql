using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

[JsonConverter(typeof(FilterDtoJsonConverter))]
public interface IQueryFilter
{
}
public record QueryFilterItem(
    LogicalOperator LogicalOperator,
    string Field,
    BinaryOperator Operation,
    object? Value = null) : IQueryFilter
{
    public bool Parameterized { get; init; }
}

[JsonConverter(typeof(GroupFilterDtoJsonConverter))]
public class QueryFilter(LogicalOperator logicalOperator = LogicalOperator.And, params IQueryFilter[] filter) : IQueryFilter
{
    public LogicalOperator LogicalOperation { get; set; } = logicalOperator;
    public List<IQueryFilter> Filters { get; set; } = filter.ToList();
}

public static class ExampleQueryFilter
{
    public static QueryFilter Build()
    {
        // Example: Id > 100 AND Name = 'John Doe' AND (Age < 30 OR Age > 60) OR (Id > 100 AND (Name = 'John Doe' AND Age < 30) OR Age > 60)
        return new QueryFilter(LogicalOperator.And,
            new QueryFilterItem(LogicalOperator.And, "Id", BinaryOperator.GreaterThan, 100),
            new QueryFilterItem(LogicalOperator.And, "Name", BinaryOperator.Equals, "John Doe"),
            new QueryFilter(LogicalOperator.And,
                new QueryFilterItem(LogicalOperator.And, "Age", BinaryOperator.LessThan, 30),
                new QueryFilterItem(LogicalOperator.Or, "Age", BinaryOperator.GreaterThan, 60)
                ),
            new QueryFilter(LogicalOperator.And,
                new QueryFilterItem(LogicalOperator.And, "Id", BinaryOperator.GreaterThan, 100),
                new QueryFilter(LogicalOperator.And,
                    new QueryFilterItem(LogicalOperator.And, "Name", BinaryOperator.Equals, "John Doe"),
                    new QueryFilterItem(LogicalOperator.And, "Age", BinaryOperator.LessThan, 30)
                    ),
                new QueryFilterItem(LogicalOperator.Or, "Age", BinaryOperator.GreaterThan, 60)
                )
             );
    }

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

internal sealed class FilterDtoJsonConverter : JsonConverter<IQueryFilter>
{
    public override IQueryFilter Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return root.TryGetProperty("filters", out _)
            ? JsonSerializer.Deserialize<QueryFilter>(root, options)
                ?? throw new JsonException("The filter group cannot be null.")
            : JsonSerializer.Deserialize<QueryFilterItem>(root, options)
                ?? throw new JsonException("The filter cannot be null.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        IQueryFilter value,
        JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, value.GetType(), options);
}

internal sealed class GroupFilterDtoJsonConverter : JsonConverter<QueryFilter>
{
    public override QueryFilter Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var logicalOperation = root.TryGetProperty("logicalOperation", out var operation)
            ? operation.Deserialize<LogicalOperator>(options)
            : LogicalOperator.And;
        var filters = root.TryGetProperty("filters", out var filterElement)
            ? filterElement.Deserialize<List<IQueryFilter>>(options) ?? []
            : [];

        return new QueryFilter(logicalOperation, filters.ToArray());
    }

    public override void Write(
        Utf8JsonWriter writer,
        QueryFilter value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("logicalOperation", (int)value.LogicalOperation);
        writer.WritePropertyName("filters");
        JsonSerializer.Serialize(writer, value.Filters, options);
        writer.WriteEndObject();
    }
}
