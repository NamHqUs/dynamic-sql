using System.Data;
using Demo.Share;
using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
using Namh.Data.DynamicSql.Test.TestData;
using Oracle.ManagedDataAccess.Client;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
internal sealed class QueryRequestOracleExecutionTests
{
    private IDbConnection _connection = null!;
    private DynamicSqlContext _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            _connection = DefinedData.GetNorthStarOracleConnection();
        }
        catch (OracleException exception)
        {
            Assert.Ignore($"Oracle execution requires the Docker Oracle service: {exception.Message}");
        }

        _db = new DynamicSqlContext(
            _connection,
            new MetaProvider(BuiltinMetadata.UnitTestData.Concat(BuiltinMetadata.NorthStarDemoData)),
            new FunctionProvider(CustomFunctionDefintions.Functions),
            SqlDialect.ForConnection(_connection));
    }

    [OneTimeTearDown]
    public void TearDown() => _db?.Dispose();

    [Test]
    public void AllNormalSavedQueryDefinitionsExecuteOnOracle()
    {
        var root = Path.Combine(GetRepositoryRoot(), "src", "Demo.WebApi", "FileStorage", "UserData", "123", "QueryRequests");
        var requests = Directory.EnumerateFiles(root, "*.json")
            .Where(path => !new[]
            {
                "Category and channel profitability.json",
                "Check Context Calculation Fields.json",
                "Multiple Contexts.json",
                "Query directly on REF CONTEXT.json",
                "Shared Context.json"
            }.Contains(Path.GetFileName(path), StringComparer.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.That(requests, Has.Count.EqualTo(25));
        foreach (var requestPath in requests)
        {
            var query = QueryDefinitionParser.Parse(File.ReadAllText(requestPath));
            var fileName = Path.GetFileName(requestPath);
            if (fileName.Equals("Sample-Recursive employee hierarchy.json", StringComparison.Ordinal))
            {
                Assert.That(() => _db.Query(query).ToList(), Throws.TypeOf<NotSupportedException>(), fileName);
                continue;
            }

            Assert.That(() => _db.Query(query).ToList(), Throws.Nothing, fileName);
        }
    }

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;
}
