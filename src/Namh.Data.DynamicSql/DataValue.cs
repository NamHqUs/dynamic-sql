namespace Namh.Data.DynamicSql;

public abstract class DataValue
{
    public override bool Equals(object? obj) => throw new NotImplementedException();
    public override int GetHashCode() => throw new NotImplementedException();

    public T As<T>() =>  throw new NotImplementedException();
    public bool RawCompare(string statement) => throw new NotImplementedException();

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
