namespace Namh.Data.DynamicSql;

public sealed class FieldExpressionTransaltion
{
    public required string Kind { get; init; }
    public string? Field { get; init; }
    public decimal? Value { get; init; }
    public string? Operator { get; init; }
    public string? Function { get; init; }
    public List<FieldExpressionTransaltion> Arguments { get; init; } = [];

    public override string ToString()
    {
        return "Not implemented yet";
    }

}
