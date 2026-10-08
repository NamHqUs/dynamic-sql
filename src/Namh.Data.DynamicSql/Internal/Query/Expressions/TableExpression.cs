using System.Text;
using Namh.Data.DynamicSql.Internal.Query.Models;
using Namh.Data.DynamicSql.Model;

namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

class TableExpression(Entity entity, string alias, ISqlDialect sqlDialect) : SqlExpression(typeof(IDbQueryable))
{
    public IEnumerable<ProjectionItem> ProjectionItems = [];

    protected StringBuilder? SqlSelect;
    public StringBuilder SqlFrom = new($"{sqlDialect.RenderTable(entity)} {alias}");
    public StringBuilder? SqlWhere;
    public StringBuilder? SqlOrder;

    public readonly string Alias = alias;
    public readonly Entity Entity = entity;
    public readonly ISqlDialect SqlDialect = sqlDialect;
    public readonly List<FieldExpression> Fields = [];
    public readonly Dictionary<string, TableExpression> JoinedTables = [];

    protected TableExpression(TableExpression source) : this(source.Entity, source.Alias, source.SqlDialect)
    {
        SqlSelect = source.SqlSelect == null ? null : new StringBuilder(source.SqlSelect.ToString());
        SqlFrom = new StringBuilder(source.SqlFrom.ToString());
        SqlWhere = source.SqlWhere == null ? null : new StringBuilder(source.SqlWhere.ToString());
        SqlOrder = source.SqlOrder == null ? null : new StringBuilder(source.SqlOrder.ToString());
        TakeCount = source.TakeCount;
        SkipCount = source.SkipCount;
    }

    protected override string ToSql()
    {
        var sqlSelect = TranslateSelect();

        return $"SELECT {sqlSelect}" +
            $"\nFROM {SqlFrom}" +
            (SqlWhere == null ? "" : $"\nWHERE {SqlWhere}") +
            (SqlOrder == null ? "" : $"\nORDER BY {SqlOrder}") +
            SqlDialect.RenderPaging(SkipCount, TakeCount);
    }

    protected int? TakeCount;
    protected int? SkipCount;

    protected virtual StringBuilder TranslateSelect()
    {
        if (!ProjectionItems.Any())
            ProjectionItems = Entity.Fields
                .Where(e => e.FieldType != FieldType.Aggregate)
                .Select(e => new ProjectionItem(Alias, e, SqlDialect))
                .ToList();

        return new StringBuilder(string.Join(',', ProjectionItems));
    }
}
