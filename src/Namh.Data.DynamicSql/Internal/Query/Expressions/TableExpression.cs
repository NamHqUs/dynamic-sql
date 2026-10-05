using System.Text;
using Namh.Data.DynamicSql.Internal.Query.Models;
using Namh.Data.DynamicSql.Model;

namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

class TableExpression(Entity entity, string alias) : SqlExpression(typeof(IDbQueryable))
{
    public IEnumerable<ProjectionItem> ProjectionItems = [];

    protected StringBuilder? SqlSelect;
    public StringBuilder SqlFrom = new($"{entity} {alias}");
    public StringBuilder? SqlWhere;
    public StringBuilder? SqlOrder;

    public readonly string Alias = alias;
    public readonly Entity Entity = entity;
    public readonly List<FieldExpression> Fields = [];
    public readonly Dictionary<string, TableExpression> JoinedTables = [];

    protected TableExpression(TableExpression source) : this(source.Entity, source.Alias)
    {
        SqlSelect = source.SqlSelect == null ? null : new StringBuilder(source.SqlSelect.ToString());
        SqlFrom = new StringBuilder(source.SqlFrom.ToString());
        SqlWhere = source.SqlWhere == null ? null : new StringBuilder(source.SqlWhere.ToString());
        SqlOrder = source.SqlOrder == null ? null : new StringBuilder(source.SqlOrder.ToString());
    }

    protected override string ToSql()
    {
        var sqlSelect = TranslateSelect();

        return $"SELECT {sqlSelect}" +
            $"\nFROM {SqlFrom}" +
            (SqlWhere == null ? "" : $"\nWHERE {SqlWhere}") +
            (SqlOrder == null ? "" : $"\nORDER BY {SqlOrder}");
    }

    protected virtual StringBuilder TranslateSelect()
    {
        if (!ProjectionItems.Any())
            ProjectionItems = Entity.Fields
                .Where(e => e.FieldType != FieldType.Aggregate)
                .Select(e => new ProjectionItem(Alias, e))
                .ToList();

        return new StringBuilder(string.Join(',', ProjectionItems));
    }
}
