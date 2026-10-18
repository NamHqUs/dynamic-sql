using Demo.Contracts.Repositories;
using Demo.Contracts.Dto;

namespace Demo.Repositories.InMemory;

public sealed class ReportMemoryRepository : RepositoryBase<ReportDto>, IReportRepository
{
    private readonly Dictionary<string, Dictionary<string, ReportDto>> _reports =
        new(StringComparer.OrdinalIgnoreCase);

    public override IReadOnlyCollection<ReportDto> GetUserDefined(string userSectionId)
        => _reports.TryGetValue(userSectionId, out var reports)
            ? reports.Values.OrderByDescending(report => report.FileName).ToArray()
            : [];

    public override IReadOnlyCollection<ReportDto> GetBuiltin() => [];

    public override ReportDto? Get(string userSectionId, string fileName)
        => _reports.TryGetValue(userSectionId, out var reports)
            ? reports.GetValueOrDefault(fileName)
            : null;

    public override void Save(string userSectionId, ReportDto report)
        => GetOrCreateSection(userSectionId)[report.FileName] = report;

    public override bool Delete(string userSectionId, string fileName)
        => _reports.TryGetValue(userSectionId, out var reports) && reports.Remove(fileName);

    private Dictionary<string, ReportDto> GetOrCreateSection(string userSectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userSectionId);
        if (!_reports.TryGetValue(userSectionId, out var reports))
        {
            reports = new Dictionary<string, ReportDto>(StringComparer.OrdinalIgnoreCase);
            _