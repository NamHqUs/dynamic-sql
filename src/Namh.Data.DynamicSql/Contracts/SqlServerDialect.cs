using System.Data;
using System.Globalization;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public sealed class SqlServerDialect : ISqlDialect
{
    public string Key => "SqlServer";

    public string QuoteIdentifier(string identifier)
        => $"[{identifier.Replace("]", "]]")}]";

    public string RenderTable(Entity entity)
        => entity.IsRawSql
            ? RawSqlEntitySqlGuard.ValidateAndReturn(entity.DbTable, RawSqlEntityDialect.SqlServer)
            : $"{QuoteIdentifier(entity.DbSchema ?? "dbo")}.{QuoteIdentifier(entity.DbTable ?? entity.Name)}";

    public string RenderBoolean(bool value) => value ? "1" : "0";
    public string RenderStringLiteral(string value) => $"N'{value.Replace("'", "''")}'";

    public string RenderStringConcatenation(string left, string right) => $"{left} + {right}";

    public string RenderTakePrefix(int count) => $"TOP {count} ";
    public string FormatParameterValue(object? value) => SqlDialect.FormatParameterValue(value);
    public string RenderCurrentDateTime() => "GETDATE()";

    public string RenderPaging(int? skip, int? take)
        => skip is null
            ? string.Empty
            : $" OFFSET {skip} ROWS" +
              (take is null ? string.Empty : $" FETCH NEXT {take} ROWS ONLY");

    public string RenderPaging(int? skip, int? take, bool hasOrderBy)
        => skip is null
            ? RenderPaging(skip, take)
            : (hasOrderBy ? string.Empty : " ORDER BY (SELECT 1)") +
              $" OFFSET {skip} ROWS" +
              (take is null ? string.Empty : $" FETCH NEXT {take} ROWS ONLY");

    public string RenderQueryWithParameters(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
    {
        if (parameters.Count == 0)
            return commandText;

        var declarations = parameters
            .Select(parameter =>
                $"{parameter.Name} {GetSqlServerType(parameter.Value)} = {FormatParameterValue(parameter.Value)}")
            .ToArray();
        var declarationText = declarations.Length == 1
            ? "DECLARE " + declarations[0] + ";"
            : "DECLARE " + declarations[0] + ",\n\t" +
                string.Join(",\n\t", declarations.Skip(1)) + ";";

        return declarationText + "\n" + commandText;
    }

    private static string GetSqlServerType(object? value)
        => value switch
        {
            null => "nvarchar(4000)",
            string => "nvarchar(4000)",
            bool => "bit",
            byte or short or int or long => "int",
            float or double => "float",
            decimal => "decimal(38, 18)",
            DateTime or DateTimeOffset => "datetime2",
            Guid => "uniqueidentifier",
            _ => "nvarchar(4000)"
        };
}

