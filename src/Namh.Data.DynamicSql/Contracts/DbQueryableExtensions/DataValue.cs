#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public abstract class DataValue
{
    public override bool Equals(object? obj) => throw new NotImplementedException();
    public override int GetHashCode() => throw new NotImplementedException();

    public T As<T>() =>  throw new NotImplementedException();
    public bool RawCompare(string statement) => throw new NotImplementedException();
    public bool Like(object value) => throw new NotImplementedException();
    public bool NotLike(object value) => throw new NotImplementedException();
    public bool Between(object lower, object upper) => throw new NotImplementedException();
    public bool NotBetween(object lower, object upper) => throw new NotImplementedException();
    public bool In(params object[] values) => throw new NotImplementedException();
    public bool NotIn(params object[] values) => throw new NotImplementedException();
    public bool IsNull() => throw new NotImplementedException();
    public bool IsNotNull() => throw new NotImplementedException();
    public bool CompareTemplate(object value, string template) => throw new NotImplementedException();
    public static DataValue Parameter(object? value) => throw new NotImplementedException();
    public static DataValue QueryExpression(IFieldDefinition expression) => throw new NotImplementedException();

    public static bool operator ==(DataValue value1, object value2) => throw new NotImplementedException();
    public static bool operator !=(DataValue value1, object value2) => throw new NotImplementedException();
    public static bool operator >(DataValue value1, object value2) => throw new NotImplementedException();
    public static bool operator <(DataValue value1, object value2) => throw new NotImplementedException();
    public static bool operator >=(DataValue value1, object value2) => throw new NotImplementedException();
    public static bool operator <=(DataValue value1, object value2) => throw new NotImplementedException();
    public static DataValue operator +(DataValue value1, object value2) => throw new NotImplementedException();
    public static DataValue operator -(DataValue value1, object value2) => throw new NotImplementedException();
    public static DataValue operator *(DataValue value1, DataValue value2) => throw new NotImplementedException();
    public static DataValue operator /(DataValue value1, DataValue value2) => throw new NotImplementedException();

}
