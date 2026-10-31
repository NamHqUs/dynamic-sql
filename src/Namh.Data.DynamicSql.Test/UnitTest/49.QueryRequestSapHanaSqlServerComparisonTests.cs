using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Demo.Share;
using Microsoft.Data.SqlClient;
using Namh.Data.DynamicSql.Test.TestData;
using Sap.Data.Hana;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
internal sealed class QueryRequestSapHanaSqlServerComparisonTests
{
    private IDbConnection _hanaConnection = null!;
    private IDbConnection _sqlServerConnection = null!;
    private DynamicSqlContext _hanaDb = null!;
    private IReadOnlyDictionary<string, string> _sqlServerStatements = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            _hanaConnection = DefinedData.GetNorthStarSapHanaConnection();
            _sqlServerConnection = DefinedData.GetNorthStarSqlServerConnection();
        }
        catch (HanaException exception)
        {
            Assert.Ignore($"SAP HANA comparison requires the Docker SAP HANA service: {exception.Message}");
        }
        catch (SqlException exception)
        {
            Assert.Ignore($"SQL Server comparison requires the Docker SQL Server service: {exception.Message}");
        }

        _hanaDb = new DynamicSqlContext(
            _hanaConnection,
            new MetaProvider(BuiltinMetadata.UnitTestData.Concat(BuiltinMetadata.NorthStarDemoData)),
            new FunctionProvider(new SapHanaDialect()),
            new SapHanaDialect());
        _sqlServerStatements = LoadSqlServerStatements(Path.Combine(
            GetRepositoryRoot(),
            "scripts",
            "northstar-demo-queries.md"));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _hanaDb?.Dispose();
        _sqlServerConnection?.Dispose();
    }

    public static IEnumerable<TestCaseData> SavedQueryCases =>
        new[]
        {
            "Sales 1. Category and channel profitability.json",
            "Sales 2. Customer lifetime value and recency.json",
            "Sales 3. Sales representative performance.json",
            "Sales 4. Best-selling products and margin.json",
            "Sales 5. Monthly sales trend and momentum.json",
            "Health 1. Diagnosis burden by clinic.json",
            "Health 2. Physician workload and care-quality scorecard.json",
            "Health 3. High-risk patient care profile.json",
            "Health 4. Lab abnormality patterns.json",
            "Health 5. Visit-type utilization by month.json",
            "Insurance 1. Denial analysis by plan and reason.json",
            "Insurance 2. Plan profitability and medical loss ratio.json",
            "Insurance 3. Agent portfolio and premium collection.json",
            "Insurance 4. Claims adjudication by plan.json",
            "Insurance 5. Member utilization versus premium.json",
            "HR 1. Department compensation and performance.json",
            "HR 2. Manager span and team profile.json",
            "HR 3. Workforce composition by location.json",
            "HR 4. Leave demand and approval status.json",
            "HR 5. Performance and bonus trends.json"
        }
        .Select(fileName => new TestCaseData(fileName)
            .SetName(Path.GetFileNameWithoutExtension(fileName)));

    [TestCaseSource(nameof(SavedQueryCases))]
    public void SapHanaEngineResultsMatchSqlServerRawResults(string fileName)
    {
        var root = GetRepositoryRoot();
        var title = Path.GetFileNameWithoutExtension(fileName);
        title = title[(title.IndexOf(' ') + 1)..];
        var query = SavedQueryDefinitionLoader.Load(Path.Combine(
            root,
            "src",
            "Demo.WebApi",
            "FileStorage",
            "UserData",
            "123",
            "QueryRequests",
            fileName));

        var rawRecords = ExecuteSqlServer(_sqlServerStatements[title]);
        var generatedSql = _hanaDb.Query(query).ToSql().CommandText;
        var generatedRecords = _hanaDb.Query(query)
            .ToList()
            .Select(record => record.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase))
            .ToArray();

        Assert.That(generatedRecords.Length, Is.EqualTo(rawRecords.Count), title);
        foreach (var generatedRecord in generatedRecords)
        {
            Assert.That(
                rawRecords.Any(rawRecord =>
                    rawRecord.Keys.Count == generatedRecord.Keys.Count &&
                    rawRecord.Keys.All(key =>
                        generatedRecord.ContainsKey(key) &&
                        ValuesMatch(generatedRecord[key], rawRecord[key]))),
                Is.True,
                $"{title}\nGenerated SAP HANA SQL:\n{generatedSql}");
        }
    }

    private IReadOnlyDictionary<string, string> LoadSqlServerStatements(string path)
    {
        var markdown = File.ReadAllText(path);
        var statements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match section in Regex.Matches(
            markdown,
            @"^### (?<title>.+?)\r?\n(?<body>.*?)(?=^### |\z)",
            RegexOptions.Multiline | RegexOptions.Singleline))
        {
            var statement = Regex.Match(
                section.Groups["body"].Value,
                "=== \"SQL Server\".*?```sql\\s*(?<sql>.*?)\\s*```",
                RegexOptions.Singleline);
            if (!statement.Success)
                throw new InvalidDataException(
                    $"SQL Server SQL statement is missing for report [{section.Groups["title"].Value}].");
            statements.Add(
                section.Groups["title"].Value.Trim(),
                QualifyNorthStarTables(statement.Groups["sql"].Value));
        }

        Assert.That(statements, Has.Count.EqualTo(20));
        return statements;
    }

    private IReadOnlyList<Dictionary<string, object?>> ExecuteSqlServer(string sql)
    {
        using var command = _sqlServerConnection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var records = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var record = new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
                record.Add(
                    reader.GetName(index),
                    reader.IsDBNull(index) ? null : reader.GetValue(index));
            records.Add(record);
        }

        return records;
    }

    private static bool ValuesMatch(object? actual, object? expected)
    {
        if (actual is null || expected is null)
            return actual is null && expected is null;
        if (IsNumber(actual) && IsNumber(expected))
        {
            var difference = Math.Abs(
                Convert.ToDecimal(actual, CultureInfo.InvariantCulture) -
                Convert.ToDecimal(expected, CultureInfo.InvariantCulture));
            return difference <= 0.01m;
        }

        return string.Equals(
            Convert.ToString(actual, CultureInfo.InvariantCulture),
            Convert.ToString(expected, CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static bool IsNumber(object value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static string QualifyNorthStarTables(string sql)
        => sql
            .Replace("FROM Users ", "FROM [Main].[Users] ", StringComparison.Ordinal)
            .Replace("JOIN Users ", "JOIN [Main].[Users] ", StringComparison.Ordinal)
            .Replace("FROM Employees ", "FROM [Main].[Employees] ", StringComparison.Ordinal)
            .Replace("JOIN Employees ", "JOIN [Main].[Employees] ", StringComparison.Ordinal)
            .Replace("FROM Departments ", "FROM [hr].[Departments] ", StringComparison.Ordinal)
            .Replace("JOIN Departments ", "JOIN [hr].[Departments] ", StringComparison.Ordinal)
            .Replace("FROM PerformanceReviews", "FROM [hr].[PerformanceReviews]", StringComparison.Ordinal)
            .Replace("JOIN PerformanceReviews", "JOIN [hr].[PerformanceReviews]", StringComparison.Ordinal)
            .Replace("FROM LeaveRequests", "FROM [hr].[LeaveRequests]", StringComparison.Ordinal)
            .Replace("JOIN LeaveRequests", "JOIN [hr].[LeaveRequests]", StringComparison.Ordinal)
            .Replace("FROM Locations ", "FROM [Main].[Locations] ", StringComparison.Ordinal)
            .Replace("JOIN Locations ", "JOIN [Main].[Locations] ", StringComparison.Ordinal)
            .Replace("FROM Visits", "FROM [he].[Visits]", StringComparison.Ordinal)
            .Replace("JOIN Visits", "JOIN [he].[Visits]", StringComparison.Ordinal)
            .Replace("FROM Diagnoses ", "FROM [he].[Diagnoses] ", StringComparison.Ordinal)
            .Replace("JOIN Diagnoses ", "JOIN [he].[Diagnoses] ", StringComparison.Ordinal)
            .Replace("FROM LabResults", "FROM [he].[LabResults]", StringComparison.Ordinal)
            .Replace("JOIN LabResults", "JOIN [he].[LabResults]", StringComparison.Ordinal)
            .Replace("FROM Claims", "FROM [in].[Claims]", StringComparison.Ordinal)
            .Replace("JOIN Claims", "JOIN [in].[Claims]", StringComparison.Ordinal)
            .Replace("FROM Policies", "FROM [in].[Policies]", StringComparison.Ordinal)
            .Replace("JOIN Policies", "JOIN [in].[Policies]", StringComparison.Ordinal)
            .Replace("FROM PremiumPayments", "FROM [in].[PremiumPayments]", StringComparison.Ordinal)
            .Replace("JOIN PremiumPayments", "JOIN [in].[PremiumPayments]", StringComparison.Ordinal)
            .Replace("FROM InsurancePlans", "FROM [in].[InsurancePlans]", StringComparison.Ordinal)
            .Replace("JOIN InsurancePlans", "JOIN [in].[InsurancePlans]", StringComparison.Ordinal)
            .Replace("FROM Orders ", "FROM [sa].[Orders] ", StringComparison.Ordinal)
            .Replace("JOIN Orders ", "JOIN [sa].[Orders] ", StringComparison.Ordinal)
            .Replace("FROM OrderItems ", "FROM [sa].[OrderItems] ", StringComparison.Ordinal)
            .Replace("JOIN OrderItems ", "JOIN [sa].[OrderItems] ", StringComparison.Ordinal)
            .Replace("FROM Products ", "FROM [sa].[Products] ", StringComparison.Ordinal)
            .Replace("JOIN Products ", "JOIN [sa].[Products] ", StringComparison.Ordinal);

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;
}
