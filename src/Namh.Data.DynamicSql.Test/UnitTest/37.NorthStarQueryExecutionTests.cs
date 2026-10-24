#if NET10_0
using Demo.Contracts.Dto;
using Demo.Repositories.FileStorage;
using Demo.Share;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Namh.Data.DynamicSql;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public sealed class _37_NorthStarQueryExecutionTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string[] NorthStarFunctions =
    [
        "COUNT_DISTINCT", "CountDistinct",
        "COUNT_IF", "CountIf",
        "COUNT_DISTINCT_IF", "CountDistinctIf",
        "SUM_IF", "SumIf",
        "AVG_IF", "AverageIf",
        "MAX_IF", "MaxIf",
        "COALESCE", "CONCAT", "MonthKey", "AgeOn", "ElapsedYears",
        "Difference", "LAG", "WINDOW_SUM"
    ];
    private static readonly HashSet<string> OneDecimalColumns =
    [
        "AverageVisitMinutes", "AverageMinutes", "AverageProcessingDays",
        "AverageTenureYears", "AverageRequestDays"
    ];

    public static IEnumerable<TestCaseData> SavedRequests()
    {
        var storagePath = Path.Combine(RepositoryRoot, "src", "Demo.WebApi", "FileStorage");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStoragePath"] = storagePath
            })
            .Build();
        var repository = new QueryRequestFileRepository(new FilePathResolver(configuration));
        var referenceQueries = ReadReferenceQueries();
        if (referenceQueries.Count != 20)
            throw new InvalidDataException($"Expected 20 reference queries, found {referenceQueries.Count}.");

        return repository.GetUserDefined("123")
            .Select(request => new TestCaseData(
                request,
                referenceQueries[GetReferenceQueryIndex(request.Name)]).SetName(request.Name));
    }

    [TestCaseSource(nameof(SavedRequests))]
    public async Task SavedNorthStarRequestMatchesReference(
        QueryRequestDto request,
        string referenceSql)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seed = connection.CreateCommand())
        {
            seed.CommandText = File.ReadAllText(
                Path.Combine(RepositoryRoot, "scripts", "northstar-demo.sqlite.sql"));
            seed.ExecuteNonQuery();
        }

        var metadata = new MetaProvider(
            BuiltinMetadata.DefaultMetadata,
            CustomFunctionDefintions.Functions);
        using var context = new DynamicSqlContext(connection, metadata, new SqliteDialect());
        var query = new QueryDefinition
        {
            Entity = request.Entity,
            Relations = request.Relations,
            Projection = request.Projection,
            Filter = request.Filters,
            Sorts = request.Sorts?.SelectMany(group => group).ToList() ?? [],
            Page = request.Page,
            PageSize = request.PageSize
        };

        var result = await context.Query(query).ToListWithTotalRecordsAsync();

        Assert.That(result.Records, Has.Count.EqualTo(10), request.Name);
        Assert.That(result.TotalRecords, Is.GreaterThanOrEqualTo(10), request.Name);
        var expectedColumns = request.Projection
            .Select(projection => projection.Alias ?? projection.Field.ToText())
            .ToArray();
        var reference = ReadReferenceResult(connection, referenceSql);
        Assert.That(reference.Columns, Is.EqualTo(expectedColumns), request.Name);
        Assert.That(reference.Rows, Has.Count.EqualTo(10), request.Name);
        Assert.That(result.Records.All(record =>
            expectedColumns.All(record.ContainsKey)), Is.True, request.Name);
        var unmatchedReferenceRows = reference.Rows.ToList();
        foreach (var record in result.Records)
        {
            var matchingReferenceIndex = unmatchedReferenceRows.FindIndex(expectedRow =>
                expectedColumns.Select((column, index) =>
                    AreEquivalentValues(record[column], expectedRow[index], column)).All(matches => matches));
            Assert.That(
                matchingReferenceIndex,
                Is.GreaterThanOrEqualTo(0),
                $"{request.Name}: no matching reference row for " +
                $"[{string.Join(", ", expectedColumns.Select(column => record[column]))}].");
            unmatchedReferenceRows.RemoveAt(matchingReferenceIndex);
        }
        Assert.That(unmatchedReferenceRows, Is.Empty, request.Name);

        foreach (var dialect in new ISqlDialect[] { new SqlServerDialect(), new PostgresDialect() })
        {
            using var providerConnection = new SqliteConnection("Data Source=:memory:");
            using var providerContext = new DynamicSqlContext(providerConnection, metadata, dialect);
            Assert.That(providerContext.Query(query).ToSql().CommandText, Is.Not.Empty, request.Name);
        }
    }

    [Test]
    public async Task NorthStarCustomFunctionsHaveAllProviderTemplatesAndElapsedYearsExecutes()
    {
        var metadata = new MetaProvider(
            [new Entity("DateRanges",
            [
                new("StartDate", DataType.Datetime),
                new("EndDate", DataType.Datetime)
            ])],
            CustomFunctionDefintions.Functions);

        foreach (var functionName in NorthStarFunctions)
        {
            var function = metadata.GetFunction(functionName);
            Assert.That(function.SqlTemplates.Keys,
                Does.Contain("SqlServer").And.Contain("Sqlite").And.Contain("Postgres"),
                functionName);
        }

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seed = connection.CreateCommand())
        {
            seed.CommandText = """
                CREATE TABLE "DateRanges" (
                    "StartDate" TEXT NOT NULL,
                    "EndDate" TEXT NOT NULL
                );
                INSERT INTO "DateRanges" ("StartDate", "EndDate")
                VALUES ('2020-01-01', '2022-01-01');
                """;
            seed.ExecuteNonQuery();
        }

        using var context = new DynamicSqlContext(connection, metadata, new SqliteDialect());
        var result = await context.Query(new QueryDefinition
        {
            Entity = "DateRanges",
            Projection =
            [
                new(new FunctionFieldDefinition("ElapsedYears",
                [
                    new FieldDefinition("StartDate"),
                    new FieldDefinition("EndDate")
                ]), "ElapsedYears")
            ]
        }).ToListAsync();

        Assert.That(
            Convert.ToDouble(result.Single()["ElapsedYears"]),
            Is.EqualTo(731d / 365.2425d).Within(0.000001));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "scripts", "northstar-demo.sqlite.sql")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the NorthStar demo seed script.");
    }

    private static List<string> ReadReferenceQueries()
    {
        var markdown = File.ReadAllText(
            Path.Combine(RepositoryRoot, "scripts", "northstar-demo-queries.md"));
        return Regex.Matches(markdown, @"(?ms)^###\s+\d+\..*?```sql\s*(.*?)```")
            .Select(match => match.Groups[1].Value.Trim())
            .ToList();
    }

    private static int GetReferenceQueryIndex(string requestName)
    {
        var id = requestName.Split(' ', 2)[0];
        if (id.Length < 3 || !int.TryParse(id[2..], out var reportNumber))
            throw new InvalidDataException($"Saved request '{requestName}' has an invalid report ID.");

        var offset = id[..2] switch
        {
            "SA" => 0,
            "HC" => 5,
            "IN" => 10,
            "HR" => 15,
            _ => throw new InvalidDataException($"Saved request '{requestName}' has an unknown domain.")
        };
        var queryIndex = offset + reportNumber - 1;
        if (queryIndex is < 0 or >= 20)
            throw new InvalidDataException($"Saved request '{requestName}' has an invalid report number.");
        return queryIndex;
    }

    private static (string[] Columns, List<object?[]> Rows) ReadReferenceResult(
        SqliteConnection connection,
        string query)
    {
        using var command = connection.CreateCommand();
        command.CommandText = query;
        using var reader = command.ExecuteReader();
        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();
        List<object?[]> rows = [];
        while (reader.Read())
        {
            var row = new object?[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(row.Select(value => value is DBNull ? null : value).ToArray());
        }

        return (columns, rows);
    }

    private static bool AreEquivalentValues(object? actual, object? expected, string column)
    {
        if (actual is null || expected is null)
            return actual is null && expected is null;

        if (IsNumeric(expected) && IsNumeric(actual))
        {
            var actualNumber = Convert.ToDouble(actual, CultureInfo.InvariantCulture);
            var expectedNumber = Convert.ToDouble(expected, CultureInfo.InvariantCulture);
            return OneDecimalColumns.Contains(column)
                ? Math.Abs(Math.Round(actualNumber, 1, MidpointRounding.AwayFromZero) - expectedNumber) < 0.000001
                : Math.Abs(actualNumber - expectedNumber) <= 0.0051;
        }

        return string.Equals(
            Convert.ToString(actual, CultureInfo.InvariantCulture),
            Convert.ToString(expected, CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static bool IsNumeric(object? value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong or
            float or double or decimal;
}
#endif
