#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public class DataRecord : Dictionary<string, object?>, IEnumerable<KeyValuePair<string, object?>>
{
    public DataRecord()
        : base(StringComparer.OrdinalIgnoreCase)
    {
    }
}
