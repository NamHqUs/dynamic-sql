using Namh.Data.DynamicSql.Execution;
using System.Linq.Expressions;
using System.Reflection;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130
public static class IDbQueryableExtension
{
    internal class DbQueryableDummy
    {
        public static IDbQueryable Where(IDbQueryable source, Expression predicate) { return source; }
        public static IDbQueryable OrderBy(IDbQueryable source, Expression selection) { return source; }
        public static IDbQueryable ThenBy(IDbQueryable source, Expression selection) { return source; }
        public static IDbQueryable OrderByDescending(IDbQueryable source, Expression selection) { return source; }
        public static IDbQueryable ThenByDescending(IDbQueryable source, Expression selection) { return source; }
        public static IDbQueryable OrderByField(IDbQueryable source, IFieldDefinition field) { return source; }
        public static IDbQueryable OrderByDescendingField(IDbQueryable source, IFieldDefinition field) { return source; }
        public static IDbQueryable ThenByField(IDbQueryable source, IFieldDefinition field) { return source; }
        public static IDbQueryable ThenByDescendingField(IDbQueryable source, IFieldDefinition field) { return source; }
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
    public static IDbQueryable ThenByDescending(this IDbQueryable source, params string[] fields)
        => source.CreateOrderQueryable(MethodBase.GetCurrentMethod()!.Name, fields);
    public static IDbQueryable GroupBy(this IDbQueryable source, params string[] fields)
        => source.CreateQuery(MethodBase.GetCurrentMethod()!, Expression.Constant(fields));
    public static IDbQueryable OrderByDescending(this IDbQueryable source, params string[] fields)
        => source.CreateOrderQueryable(MethodBase.GetCurrentMethod()!.Name, fields);
    public static IDbQueryable ThenByDescending<TSource>(this IDbQueryable source, params string[] fields) where TSource : DataRecord
        => source.CreateOrderQueryable(MethodBase.GetCurrentMethod()!.Name, fields);

    internal static IDbQueryable OrderByField(this IDbQueryable source, IFieldDefinition field)
        => source.CreateOrderFieldQueryable(nameof(DbQueryableDummy.OrderByField), field);
    internal static IDbQueryable OrderByDescendingField(this IDbQueryable source, IFieldDefinition field)
        => source.CreateOrderFieldQueryable(nameof(DbQueryableDummy.OrderByDescendingField), field);
    internal static IDbQueryable ThenByField(this IDbQueryable source, IFieldDefinition field)
        => source.CreateOrderFieldQueryable(nameof(DbQueryableDummy.ThenByField), field);
    internal static IDbQueryable ThenByDescendingField(this IDbQueryable source, IFieldDefinition field)
        => source.CreateOrderFieldQueryable(nameof(DbQueryableDummy.ThenByDescendingField), field);

    public static IDbQueryable Skip(this IDbQueryable source, int count)
        => source.CreateQuery(MethodBase.GetCurrentMethod()!, Expression.Constant(count));
    public static IDbQueryable TakeQuery(this IDbQueryable source, int count)
        => source.CreateQuery(GetQueryMethod(nameof(Take), 2)!, Expression.Constant(count));

    public static List<DataRecord> Take(this IDbQueryable source, int count)
        => source.TakeQuery(count).ToList();

    public static List<DataRecord> ToList(this IDbQueryable source)
        => [.. (IEnumerable<DataRecord>)source];

    public static (List<DataRecord> Records, long TotalRecords) ToListWithTotalRecords(this IDbQueryable source)
        =>  GetTotalRecordsProvider(source.Provider).ExecuteWithTotalRecords(source.Expression);

    public static SqlQuery ToSql(this IDbQueryable source)
        => source.Provider.GetQuery(source.Expression);

    public static Task<List<DataRecord>> ToListAsync(
        this IDbQueryable source,
        CancellationToken cancellationToken = default)
        => source.Provider.ExecuteAsync(source.Expression, cancellationToken);

    public static Task<(List<DataRecord> Records, long TotalRecords)> ToListWithTotalRecordsAsync(
        this IDbQueryable source,
        CancellationToken cancellationToken = default)
        => GetTotalRecordsProvider(source.Provider)
            .ExecuteWithTotalRecordsAsync(source.Expression, cancellationToken);

