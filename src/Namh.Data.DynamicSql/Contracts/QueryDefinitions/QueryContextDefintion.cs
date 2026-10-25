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
    IConditionDefintion? Condition = null,
    QueryJoinType JoinType = QueryJoinType.Left);
