using Namh.Data.DynamicSql.Internal.Query.Expressions;
using Namh.Data.DynamicSql.Internal.Query.Models;
using Namh.Data.DynamicSql.Model;
using System.Text;

namespace Namh.Data.DynamicSql.Internal.Query;

class QueryContext(IMetaProvider metaProvider, bool includeArchive)
{
    private const string ARCHIVE_COL = "DeletedOn";
    private int _aliasCount;
    public readonly Stack<(string ArgumentName, TableExpression TableContext)> Arguments = new();
    private readonly List<DbParameter> _dbParameters = [];

    public IEnumerable<DbParameter> DbParameters => _dbParameters;

    public TableExpression CreateTableContext(Entity entity)
    {
        var table = new TableExpression(entity, GetNextAlias());

        if (BuildEnforceFilter(table, out string filter))
            table.SqlWhere = new StringBuilder(filter);

        return table;
    }

    public SqlExpression ParseFieldPath(TableExpression tableContext, string fieldPath)
    {
        if (tableContext.ProjectionItems
            .FirstOrDefault(e => string.Equals(e.Alias, fieldPath, StringComparison.OrdinalIgnoreCase))
            ?.Item is SqlExpression projection)
            return projection;

        var i = fieldPath.LastIndexOf('.');
        var relationPath = i < 0 ? null : fieldPath[..i];
        var fieldName = fieldPath[(i + 1)..];

        var joinedData = ParseRelationPath(tableContext, relationPath);

        var lastEntity = joinedData.LastOrDefault().joinedEntity ?? tableContext.Entity;
        var metaField = lastEntity.GetField(fieldName);

        if (metaField.FieldType == FieldType.Aggregate)
        {
            var lastTable = ApplyJoinAggregate(tableContext, joinedData);
            return new AggregateExpression(lastTable, metaField.RawSql);
        }
        else
        {
            var lastAlias = ApplyJoin(tableContext, joinedData).Alias;
            return new FieldExpression(lastAlias, metaField);
        }
    }

    public TableExpression JoinCollection(TableExpression tableContext, string? relationPath)
    {
        var joinedData = ParseRelationPath(tableContext, relationPath);
        var lastTable = ApplyJoin(tableContext, joinedData.Take(joinedData.Count() - 1));

        var (relation, entity) = joinedData.Last();
        var joinedTable = new TableExpression(entity, GetNextAlias());
        joinedTable.SqlWhere = new StringBuilder(BuildJoinKeys(lastTable, joinedTable, relation));

        return joinedTable;
    }

    private IEnumerable<(Relation relation, Entity joinedEntity)> ParseRelationPath(TableExpression table, string? relationPath)
    {
        var next = table.Entity;

        return string.IsNullOrEmpty(relationPath)
            ? Enumerable.Empty<(Relation relation, Entity joinedEntity)>()
            : relationPath.Split('.')
                .Select(e =>
                {
                    var rel = next.GetRelation(e);
                    next = metaProvider.Get(rel.RelatedEntity);
                    return (rel, next);
                })
                .ToList();
    }

    private TableExpression ApplyJoin(TableExpression tableContext, IEnumerable<(Relation relation, Entity joinedEntity)> joinedData)
    {
        var lastTable = tableContext;
        foreach (var (relation, joinedEntity) in joinedData)
        {
            if (!lastTable.JoinedTables.TryGetValue(relation.Name, out TableExpression? joinedTable))
            {
                lastTable.JoinedTables[relation.Name] = joinedTable = new TableExpression(joinedEntity, GetNextAlias());
                tableContext.SqlFrom.Append(BuildJoinPath(lastTable, joinedTable, relation));
            }
            lastTable = joinedTable;
        }

        return lastTable;
    }

    private TableExpression ApplyJoinAggregate(TableExpression tableContext, IEnumerable<(Relation relation, Entity joinedEntity)> joinedData)
    {
        var lastTable = tableContext;
        if (joinedData.Any())
        {
            var sqlFrom = new StringBuilder();
            string sqlWhere = null!;
            foreach (var (relation, joinedEntity) in joinedData)
            {
                var joinedTable = new TableExpression(joinedEntity, GetNextAlias());
                if (sqlWhere == null)
                    sqlWhere = BuildJoinKeys(tableContext, joinedTable, relation);
                else
                    sqlFrom.Append(BuildJoinPath(lastTable, joinedTable, relation, true));
                lastTable = joinedTable;
            }

            lastTable.SqlFrom.Append(sqlFrom);
            lastTable.SqlWhere = new StringBuilder(sqlWhere);
        }

        return lastTable;
    }

    private string BuildJoinPath(TableExpression table, TableExpression joinedTable, Relation relation, bool reverse = false)
    {
        var joinKeys = BuildJoinKeys(table, joinedTable, relation);
        //var joinOper = relation.Type == RelationType.Collection ? "INNER JOIN" : "LEFT JOIN";
        var joinOper = "LEFT JOIN";

        joinedTable = reverse ? table : joinedTable;

        return $" {joinOper} {joinedTable.Entity} {joinedTable.Alias} ON {joinKeys}";
    }

    private string BuildJoinKeys(TableExpression table, TableExpression joinedTable, Relation relation)
    {
        var sb = new StringBuilder();
        foreach (var e in relation.RelatedKeys)
        {
            if (sb.Length > 0)
                sb.Append(" AND ");
            sb.AppendFormat("{0}.[{1}] = {2}.[{3}]", joinedTable.Alias, e.ToDbColumn, table.Alias, e.FromDbColumn);
        }

        if (BuildEnforceFilter(joinedTable, out string filter))
            sb.Append(" AND ").Append(filter);

        return sb.ToString();
    }

    private bool BuildEnforceFilter(TableExpression table, out string filter)
    {
        filter = !includeArchive
                && table.Entity.Fields.Any(e => string.Equals(e.Name, ARCHIVE_COL, StringComparison.OrdinalIgnoreCase))
            ? $"{table.Alias}.[{ARCHIVE_COL}] is null"
            : string.Empty;

        return filter != string.Empty;
    }

    public string AddDbParameter(object? value)
    {
        if (value is byte || value is short || value is int || value is long || value is float || value is decimal || value is double)
            return value.ToString()!;

        if (value is bool b)
            return b ? "1" : "0";

        if (value is string st && !st.Contains('\''))
            return $"'{st}'";

        var name = string.Format("@p{0}", _dbParameters.Count);
        _dbParameters.Add(new DbParameter(name, value));

        return name;
    }
    public string GetNextAlias() => "t" + _aliasCount++;
}
