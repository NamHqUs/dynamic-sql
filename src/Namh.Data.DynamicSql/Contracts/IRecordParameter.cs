#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public interface IRecordParameter
{
    DataValue this[string fieldName] { get; }

    DataValue DbColumn(string dbColumnName);

    DataValue RawSql(string sqlString);

    IDbQueryable Query(string fieldName);
}
