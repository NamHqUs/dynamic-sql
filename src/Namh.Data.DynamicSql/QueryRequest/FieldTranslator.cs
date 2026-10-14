#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public sealed record FieldTranslator(
    string? Field,
    string? Alias = null,
    FieldExpressionTransaltion? Expression = null)
{
    public override string ToString()
    {
        var name = Field is not null ? $".{Field}" : Expression?.ToString() ?? string.Empty;

        return Alias is null ? name : $"{name} | {Alias}";
    }
}
