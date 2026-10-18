using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.Compilation.Expressions;
using Namh.Data.DynamicSql.Compilation.Models;
using System.Text;

namespace Namh.Data.DynamicSql.Compilation;

internal sealed record SqlStatement(string CommandText, IReadOnlyList<DbParameter> Parameters);

internal sealed class SqlStatementGenerator(ISqlDialect dialect)
{
    public SqlStatement Generate(TableExpression query, IEnumerable<DbParameter> parameters)
    {
        var text = RenderQuery(query);

        return new SqlStatement(text, parameters.ToArray());
    }

    public SqlStatement GenerateTotalRecords(
        TableExpression query,
        IEnumerable<DbParameter> parameters,
        int maximumRecords)
    {
        query.EnsureDefaultProjection();
        var limitedQuery = new TakeExpression(query.WithoutPaging(), maximumRecords);
        var alias = dialect.QuoteIdentifier("__bounded_count");
        var text = $"SELECT COUNT(*)\nFROM ({RenderQuery(limitedQuery)}) AS {alias}";

        return new SqlStatement(text, parameters.ToArray());
    }

    private string RenderQuery(TableExpression query, bool exists = false)
    {
        query.EnsureDefaultProjection();
        var hasGroupedAggregate = query.ProjectionItems
            .Any(item => item.Item.ContainsGroupedAggregate());

        var select = exists ? "1" : query.SqlSelect?.ToString();

        if (!exists && query is AggregateExpression aggregate && select is not null)
            select = RenderAggregateSelect(aggregate);

        if (select is null)
            select = string.Join(", ", query.ProjectionItems.Select(item => RenderProjection(item, hasGroupedAggregate)));

        var takePrefix = query.IsTake ? dialect.RenderTakePrefix(query.Take!.Value) : string.Empty;
        if (takePrefix.Length != 0)
            select = takePrefix + select;

        var text = $"SELECT {select}\nFROM {RenderTable(query)}";

        foreach (var join in query.Joins)
            text += $"\n\t{join.JoinType} {RenderTable(join.Table)} ON " +
                string.Join(" AND ", join.Conditions.Select(RenderExpression));

        if (query.SqlWhere.Count != 0)
        {
            text += "\nWHERE " + RenderExpression(query.SqlWhere[0]);

            foreach (var where in query.SqlWhere.Skip(1))
                text += "\n\tAND " + RenderExpression(where);
        }

        if (hasGroupedAggregate)
        {
            var groupBy = query.GroupByExpressions
                .Concat(query.ProjectionItems
                    .Where(item => !item.Item.ContainsGroupedAggregate())
                    .Select(item => item.Item))
                .Select(RenderExpression)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (groupBy.Length > 0)
                text += "\nGROUP BY " + string.Join(", ", groupBy);
        }

        if (query.SqlHaving.Count != 0)
        {
            text += "\nHAVING " + RenderExpression(query.SqlHaving[0]);

            foreach (var having in query.SqlHaving.Skip(1))
                text += "\n\tAND " + RenderExpression(having);
        }

        if (query.Orders.Count != 0)
            text += "\nORDER BY " + string.Join(", ", query.Orders.Select(RenderOrder));

        text += dialect.RenderPaging(
            query.Skip,
            takePrefix.Length != 0 ? null : query.Take,
            query.Orders.Count != 0);

        return text;
    }

    private string RenderProjection(ProjectionItem item, bool hasGroupedAggregate = false)
    {
        if (hasGroupedAggregate && item.Item is AggregateExpression { GroupWithQuery: true } aggregate)
            return $"{RenderAggregateSelect(aggregate)} {dialect.QuoteIdentifier(item.Alias)}";

        var expression = RenderExpression(item.Item);

        if (item.Item is not FieldExpression)
            expression = $"({expression})";

        return $"{expression} {dialect.QuoteIdentifier(item.Alias)}";
    }

    private string RenderTable(TableExpression table)
        => $"{dialect.RenderTable(table.Entity)} {table.Alias}";

    private string RenderOrder(OrderExpression order)
    {
        var expression = order.Expression is AggregateExpression { GroupWithQuery: true } aggregate
            ? RenderAggregateSelect(aggregate)
            : RenderExpression(order.Expression);

        return $"{expression}{(order.Descending ? " DESC" : " ASC")}";
    }

