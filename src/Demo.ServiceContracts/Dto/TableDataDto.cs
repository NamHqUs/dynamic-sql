namespace Demo.ServiceContracts.Dto;

/// <summary>Represents rows and column metadata returned by a demo query.</summary>
public sealed record TableDataDto(
    string Name,
    string Columns,
    string[] Rows,
    int? Page,
    int? PageSize,
    long TotalRecords);
