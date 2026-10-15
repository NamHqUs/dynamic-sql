using Namh.Data.DynamicSql.Internal.Compilation.Expressions;
using Namh.Data.DynamicSql.Internal.Compilation.Models;
using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;

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

        if (TryParseAggregateFunction(fieldPath, out var wrappedAggregate))
            return ParseFunctionExpression(tableContext, wrappedAggregate, 0);

        var i = fieldPath.LastIndexOf('.');
        var relationPath = i < 0 ? null : fieldPath[..i];
        var fieldName = fieldPath[(i + 1)..];

        var joinedData = ParseRelationPath(tableContext, relationPath);

        if (TryParseAggregateFunction(fieldName, out var aggregateFunction))
        {
            var lastTable = ApplyJoinAggregate(tableContext, joinedData);
            var function = (FunctionSqlExpression)ParseFunctionExpression(lastTable, aggregateFunction, 0);
            return new AggregateExpression(lastTable, function, !joinedData.Any());
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
        => expression.Value switch
        {
            string value => new ValueExpression(AddDbParameter(value, true), typeof(string)),
            null => throw new FormatException("A value query expression requires a numeric value."),
            var value => new ValueExpression(AddDbParameter(ToDecimal(value), true), typeof(decimal))
        };

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
        var definition = metaProvider.GetFunction(expression.FunctionName);
        var suppliedArguments = expression.Arguments
            ?? throw new FormatException("A function's arguments cannot be null.");
        var minimumArgumentCount = definition.Arguments.Count(argument => !argument.Optional);
        if (suppliedArguments.Length < minimumArgumentCount ||
            suppliedArguments.Length > definition.Arguments.Count)
            throw new FormatException($"Unsupported function or argument count [{expression.FunctionName}].");

        var arguments = new FunctionSqlArgument?[definition.Arguments.Count];
        for (var index = 0; index < definition.Arguments.Count; index++)
        {
            var argumentDefinition = definition.Arguments[index];
            arguments[index] = index < suppliedArguments.Length
                ? ParseFunctionArgument(tableContext, argumentDefinition, suppliedArguments[index], depth + 1)
                : ParseDefaultFunctionArgument(tableContext, argumentDefinition, depth + 1);
        }

        return new FunctionSqlExpression(definition, arguments);
    }

    private FunctionSqlArgument ParseFunctionArgument(
        TableExpression tableContext,
        FunctionArgumentDefinition definition,
        IFieldDto expression,
        int depth)
    {
        if (expression is null)
            throw new FormatException($"Function argument [{definition.Name}] cannot be null.");

        return definition.Kind switch
        {
            FunctionArgumentKind.QueryExpression =>
                new FunctionSqlExpressionArgument(ParseQueryExpression(tableContext, expression, depth)),

            FunctionArgumentKind.QueryExpressionOrSqlToken when expression is FieldDto field &&
                definition.AllowedTokens.Contains(field.FieldName, StringComparer.OrdinalIgnoreCase) =>
                new FunctionSqlTokenArgument(field.FieldName),

            FunctionArgumentKind.QueryExpressionOrSqlToken =>
                new FunctionSqlExpressionArgument(ParseQueryExpression(tableContext, expression, depth)),

            FunctionArgumentKind.Constant when expression is ConstFieldDto constant &&
                definition.AllowedConstants.Contains(constant.Value) =>
                new FunctionSqlExpressionArgument(ParseValueExpression(constant)),

            FunctionArgumentKind.Constant =>
                throw new FormatException($"Invalid constant for function argument [{definition.Name}]."),

            FunctionArgumentKind.SqlType when expression is ConstFieldDto { Value: string typeName } &&
                definition.AllowedTypes.Contains(typeName, StringComparer.OrdinalIgnoreCase) =>
                new FunctionSqlTypeArgument(metaProvider.GetSqlType(typeName)),

            FunctionArgumentKind.SqlType =>
                throw new FormatException($"Invalid logical SQL type for function argument [{definition.Name}]."),

            _ => throw new FormatException($"Unsupported function argument kind [{definition.Kind}].")
        };
    }

    private FunctionSqlArgument? ParseDefaultFunctionArgument(
        TableExpression tableContext,
        FunctionArgumentDefinition definition,
        int depth)
    {
        if (definition.DefaultValue is not { } defaultValue)
            return null;

        return defaultValue.Kind switch
        {
            FunctionArgumentDefaultKind.Constant =>
                new FunctionSqlExpressionArgument(
                    ParseQueryExpression(tableContext, new ConstFieldDto(defaultValue.Value), depth)),
            FunctionArgumentDefaultKind.SqlToken when defaultValue.Value is string token &&
                definition.AllowedTokens.Contains(token, StringComparer.OrdinalIgnoreCase) =>
                new FunctionSqlTokenArgument(token),
            FunctionArgumentDefaultKind.LogicalType when defaultValue.Value is string typeName &&
                definition.AllowedTypes.Contains(typeName, StringComparer.OrdinalIgnoreCase) =>
                new FunctionSqlTypeArgument(metaProvider.GetSqlType(typeName)),
            _ => throw new FormatException($"Invalid default for function argument [{definition.Name}].")
        };
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

    private bool TryParseAggregateFunction(string fieldName, out FunctionFieldDto function)
    {
        function = null!;
        if (!fieldName.Contains('(') || !fieldName.EndsWith(')'))
            return false;

        if (FieldDtoParser.Parse(fieldName) is not FunctionFieldDto parsedFunction ||
            !metaProvider.TryGetFunction(parsedFunction.FunctionName, out var definition) ||
            definition is not { IsAggregate: true })
            return false;
        if (parsedFunction.Arguments.Any(argument =>
            argument is not FieldDto field ||
            field.FieldName.Contains('(') ||
            field.FieldName.Contains(')')))
            return false;

        function = parsedFunction;
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
