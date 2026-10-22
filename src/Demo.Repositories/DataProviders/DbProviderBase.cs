using System.Data;

namespace Demo.Repositories.DataProviders;

public abstract class DbProviderBase(string[] scriptFiles)
{
    private static readonly object oLock = new();
    private static bool _initialized = false;

    protected abstract string? SqlScriptCheckIfDbExists { get; }
    protected abstract string SqlGetTableNames { get; }
    protected virtual string IdentifierQuotePrefix => "\"";
    protected virtual string IdentifierQuoteSuffix => "\"";

    public virtual IDbConnection GetDbConnection()
    {
        var cn = CreateDbConnection();
        lock (oLock)
        {
            if (!_initialized  
                && (string.IsNullOrEmpty(SqlScriptCheckIfDbExists) 
                        || ExecuteScalar<long>(cn, SqlScriptCheckIfDbExists) != 1))
                    InitializeDb(cn);

            _initialized = true;
        }
        return cn;
    }

    public string[] GetTableNames()
    {
        using var connection = GetDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = SqlGetTableNames;
        using var reader = command.ExecuteReader();
        var tableNames = new List<string>();
        while (reader.Read())
            tableNames.Add(reader.GetString(0));

        return tableNames.ToArray();
    }

    public string QuoteIdentifier(string identifier)
    {
        var escapedIdentifier = identifier.Replace(IdentifierQuoteSuffix, IdentifierQuoteSuffix + IdentifierQuoteSuffix);
        return $"{IdentifierQuotePrefix}{escapedIdentifier}{IdentifierQuoteSuffix}";
    }

    protected abstract IDbConnection CreateDbConnection();
    private void InitializeDb(IDbConnection cn)
    {
        foreach (var fileName in scriptFiles)
        {
            var filePath = Path.Combine(Environment.CurrentDirectory, fileName);
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
