#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;

public enum QueryJoinType
{
    Inner,
    Left,
    Right,
    Full
}

public record QueryContextDefintion(
    string Name,
    QueryDefinition Query,
    IConditionDefintion? Condition,
    QueryJoinType JoinType = QueryJoinType.Left)
{
    public string? Alias { get; init; }
}
