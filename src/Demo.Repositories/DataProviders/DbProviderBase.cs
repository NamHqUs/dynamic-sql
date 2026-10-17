using System.Data;

namespace Demo.Repositories.DataProviders;

public abstract class DbProviderBase
{
    private static readonly object oLock = new();
    private static bool _initialized = false;

    protected abstract string? SqlScriptCheckIfDbExists { get; }
    protected abstract string[] InitializeDbScriptFileNames { get; } 

    public virtual IDbConnection GetDbConnection()
    {
        var cn = CreateDbConnection();
        lock (oLock)
        {
            if (!_initialized && InitializeDbScriptFileNames != null)
            {
                if  (string.IsNullOrEmpty(SqlScriptCheckIfDbExists) 
                        || ExecuteScalar<long>(cn, SqlScriptCheckIfDbExists) != 1)
                    InitializeDb(cn);
            }

            _initialized = true;
        }
        return cn;
    }

    protected abstract IDbConnection CreateDbConnection();
    private void InitializeDb(IDbConnection cn)
    {
        foreach (var fileName in InitializeDbScriptFileNames)
        {
            var filePath = Path.Combine(Environment.CurrentDirectory, "DataStore", "DbScripts", fileName);
            var scripts = File.ReadAllText(filePath);
            ExecuteNonQuery(cn, scripts);
        }
    }

    private static T ExecuteScalar<T>(IDbConnection cn, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;

        if (cn.State != ConnectionState.Open)
            cn.Open();
        return (T)Convert.ChangeType(cmd.ExecuteScalar()!, typeof(T));
    }
    private static void ExecuteNonQuery(IDbConnection cn, string sql)
    {
        using var cmd = cn.CreateCommand();
        if (cn.State != ConnectionState.Open)
            cn.Open();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
