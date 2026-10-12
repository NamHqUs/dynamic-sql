using Namh.Data.DynamicSql.Internal.Compilation.Expressions;
using Namh.Data.DynamicSql.Internal.Compilation.Models;
using System.Text;

namespace Namh.Data.DynamicSql.Internal.Compilation;

internal sealed record SqlStatement(string CommandText, IReadOnlyList<DbParameter> Parameters);

internal sealed class SqlStatementGenerator(ISqlDialect dialect)
{
    public SqlStatement Generate(TableExpression query, IEnumerable<DbParameter> parameters)
    {
        var text = RenderQuery(query);

        return new SqlStatement(text, parameters.ToArray());
    }

    private string RenderQuery(TableExpression query, bool exists = false)
    {
        query.EnsureDefaultProjection();
        var hasGroupedAggregate = query.ProjectionItems
            .Any(item => item.Item is AggregateExpression { GroupWithQuery: true });

        var select = exists ? "1" : query.SqlSelect?.ToString();

        if (!exists && query is AggregateExpression aggregate && select is not null)
            select = RenderAggregateSelect(aggregate);

        if (select is null)
            select = string.Join(", ", query.ProjectionItems.Select(item => RenderProjection(item, hasGroupedAggregate)));

        if (query.IsTake)
            select = dialect.RenderTakePrefix(query.Take!.Value) + select;

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
            var groupBy = query.ProjectionItems
                .Where(item => item.Item is not AggregateExpression)
                .Select(item => RenderExpression(item.Item))
                .ToArray();
            if (groupBy.Length > 0)
                text += "\nGROUP BY " + string.Join(", ", groupBy);
        }

        if (query.Orders.Count != 0)
            text += "\nORDER BY " + string.Join(", ", query.Orders.Select(RenderOrder));

        text += dialect.RenderPaging(query.Skip, query.IsTake ? null : query.Take);

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

            RawSqlExpression raw => raw.Sql.Replace("__ARCHIVE__", dialect.QuoteIdentifier("DeletedOn"), StringComparison.Ordinal),

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

            AggregateExpression aggregate => $"({RenderAggregate(aggregate)})",

            _ => throw new NotSupportedException($"Unsupported SQL expression {expression.GetType().Name}")
        };

    private string RenderAggregate(AggregateExpression aggregate)
    {
        aggregate.EnsureDefaultProjection();

        return RenderQuery(aggregate);
    }

    private string RenderAggregateSelect(AggregateExpression aggregate)
    {
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
