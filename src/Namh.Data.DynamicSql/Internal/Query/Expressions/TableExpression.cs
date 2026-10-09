using Namh.Data.DynamicSql.Internal.Query.Models;
using System.Text;

namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

internal sealed record JoinExpression(
    TableExpression Table,
    string JoinType,
    IReadOnlyList<SqlExpression> Conditions);

internal sealed record OrderExpression(SqlExpression Expression, bool Descending);

class TableExpression(Entity entity, string alias) : SqlExpression(typeof(IDbQueryable))
{
    public IEnumerable<ProjectionItem> ProjectionItems = [];
    public List<SqlExpression> SqlWhere { get; } = [];
    public List<JoinExpression> Joins { get; } = [];
    public List<OrderExpression> Orders { get; } = [];
    public StringBuilder? SqlSelect { get; protected set; }
    public StringBuilder? SqlOrder { get; set; }
    public readonly string Alias = alias;
    public readonly Entity Entity = entity;
    public readonly List<FieldExpression> Fields = [];
    public readonly Dictionary<string, TableExpression> JoinedTables = [];
    protected int? TakeCount;
    protected int? SkipCount;

    internal override string DebugView => $"{GetType().Name}: {Entity.Name} ({Alias})";

    protected TableExpression(TableExpression source) : this(source.Entity, source.Alias)
    {
        ProjectionItems = source.ProjectionItems.ToList();
        SqlWhere.AddRange(source.SqlWhere);
        Joins.AddRange(source.Joins);
        Orders.AddRange(source.Orders);
        SqlSelect = source.SqlSelect;
        SqlOrder = source.SqlOrder;
        TakeCount = source.TakeCount;
        SkipCount = source.SkipCount;
        foreach (var pair in source.JoinedTables)
            JoinedTables[pair.Key] = pair.Value;
    }

    protected virtual StringBuilder TranslateSelect()
        => new(string.Join(',', ProjectionItems));

    internal int? Take => TakeCount;
    internal int? Skip => SkipCount;
    internal bool IsTake => this is TakeExpression && SkipCount is null;

    internal void EnsureDefaultProjection()
    {
        if (ProjectionItems.Any() || SqlSelect is not null)
            return;
        ProjectionItems = Entity.Fields
            .Where(e => e.FieldType != FieldType.Aggregate)
            .Select(e => new ProjectionItem(new FieldExpression(Alias, e), e.Name))
            .ToList();
    }
}
