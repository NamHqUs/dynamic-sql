using Namh.Data.DynamicSql;

namespace Demo.Contracts.Dto;

/// <summary>Represents a built-in or saved report.</summary>
public sealed record ReportDto(
    string FileName,
    QueryDefinition Query,
    string? RequestText = null,
    string? Description = null,
    string? ChartType = null)
{
    public bool CanDelete { get; init; } = true;
}
