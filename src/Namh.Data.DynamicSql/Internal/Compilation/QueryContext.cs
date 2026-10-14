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

    public SqlExpression ParseQueryExpression(TableExpression tableContext, IFieldDto expression, int depth = 0)
    {
        if (depth > 32)
            throw new FormatException("Query expressions cannot be nested more than 32 levels.");
        if (expression is null)
            throw new FormatException("A query expression cannot be null.");

        return expression switch
        {
            FieldDto field => ParseFieldExpression(tableContext, field),
            ConstFieldDto value => ParseValueExpression(value),
            GroupFieldDto group => ParseGroupExpression(tableContext, group.Fields, depth),
            FunctionFieldDto function => ParseFunctionExpression(tableContext, function, depth),
            _ => throw new FormatException($"Unsupported query expression [{expression.GetType().Name}].")
        };
    }

    private SqlExpression ParseFieldExpression(TableExpression tableContext, FieldDto expression)
        => string.IsNullOrWhiteSpace(expression.FieldName)
            ? throw new FormatException("A field query expression requires a field path.")
            : ParseFieldPath(tableContext, expression.FieldName);

    private SqlExpression ParseValueExpression(ConstFieldDto expression)
        => expression.Value is null
            ? throw new FormatException("A value query expression requires a numeric value.")
            : new ValueExpression(AddDbParameter(ToDecimal(expression.Value), true), typeof(decimal));

    private SqlExpression ParseGroupExpression(TableExpression tableContext, IFieldDto[] fields, int depth)
    {
        if (fields.Length == 0)
            throw new FormatException("A grouped query expression cannot be empty.");
        if (fields.Length == 1)
            return ParseQueryExpression(tableContext, fields[0], depth);

        var result = ParseQueryExpression(tableContext, fields[0], depth + 1);
        for (var index = 1; index < fields.Length; index++)
        {
            if (fields[index] is not BinaryFieldDto binary ||
                binary.Oper is not ("+" or "-" or "*" or "/"))
                throw new FormatException("Grouped projections must alternate expressions and supported binary operators.");

            result = new BinarySqlExpression(
                result,
                binary.Oper,
                ParseQueryExpression(tableContext, binary.Field, depth + 1),
                typeof(decimal));
        }

        return result;
    }

    private SqlExpression ParseFunctionExpression(TableExpression tableContext, FunctionFieldDto expression, int depth)
    {
        var name = expression.FunctionName.ToUpperInvariant();
        var valid = name switch
        {
            "SUM" => expression.Arguments.Length == 1,
            "ROUND" or "NULLIF" => expression.Arguments.Length == 2,
            _ => false
        };

        if (!valid)
            throw new FormatException($"Unsupported function or argument count [{expression.FunctionName}].");

        var arguments = expression.Arguments
            .Select(argument => ParseQueryExpression(tableContext, argument, depth + 1))
            .ToArray();
        return new FunctionSqlExpression(name!, arguments, typeof(decimal));
    }

    private static decimal ToDecimal(object value)
    {
        if (value is string text)
        {
            if (text.EndsWith('m') || text.EndsWith('M'))
                text = text[..^1];
            if (decimal.TryParse(
                text,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed))
                return parsed;
            throw new FormatException("A projection value must be numeric.");
        }

        try
        {
            return Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (FormatException exception)
        {
            throw new FormatException("A projection value must be numeric.", exception);
        }
        catch (InvalidCastException exception)
        {
            throw new FormatException("A projection value must be numeric.", exception);
        }
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
