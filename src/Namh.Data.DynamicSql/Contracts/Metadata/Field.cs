#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public enum FieldType
{
    DbColumn,
    Aggregate,
    RawSql
}
public enum DataType
{
    String = 0,
    Number,
    Datetime,
    Boolean,
}

public record Field(
    string Name,
    DataType DataType = DataType.String,
    FieldType FieldType = FieldType.DbColumn)
{
    public string RawSql { get; set; } = Name;

    public override string ToString() => $"{Name}";
}
