namespace Namh.Data.DynamicSql;

public class DataRecord : Dictionary<string, object?>, IEnumerable<KeyValuePair<string, object?>>
{
    public DataRecord()
        : base(StringComparer.OrdinalIgnoreCase)
    {
    }
}
