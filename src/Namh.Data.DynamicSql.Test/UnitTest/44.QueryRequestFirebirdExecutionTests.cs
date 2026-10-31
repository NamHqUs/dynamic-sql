using System.Data;
using Demo.Share;
using FirebirdSql.Data.FirebirdClient;
using Namh.Data.DynamicSql.Test.TestData;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
internal sealed class QueryRequestFirebirdExecutionTests
{
    private IDbConnection _connection = null!;
    private DynamicSqlContext _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            _connection = DefinedData.GetNorthStarFirebirdConnection();
        }
        catch (FbException exception)
        {
            Assert.Ignore($"Firebird execution requires the Docker Firebird service: {exception.Message}");
        }

        var metadata = BuiltinMetadata.UnitTestData
            .Concat(BuiltinMetadata.NorthStarDemoData);
        _db = new DynamicSqlContext(
            _connection,
            new MetaProvider(metadata),
            new FunctionProvider(CustomFunctionDefintions.Functions),
            SqlDialect.ForConnection(_connection));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _db?.Dispose();
    }

    [Test]
    public void AllNormalSavedQueryDefinitionsExecuteOnFirebird()
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
            .Where(path => !Path.GetFileName(path).Equals(
                "Sales 1. Category and channel profitability.json",
                StringComparison.Ordinal))
            .Where(path => !Path.GetFileName(path).Equals(
                "Check Context Calculation Fields.json",
                StringComparison.Ordinal))
            .Where(path => !Path.GetFileName(path).Equals(
                "Multiple Contexts.json",
                StringComparison.Ordinal))
            .Where(path => !Path.GetFileName(path).Equals(
                "Query directly on REF CONTEXT.json",
                StringComparison.Ordinal))
            .Where(path => !Path.GetFileName(path).Equals(
                "Shared Context.json",
                StringComparison.Ordinal))
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
