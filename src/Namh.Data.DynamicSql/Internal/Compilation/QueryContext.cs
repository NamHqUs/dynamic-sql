using Namh.Data.DynamicSql.Internal.Compilation.Expressions;
using Namh.Data.DynamicSql.Internal.Compilation.Models;

namespace Namh.Data.DynamicSql.Internal.Compilation;

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

        if (BuildEnforceFilter(table, out var filter))
            table.SqlWhere.Add(filter);

        return table;
    }

    public SqlExpression ParseFieldPath(TableExpression tableContext, string fieldPath)
    {
        if (tableContext.ProjectionItems
            .FirstOrDefault(e => string.Equals(e.Alias, fieldPath, StringComparison.OrdinalIgnoreCase))
            ?.Item is SqlExpression projection)
            return projection;

        if (TryParseAggregate(fieldPath, out var wrappedAggregate, out var wrappedAggregateFieldPath))
        {
            var wrappedFieldSeparator = wrappedAggregateFieldPath.LastIndexOf('.');
            var wrappedRelationPath = wrappedFieldSeparator < 0 ? null : wrappedAggregateFieldPath[..wrappedFieldSeparator];
            var wrappedFieldName = wrappedAggregateFieldPath[(wrappedFieldSeparator + 1)..];
            var wrappedJoinedData = ParseRelationPath(tableContext, wrappedRelationPath);
            var wrappedAggregateEntity = wrappedJoinedData.LastOrDefault().joinedEntity ?? tableContext.Entity;
            wrappedAggregateEntity.GetField(wrappedFieldName);

            var wrappedLastTable = ApplyJoin(tableContext, wrappedJoinedData);
            return new AggregateExpression(wrappedLastTable, $"{wrappedAggregate}(@{wrappedFieldName})", true);
        }

        var i = fieldPath.LastIndexOf('.');
        var relationPath = i < 0 ? null : fieldPath[..i];
        var fieldName = fieldPath[(i + 1)..];

        var joinedData = ParseRelationPath(tableContext, relationPath);

        if (string.Equals(fieldName, "Count()", StringComparison.OrdinalIgnoreCase))
        {
            var lastTable = ApplyJoinAggregate(tableContext, joinedData);
            return new AggregateExpression(lastTable, "Count(*)", !joinedData.Any());
        }

        if (TryParseAggregate(fieldName, out var aggregate, out var aggregateField))
        {
            var aggregateEntity = joinedData.LastOrDefault().joinedEntity ?? tableContext.Entity;
            aggregateEntity.GetField(aggregateField);

            var lastTable = ApplyJoinAggregate(tableContext, joinedData);
            return new AggregateExpression(lastTable, $"{aggregate}(@{aggregateField})", !joinedData.Any());
        }

        var lastEntity = joinedData.LastOrDefault().joinedEntity ?? tableContext.Entity;
        var metaField = lastEntity.GetField(fieldName);

        if (metaField.FieldType == FieldType.Aggregate)
        {
            var lastTable = ApplyJoinAggregate(tableContext, joinedData);
            return new AggregateExpression(lastTable, metaField.RawSql, !joinedData.Any());
        }
        else
        {
            var lastAlias = ApplyJoin(tableContext, joinedData).Alias;
            return new FieldExpression(lastAlias, metaField);
        }
    }

    public SqlExpression ParseQueryExpression(TableExpression tableContext, FieldExpressionTransaltion expression, int depth = 0)
    {
        if (depth > 32)
            throw new FormatException("Query expressions cannot be nested more than 32 levels.");
        if (expression is null || string.IsNullOrWhiteSpace(expression.Kind))
            throw new FormatException("A query expression requires a kind.");

        return expression.Kind.ToLowerInvariant() switch
        {
            "field" => ParseFieldExpression(tableContext, expression),
            "value" => ParseValueExpression(expression),
            "binary" => ParseBinaryExpression(tableContext, expression, depth),
            "function" => ParseFunctionExpression(tableContext, expression, depth),
            _ => throw new FormatException($"Invalid query expression kind [{expression.Kind}].")
        };
    }

    private SqlExpression ParseFieldExpression(TableExpression tableContext, FieldExpressionTransaltion expression)
        => string.IsNullOrWhiteSpace(expression.Field)
            ? throw new FormatException("A field query expression requires a field path.")
            : ParseFieldPath(tableContext, expression.Field);

    private SqlExpression ParseValueExpression(FieldExpressionTransaltion expression)
        => expression.Value is null
            ? throw new FormatException("A value query expression requires a numeric value.")
            : new ValueExpression(AddDbParameter(expression.Value.Value, true), typeof(decimal));

    private SqlExpression ParseBinaryExpression(TableExpression tableContext, FieldExpressionTransaltion expression, int depth)
    {
        if (expression.Arguments is null ||
            expression.Arguments.Count != 2 ||
            expression.Operator is not ("+" or "-" or "*" or "/"))
            throw new FormatException("A binary query expression requires two arguments and a supported arithmetic operator.");

        var left = ParseQueryExpression(tableContext, expression.Arguments[0], depth + 1);
        var right = ParseQueryExpression(tableContext, expression.Arguments[1], depth + 1);
        return new BinarySqlExpression(left, expression.Operator, right, typeof(decimal));
    }

    private SqlExpression ParseFunctionExpression(TableExpression tableContext, FieldExpressionTransaltion expression, int depth)
    {
        var name = expression.Function?.ToUpperInvariant();
        if (expression.Arguments is null)
            throw new FormatException("A function query expression requires arguments.");
        var valid = name switch
        {
            "SUM" => expression.Arguments.Count == 1,
            "ROUND" or "NULLIF" => expression.Arguments.Count == 2,
            _ => false
        };

        if (!valid)
            throw new FormatException($"Unsupported function or argument count [{expression.Function}].");

        var arguments = expression.Arguments
            .Select(argument => ParseQueryExpression(tableContext, argument, depth + 1))
            .ToArray();
        return new FunctionSqlExpression(name!, arguments, typeof(decimal));
    }

    private static bool TryParseAggregate(
        string fieldName,
        out string aggregate,
        out string aggregateField)
    {
        aggregate = string.Empty;
        aggregateField = string.Empty;

        var openParenthesis = fieldName.IndexOf('(');
        if (openParenthesis <= 0 || !fieldName.EndsWith(')'))
            return false;

        var function = fieldName[..openParenthesis].Trim();
        if (!string.Equals(function, "Sum", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(function, "Max", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(function, "Min", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(function, "Average", StringComparison.OrdinalIgnoreCase))
            return false;

        aggregateField = fieldName[(openParenthesis + 1)..^1].Trim();
        if (aggregateField.Length == 0 || aggregateField.Contains('(') || aggregateField.Contains(')'))
            return false;

        aggregate = string.Equals(function, "Average", StringComparison.OrdinalIgnoreCase)
            ? "AVG"
            : function;
        return true;
    }

    public TableExpression JoinCollection(TableExpression tableContext, string? relationPath)
    {
        var joinedData = ParseRelationPath(tableContext, relationPath);
        var lastTable = ApplyJoin(tableContext, joinedData.Take(joinedData.Count() - 1));

        var (relation, entity) = joinedData.Last();
        var joinedTable = new TableExpression(entity, GetNextAlias());
        joinedTable.SqlWhere.AddRange(BuildJoinKeys(lastTable, joinedTable, relation));

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
                tableContext.Joins.Add(BuildJoinPath(lastTable, joinedTable, relation));
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
            List<SqlExpression>? predicates = null;
            foreach (var (relation, joinedEntity) in joinedData)
            {
                var joinedTable = new TableExpression(joinedEntity, GetNextAlias());
                if (predicates == null)
                    predicates = BuildJoinKeys(tableContext, joinedTable, relation);
                else
                {
                    joinedTable.Joins.Add(BuildJoinPath(lastTable, joinedTable, relation, true));
                    joinedTable.Joins.AddRange(lastTable.Joins);
                }
                lastTable = joinedTable;
            }

            if (predicates is not null)
                lastTable.SqlWhere.AddRange(predicates);
        }

        return lastTable;
    }

    private JoinExpression BuildJoinPath(TableExpression table, TableExpression joinedTable, Relation relation, bool reverse = false)
    {
        var joinKeys = BuildJoinKeys(table, joinedTable, relation);
        //var joinOper = relation.Type == RelationType.Collection ? "INNER JOIN" : "LEFT JOIN";
        var joinOper = "LEFT JOIN";

        joinedTable = reverse ? table : joinedTable;
        return new JoinExpression(reverse ? table : joinedTable, joinOper, joinKeys);
    }

    private List<SqlExpression> BuildJoinKeys(TableExpression table, TableExpression joinedTable, Relation relation)
    {
        var predicates = new List<SqlExpression>();
        foreach (var e in relation.RelatedKeys)
            predicates.Add(new BinarySqlExpression(
                new FieldExpression(joinedTable.Alias, new Field(e.ToDbColumn)),
                "=",
                new FieldExpression(table.Alias, new Field(e.FromDbColumn)),
                typeof(bool)));

        if (BuildEnforceFilter(joinedTable, out var filter))
            predicates.Add(filter);

        return predicates;
    }

    private bool BuildEnforceFilter(TableExpression table, out SqlExpression filter)
    {
        var enforced = !includeArchive
                && table.Entity.Fields.Any(e => string.Equals(e.Name, ARCHIVE_COL, StringComparison.OrdinalIgnoreCase))
            ;
        filter = enforced
            ? new BinarySqlExpression(
                new FieldExpression(table.Alias, table.Entity.GetField(ARCHIVE_COL)),
                "IS",
                new ValueExpression(null, typeof(object)),
                typeof(bool))
            : null!;

        return enforced;
    }

    public string AddDbParameter(object? value, bool forceParameter = false)
    {
        if (!forceParameter &&
            (value is byte || value is short || value is int || value is long ||
             value is float || value is decimal || value is double))
            return value.ToString()!;

        if (value is bool b)
        {
            var boolName = string.Format("@p{0}", _dbParameters.Count);
            _dbParameters.Add(new DbParameter(boolName, b));
            return boolName;
        }

        if (!forceParameter && value is string st && !st.Contains('\''))
            return $"'{st}'";

        var name = string.Format("@p{0}", _dbParameters.Count);
        _dbParameters.Add(new DbParameter(name, value));

        return name;
    }
    public string GetNextAlias() => "t" + _aliasCount++;
}
