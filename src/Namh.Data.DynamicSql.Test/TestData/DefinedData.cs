using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using System.Data;
using System.Text;

namespace Namh.Data.DynamicSql.Test.TestData;

public enum DbServerType
{
    Sqlite,
    SqlServer,
    Postgres
}
internal sealed class DefinedData 
{
    private static readonly object oLock = new();

    public static IDbConnection GetDbConnection(DbServerType dbServerType = DbServerType.Sqlite)
        => dbServerType switch
        {
            DbServerType.Sqlite => CreateSqliteDbConnection(),
            DbServerType.SqlServer => CreateSqlServerDbConnection(),
            DbServerType.Postgres => CreatePostgresDbConnection(),
            _ => throw new ArgumentOutOfRangeException(nameof(dbServerType), dbServerType, null)
        };

    private static IDbConnection CreateSqliteDbConnection()
    {
        var cn = new SqliteConnection("Data Source=:memory:");
        cn.Open();

        var isExisted = ExecuteScalar<long>(cn, 
            "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'dbo.User')");

        if (isExisted != 1)
        {
            StringBuilder sb = new("ATTACH DATABASE ':memory:' AS dbo;");
            sb.Append(LoadScript("db-scripts.sqlite.sql"));
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
            ExecuteNonQuery(cn, LoadScript("db-scripts.sql"));
        return cn;
    }

    private static IDbConnection CreatePostgresDbConnection()
    {
        var cn = new NpgsqlConnection(
            "Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=NoPassword123!");
        cn.Open();

        lock (oLock)
        {
            var isExisted = ExecuteScalar<bool>(cn,
                "SELECT EXISTS (SELECT 1 FROM information_schema.tables " +
                "WHERE table_schema = 'public' AND table_name = 'User');");

            if (!isExisted)
                ExecuteNonQuery(cn, LoadScript("db-scripts.postgres.sql"));
        }

        return cn;
    }

    private static string LoadScript(string fileName)
        => File.ReadAllText(fileName);
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
