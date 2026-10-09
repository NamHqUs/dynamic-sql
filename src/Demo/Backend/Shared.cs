#!
#:package Microsoft.EntityFrameworkCore.Sqlite@10.0.12
using System.Data;
using Microsoft.Data.Sqlite;

public record TableDataDto(string Name, string Columns, string[] Rows);

public static class DbConnection
{
    public static IDbConnection Value => CreateDbConnection();
    
    private static IDbConnection CreateDbConnection()
    {
        var cn = new SqliteConnection("Data Source=:memory:");
        cn.Open();

        var isExisted = ExecuteScalar<long>(cn, 
            "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Customers')");

        if (isExisted != 1)
        {
            var scripts = File.ReadAllText("data/db-scripts.sqlite.sql");
            ExecuteNonQuery(cn, scripts);
        }

        return cn;
    }
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