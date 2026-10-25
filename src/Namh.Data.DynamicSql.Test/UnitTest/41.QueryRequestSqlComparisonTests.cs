using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
[Ignore("Skipping tests temporarily. Implement CTE")]
internal sealed class _41_QueryRequestSqlComparisonTests : TestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IEnumerable<TestCaseData> QueryRequests()
    {
        var repositoryRoot = GetRepositoryRoot();
        var requestsPath = Path.Combine(
            repositoryRoot,
            "src",
            "Demo.WebApi",
            "FileStorage",
            "UserData",
            "123",
            "QueryRequests");
        var markdownPath = Path.Combine(repositoryRoot, "scripts", "northstar-demo-queries.md");
        var markdown = File.ReadAllText(markdownPath);

        foreach (var requestPath in Directory.EnumerateFiles(requestsPath, "*.json")
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var title = Path.GetFileNameWithoutExtension(requestPath);
            title = title[(title.IndexOf(" - ", StringComparison.Ordinal) + 3)..];

            yield return new TestCaseData(
                    requestPath,
                    ExtractSqlServerStatement(markdown, title))
                .SetName($"QueryRequestMatchesRawSql_{Path.GetFileNameWithoutExtension(requestPath)}");
        }
    }

    [TestCaseSource(nameof(QueryRequests))]
    public void QueryRequestMatchesRawSqlStatement(string requestPath, string rawSql)
    {
        var request = JsonSerializer.Deserialize<StoredQueryRequest>(
            File.ReadAllText(requestPath),
            JsonOptions)
            ?? throw new InvalidDataException($"Query request '{requestPath}' is empty.");

        var definition = new QueryDefinition
        {
            Entity = request.Entity,
            Relations = request.Relations,
            Projection = request.Projection,
            Filter = request.Filters,
            Sorts = request.Sorts?.SelectMany(sorts => sorts).ToList() ?? [],
            Page = request.Page,
            PageSize = request.PageSize
        };

        var queryRecords = _db.Query(definition).ToList();
        var rawRecords = ExecuteRawSql(rawSql);
        var requestName = Path.GetFileNameWithoutExtension(requestPath);

        Assert.That(queryRecords, Has.Count.EqualTo(rawRecords.Count), requestName);
        for (var rowIndex = 0; rowIndex < queryRecords.Count; rowIndex++)
        {
            var queryRow = queryRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var rawRow = rawRecords[rowIndex];

            Assert.That(queryRow.Keys, Is.EquivalentTo(rawRow.Keys), $"{requestName}, row {rowIndex}");
            foreach (var (column, expected) in rawRow)
            {
                AssertValuesMatch(
                    queryRow[column],
                    expected,
                    $"{requestName}, row {rowIndex}, column '{column}'");
            }
        }
    }

    private static IReadOnlyList<Dictionary<string, object?>> ExecuteRawSql(string sql)
    {
        using var connection = DefinedData.GetDbConnection(DbServerType.SqlServer);
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        var records = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
            {
                record.Add(
                    reader.GetName(columnIndex),
                    reader.IsDBNull(columnIndex) ? null : reader.GetValue(columnIndex));
            }

            records.Add(record);
        }

        return records;
    }

    private static void AssertValuesMatch(object? actual, object? expected, string context)
    {
        if (IsNumber(actual) && IsNumber(expected))
        {
            var actualNumber = Convert.ToDecimal(actual, CultureInfo.InvariantCulture);
            var expectedNumber = Convert.ToDecimal(expected, CultureInfo.InvariantCulture);
            var tolerance = IsIntegral(actual) && IsIntegral(expected) ? 0m : 0.01m;
            Assert.That(Math.Abs(actualNumber - expectedNumber), Is.LessThanOrEqualTo(tolerance), context);
            return;
        }

        Assert.That(actual, Is.EqualTo(expected), context);
    }

    private static bool IsNumber(object? value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong or
            float or double or decimal;

    private static bool IsIntegral(object? value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong;

    private static string ExtractSqlServerStatement(string markdown, string title)
    {
        var lines = markdown.Split('\n');
        var headingIndex = Array.FindIndex(
            lines,
            line => line.TrimEnd('\r').EndsWith($". {title}", StringComparison.Ordinal));
        if (headingIndex < 0)
            throw new InvalidDataException($"No query section named '{title}' was found in the query Markdown.");

        var sectionEnd = Array.FindIndex(
            lines,
            headingIndex + 1,
            line => line.StartsWith("### ", StringComparison.Ordinal) ||
                line.StartsWith("## ", StringComparison.Ordinal));
        if (sectionEnd < 0)
            sectionEnd = lines.Length;

        var sqlServerIndex = Array.FindIndex(
            lines,
            headingIndex,
            sectionEnd - headingIndex,
            line => line.Trim() == "=== \"SQL Server\"");
        if (sqlServerIndex < 0)
            throw new InvalidDataException($"Query section '{title}' has no SQL Server statement.");

        var codeStart = Array.FindIndex(
            lines,
            sqlServerIndex + 1,
            sectionEnd - sqlServerIndex - 1,
            line => line.Trim() == "```sql");
        if (codeStart < 0)
            throw new InvalidDataException($"Query section '{title}' has no SQL code block.");

        var codeEnd = Array.FindIndex(
            lines,
            codeStart + 1,
            sectionEnd - codeStart - 1,
            line => line.Trim() == "```");
        if (codeEnd < 0)
            throw new InvalidDataException($"Query section '{title}' has an unterminated SQL code block.");

        return string.Join(
            Environment.NewLine,
            lines[(codeStart + 1)..codeEnd].Select(line =>
                line.StartsWith("    ", StringComparison.Ordinal) ? line[4..] : line));
    }

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;

    private sealed class StoredQueryRequest
    {
        public required string Entity { get; init; }
        public List<RelationDefinition> Relations { get; init; } = [];
        public List<ProjectionDefinition> Projection { get; init; } = [];
        public FilterDefinition? Filters { get; init; }
        public List<List<SortDefinition>>? Sorts { get; init; }
        public int? Page { get; init; }
        public int? PageSize { get; init; }
    }
}
