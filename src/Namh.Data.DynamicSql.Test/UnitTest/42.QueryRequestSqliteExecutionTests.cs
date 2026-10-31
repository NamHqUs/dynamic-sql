using System.Data;
using Demo.Share;
using Namh.Data.DynamicSql.Test.TestData;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
internal sealed class QueryRequestSqliteExecutionTests
{
    private static readonly string[] LegacyRequests =
    [
        "Sales 1. Category and channel profitability.json",
        "Test-Check Context Calculation Fields.json",
        "Test-Multiple Contexts.json",
        "Test-Query directly on REF CONTEXT.json",
        "Shared Context.json"
    ];

    private IDbConnection _connection = null!;
    private DynamicSqlContext _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        _connection = DefinedData.GetNorthStarSqliteDockerConnection();
        var metadata = BuiltinMetadata.UnitTestData
            .Concat(BuiltinMetadata.NorthStarDemoData);
        _db = new DynamicSqlContext(
            _connection,
            new MetaProvider(metadata),
            new FunctionProvider(new SqliteDialect()),
            SqlDialect.ForConnection(_connection));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _db?.Dispose();
    }

    [Test]
    public void AllNormalSavedQueryDefinitionsExecuteOnSqlite()
    {
        var root = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "Demo.WebApi",
            "FileStorage",
            "UserData",
            "123",
            "QueryRequests");

        var requests = Directory
            .EnumerateFiles(root, "*.json")
            .Where(path => !LegacyRequests.Contains(Path.GetFileName(path), StringComparer.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.That(requests, Has.Count.EqualTo(25));
        foreach (var requestPath in requests)
        {
            var query = SavedQueryDefinitionLoader.Load(requestPath);
            if (Path.GetFileName(requestPath)
                .Equals("Sample-Recursive employee hierarchy.json", StringComparison.Ordinal))
            {
                Assert.That(
                    () => _db.Query(query).ToList(),
                    Throws.TypeOf<NotSupportedException>()
                        .Or.TypeOf<RelationNotFoundException>(),
                    Path.GetFileName(requestPath));
                continue;
            }

            Assert.That(
                () => _db.Query(query).ToList(),
                Throws.Nothing,
                Path.GetFileName(requestPath));
        }
    }

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;

}
