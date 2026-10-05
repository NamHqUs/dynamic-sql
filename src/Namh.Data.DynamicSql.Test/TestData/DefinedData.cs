using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using NUnit.Framework.Internal;
using System.Data;
using System.Text;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Namh.Data.DynamicSql.Test.TestData;

public enum DbServerType
{
    Sqlite,
    SqlServer
}
internal sealed class DefinedData 
{
    public static IDbConnection GetDbConnection(DbServerType dbServerType = DbServerType.Sqlite)
        => dbServerType == DbServerType.Sqlite ? CreateSqliteDbConnection() : CreateSqlServerDbConnection();

    private static IDbConnection CreateSqliteDbConnection()
    {
        var cn = new SqliteConnection("Data Source=:memory:");
        cn.Open();

        var isExisted = ExecuteScalar<long>(cn, 
            "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'dbo.User')");

        if (isExisted != 1)
        {
            StringBuilder sb = new("ATTACH DATABASE ':memory:' AS dbo;");
            sb.Append(LoadDdScripts());
            ExecuteNonQuery(cn, sb.ToString());
        }

        return cn;
    }

    private static IDbConnection CreateSqlServerDbConnection()
    {
        var cn = new SqlConnection("Server=localhost;Database=DynamicSqlTest;User Id=sa;Password=NoPassword123!;TrustServerCertificate=True;");
        cn.Open();

        var isExisted = ExecuteScalar<int>(cn, "SELECT IIF(OBJECT_ID('dbo.[User]', 'U') IS NOT NULL, 1, 0);");
        if(isExisted != 1)
            ExecuteNonQuery(cn, LoadDdScripts());
        return cn;
    }

    private static string LoadDdScripts()
        => File.ReadAllText("db-scripts.sql");
    private static void ExecuteNonQuery(IDbConnection cn, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static T ExecuteScalar<T>(IDbConnection cn, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        return (T)cmd.ExecuteScalar()!;
    }
}
