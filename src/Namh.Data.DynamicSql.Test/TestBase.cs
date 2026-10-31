using Demo.Share;
using System.Data;

namespace Namh.Data.DynamicSql.Test;

[TestFixture]
public class TestBase 
{
    protected DynamicSqlContext _db = null!;
    protected ISqlDialect _sqlDialect = null!;
    protected IFunctionProvider _functionProvider = null!;
    private IDbConnection _connection = null!;

    [OneTimeSetUp]
    public void SetupOnce()
    {
        _connection = DefinedData.GetDbConnection(DbServerType.Sqlite);

        var entities = BuiltinMetadata.UnitTestData
            .Concat(BuiltinMetadata.SaleDemoData)
            .Concat(BuiltinMetadata.NorthStarDemoData);

        _sqlDialect = SqlDialect.ForConnection(_connection);
        IMetaProvider metaProvider = new MetaProvider(entities);
        _functionProvider = new FunctionProvider();
        _db = new DynamicSqlContext(_connection, metaProvider, _functionProvider, _sqlDialect);
    }


    [SetUp]
    public void Setup() { }

    [TearDown]
    public void TearDown()  { }

    [OneTimeTearDown]
    public void TearDownOnce()
    {
        _db.Dispose();
    }
}