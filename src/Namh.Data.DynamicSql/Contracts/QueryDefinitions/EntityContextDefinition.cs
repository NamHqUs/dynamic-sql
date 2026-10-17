#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public sealed record EntityContextDefinition(
    string Alias,
    string RelationPath,
    List<RelationItem> RelatedKeys);