    private string RenderExpression(SqlExpression expression)
        => expression switch
        {
            FieldExpression field => field.TableAlias.Length == 0
                ? $"({field.MetaField.RawSql})"
                : field.MetaField.FieldType == FieldType.DbColumn
                    ? $"{field.TableAlias}.{dialect.QuoteIdentifier(field.MetaField.RawSql)}"
                    : $"({field.MetaField.RawSql.Replace("@", field.TableAlias + ".")})",

            RawSqlExpression raw => raw.Sql,

            ValueExpression value => RenderValue(value.Value),

            BinarySqlExpression binary => $"({(binary.StringConcat
                ? dialect.RenderStringConcatenation(RenderExpression(binary.Left), RenderExpression(binary.Right))
                : $"{RenderExpression(binary.Left)} {binary.Operator} {RenderExpression(binary.Right)}")})",

            CompareSqlExpression compare => $"({RenderExpression(compare.Left)} {compare.Comparison})",

            PredicateExpression predicate => predicate.Right is null
                ? RenderExpression(predicate.Left)
                : $"({RenderExpression(predicate.Left)} {predicate.Operator} {RenderExpression(predicate.Right)})",

            ProjectionExpression projection => $"{(projection.Parenthesize ? "(" : "")}{RenderExpression(projection.Expression)}{(projection.Parenthesize ? ")" : "")} {dialect.QuoteIdentifier(projection.Alias)}",

            SubqueryExpression subquery => $"({RenderQuery(subquery.Query)})",

            AnyExpression any => $"EXISTS({RenderQuery(any.Query, true)})",

            AggregateExpression aggregate => aggregate.GroupWithQuery
                ? RenderAggregateSelect(aggregate)
                : $"({RenderAggregate(aggregate)})",

            FunctionSqlExpression function =>
                RenderFunction(function),

            _ => throw new NotSupportedException($"Unsupported SQL expression {expression.GetType().Name}")
        };

    private string RenderFunction(FunctionSqlExpression function)
    {
        var arguments = function.Arguments
            .Select(argument => argument switch
            {
                FunctionSqlExpressionArgument expression => RenderExpression(expression.Expression),
                FunctionSqlTokenArgument token => token.Token,
                null => null,
                _ => throw new NotSupportedException(
                    $"Unsupported function argument type [{argument.GetType().Name}].")
            })
            .ToArray();
        var template = function.Definition.GetSqlTemplate(dialect.Key);
        var sql = new StringBuilder(template.Length);

        for (var index = 0; index < template.Length; index++)
        {
            if (template[index] != '{')
            {
                sql.Append(template[index]);
                continue;
            }

            var close = template.IndexOf('}', index + 1);
            var placeholder = template.Substring(index + 1, close - index - 1);
            var argumentIndex = int.TryParse(placeholder, out var numericIndex)
                ? numericIndex
                : function.Definition.Arguments
                    .Select((argument, argumentIndex) => (argument, argumentIndex))
                    .First(pair => string.Equals(placeholder, pair.argument.Name, StringComparison.OrdinalIgnoreCase))
                    .argumentIndex + 1;
            sql.Append(arguments[argumentIndex - 1]
                ?? throw new FormatException(
                    $"SQL template for function [{function.Definition.Name}] references an omitted argument."));
            index = close;
        }

        return sql.ToString();
    }

    private string RenderAggregate(AggregateExpression aggregate)
    {
        aggregate.EnsureDefaultProjection();

        return RenderQuery(aggregate);
    }

    private string RenderAggregateSelect(AggregateExpression aggregate)
    {
        if (aggregate.FunctionExpression is not null)
            return RenderExpression(aggregate.FunctionExpression);

        var sql = aggregate.SelectSql;
        var result = new StringBuilder(sql.Length);

        for (var index = 0; index < sql.Length; index++)
        {
            if (sql[index] != '@')
            {
                result.Append(sql[index]);
                continue;
            }

            var fieldStart = index + 1;
            var fieldEnd = fieldStart;
            while (fieldEnd < sql.Length &&
                (char.IsLetterOrDigit(sql[fieldEnd]) || sql[fieldEnd] == '_'))
                fieldEnd++;

            if (fieldEnd == fieldStart)
            {
                result.Append('@');
                continue;
            }

            result.Append(aggregate.Alias)
                .Append('.')
                .Append(dialect.QuoteIdentifier(sql[fieldStart..fieldEnd]));
            index = fieldEnd - 1;
        }

        return result.ToString();
    }

    private string RenderValue(object? value)
        => value is null ? "NULL" :
            value is string s && !s.StartsWith("@", StringComparison.Ordinal) &&
                !(s.Length >= 2 && s[0] == '\'' && s[^1] == '\'') &&
                !decimal.TryParse(s, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out _)
                ? dialect.RenderStringLiteral(s) : value.ToString()!;
}