    public static DataRecord FirstOrDefault(this IDbQueryable source, Expression<Func<IRecordParameter, bool>> predicate)
        => source.Where(predicate).FirstOrDefault();
    public static DataRecord FirstOrDefault(this IDbQueryable source)
        => source.Take(1).FirstOrDefault()!;
    public static Task<DataRecord?> FirstOrDefaultAsync(
        this IDbQueryable source,
        Expression<Func<IRecordParameter, bool>> predicate,
        CancellationToken cancellationToken = default)
        => source.Where(predicate).FirstOrDefaultAsync(cancellationToken);
    public static async Task<DataRecord?> FirstOrDefaultAsync(
        this IDbQueryable source,
        CancellationToken cancellationToken = default)
        => (await source.TakeAsync(1, cancellationToken)).FirstOrDefault();

    public static int Count(this IDbQueryable source)
    {
        var expression = source.CreateQuery(MethodBase.GetCurrentMethod()!).Expression;
        return Convert.ToInt32(source.Provider.ExecuteScalar(expression));
    }
    public static int Count(this IDbQueryable source, Expression<Func<IRecordParameter, bool>> predicate)
        => source.Where(predicate).Count();
    public static Task<int> CountAsync(
        this IDbQueryable source,
        CancellationToken cancellationToken = default)
        => ExecuteScalarAsync<int>(source, cancellationToken);
    public static Task<int> CountAsync(
        this IDbQueryable source,
        Expression<Func<IRecordParameter, bool>> predicate,
        CancellationToken cancellationToken = default)
        => source.Where(predicate).CountAsync(cancellationToken);

    public static bool Any(this IDbQueryable source, Expression<Func<IRecordParameter, bool>> predicate)
    {
        var expression = source.CreateQuery(MethodBase.GetCurrentMethod()!).Expression;
        return Convert.ToBoolean(source.Provider.ExecuteScalar(expression));
    }
    public static Task<bool> AnyAsync(
        this IDbQueryable source,
        Expression<Func<IRecordParameter, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var expression = source.CreateQuery(GetQueryMethod(nameof(Any), 1)!).Expression;
        return ExecuteScalarAsync<bool>(source, expression, cancellationToken);
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
    private static IDbQueryable CreateOrderFieldQueryable(this IDbQueryable source, string methodName, IFieldDefinition field)
        => source.CreateQuery(
            typeof(DbQueryableDummy).GetMethod(methodName)!,
            Expression.Constant(field, typeof(IFieldDefinition)));

    private static Task<T> ExecuteScalarAsync<T>(IDbQueryable source, CancellationToken cancellationToken)
        => ExecuteScalarAsync<T>(
            source,
            source.CreateQuery(GetQueryMethod(typeof(T) == typeof(int) ? nameof(Count) : nameof(Any), 1)!).Expression,
            cancellationToken);

    private static async Task<T> ExecuteScalarAsync<T>(
        IDbQueryable source,
        Expression expression,
        CancellationToken cancellationToken)
    {
        var value = await source.Provider.ExecuteScalarAsync(expression, cancellationToken);

        if (typeof(T) == typeof(int))
            return (T)(object)Convert.ToInt32(value);

        if (typeof(T) == typeof(bool))
            return (T)(object)Convert.ToBoolean(value);

        return (T)value!;
    }

    private static Task<List<DataRecord>> TakeAsync(
        this IDbQueryable source,
        int count,
        CancellationToken cancellationToken)
        => source.Provider.ExecuteAsync(
            source.CreateQuery(GetQueryMethod(nameof(Take), 2)!, Expression.Constant(count)).Expression,
            cancellationToken);

    private static MethodInfo? GetQueryMethod(string name, int parameterCount)
        => typeof(IDbQueryableExtension).GetMethods()
            .FirstOrDefault(method => method.Name == name && method.GetParameters().Length == parameterCount);

    private static DbQueryProvider GetTotalRecordsProvider(IDbQueryProvider provider)
        => provider as DbQueryProvider
            ?? throw new NotSupportedException(
                $"The query provider [{provider.GetType().FullName}] does not support total-record queries.");
}
