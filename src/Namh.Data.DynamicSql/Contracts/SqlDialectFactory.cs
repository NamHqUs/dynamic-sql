using System.Data;
using System.Globalization;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public static class SqlDialect
{
    internal static string FormatParameterValue(object? value)
        => value switch
        {
            null => "NULL",
            string text => $"'{text.Replace("'", "''", StringComparison.Ordinal)}'",
            bool boolean => boolean ? "1" : "0",
            DateTime dateTime => $"'{dateTime.ToString("O", CultureInfo.InvariantCulture)}'",
            DateTimeOffset dateTimeOffset => $"'{dateTimeOffset.ToString("O", CultureInfo.InvariantCulture)}'",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "NULL",
            _ => $"'{value}'"
        };

    public static ISqlDialect ForConnection(IDbConnection connection)
    {
        var connectionType = connection.GetType().FullName;

        if (connectionType?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            return new SqliteDialect();

        if (connectionType?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            return new PostgresDialect();

        if (connectionType?.Contains("FbConnection", StringComparison.OrdinalIgnoreCase) == true ||
            connectionType?.Contains("Firebird", StringComparison.OrdinalIgnoreCase) == true)
            return new FirebirdDialect();

        if (connectionType?.Contains("OracleConnection", StringComparison.OrdinalIgnoreCase) == true ||
            connectionType?.Contains("Oracle", StringComparison.OrdinalIgnoreCase) == true)
            return new OracleDialect();

        if (connectionType?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true ||
            connectionType?.Contains("Maria", StringComparison.OrdinalIgnoreCase) == true)
            return new MariaDialect();

        if (connectionType?.Contains("Hana", StringComparison.OrdinalIgnoreCase) == true ||
            connectionType?.Contains("Hdb", StringComparison.OrdinalIgnoreCase) == true)
            return new SapHanaDialect();

        return new SqlServerDialect();
    }
}
