using System.Linq.Expressions;
using System.Reflection;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130
public static class DbQueryableExtension
{
    internal class DbQueryableDummy
    {
        public static IDbQueryable Where(IDbQueryable source, Expression predicate) { return source; }
        public static IDbQueryable OrderBy(IDbQueryable source, Expression selection) { return source; }
        public static IDbQueryable ThenBy(IDbQueryable source, Expression selection) { return source; }
        public static IDbQueryable OrderByDescending(IDbQueryable source, Expression selection) { return source; }
        public static IDbQueryable ThenByDescending(IDbQueryable source, Expression selection) { return source; }
    }

    public static IDbQueryable Select(this IDbQueryable source, params string[] fields)
    {
        if (fields == null || fields.Length == 0)
            return source;

        var selection = new DataRecord();
        foreach (var e in fields)
            selection[e] = e;
        return source.Select(selection);
    }
    public static IDbQueryable Select(this IDbQueryable source, DataRecord selection)
        => source.Select(e => selection);
    public static IDbQueryable Select(this IDbQueryable source, Expression<Func<IRecordParameter, DataRecord>> selection)
        => source.CreateQuery(MethodBase.GetCurrentMethod()!, Expression.Quote(selection));

    public static IDbQueryable Where(this IDbQueryable source, Expression<Func<IRecordParameter, bool>> predicate)
        => source.CreateQuery(MethodBase.GetCurrentMethod()!, Expression.Quote(predicate));

    public static IDbQueryable OrderBy(this IDbQueryable source, params string[] fields)
        => source.CreateOrderQueryable(MethodBase.GetCurrentMethod()!.Name, fields);
    public static IDbQueryable ThenBy(this IDbQueryable source, params string[] fields)
        => source.CreateOrderQueryable(MethodBase.GetCurrentMethod()!.Name, fields);
    public static IDbQueryable OrderByDescending(this IDbQueryable source, params string[] fields)
        => source.CreateOrderQueryable(MethodBase.GetCurrentMethod()!.Name, fields);
    public static IDbQueryable ThenByDescending<TSource>(this IDbQueryable source, params string[] fields) where TSource : DataRecord
        => source.CreateOrderQueryable(MethodBase.GetCurrentMethod()!.Name, fields);

    public static IDbQueryable Skip(this IDbQueryable source, int count)
        => source.CreateQuery(MethodBase.GetCurrentMethod()!, Expression.Constant(count));
    public static List<DataRecord> Take(this IDbQueryable source, int count)
        => source.CreateQuery(MethodBase.GetCurrentMethod()!, Expression.Constant(count)).ToList();

    public static List<DataRecord> ToList(this IDbQueryable source)
        => [.. (IEnumerable<DataRecord>)source];
    public static DataRecord FirstOrDefault(this IDbQueryable source, Expression<Func<IRecordParameter, bool>> predicate)
        => source.Where(predicate).FirstOrDefault();
    public static DataRecord FirstOrDefault(this IDbQueryable source)
        => source.Take(1).FirstOrDefault()!;

    public static double Aggregate(this IDbQueryable source, string aggregateFieldName)
    {
        var expression = source.CreateQuery(MethodBase.GetCurrentMethod()!, Expression.Constant(aggregateFieldName)).Expression;
        return Convert.ToDouble(source.Provider.ExecuteScalar(expression));
    }

    public static int Count(this IDbQueryable source)
    {
        var expression = source.CreateQuery(MethodBase.GetCurrentMethod()!).Expression;
        return (int)source.Provider.ExecuteScalar(expression)!;
    }
    public static int Count(this IDbQueryable source, Expression<Func<IRecordParameter, bool>> predicate)
        => source.Where(predicate).Count();

    public static bool Any(this IDbQueryable source, Expression<Func<IRecordParameter, bool>> predicate)
    {
        var expression = source.CreateQuery(MethodBase.GetCurrentMethod()!).Expression;
        return (bool)source.Provider.ExecuteScalar(expression)!;
    }

    private static IDbQueryable CreateQuery(this IDbQueryable source, MethodBase method, params Expression[] expressions)
    {
        var arguments = new List<Expression> { source.Expression };
        arguments.AddRange(expressions);

        return source.Provider.CreateQuery(Expression.Call(null, (MethodInfo)method, arguments.ToArray()));
    }
    private static IDbQueryable CreateOrderQueryable(this IDbQueryable source, string methodName, params string[] fields)
    {
        Expression<Func<IRecordParameter, string[]>> lambda = (e) => fields;
        return source.CreateQuery(typeof(DbQueryableDummy).GetMethod(methodName)!, Expression.Quote(lambda));
    }

}
