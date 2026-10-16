#pragma warning disable IDE0130
using static Namh.Data.DynamicSql.SqlFunctionDefintion;

namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

internal static class SqlFunctionDefinitionBuiltin
{
    public static IEnumerable<SqlFunctionDefintion> AllFunctions => Functions.Values;

    private static readonly IReadOnlyDictionary<string, SqlFunctionDefintion> Functions =
        new SqlFunctionDefintion[]
        {
            Create("GETDATE", [], DataType.Datetime,
                description: "Returns the current date and time.",
                sqlTemplates: new Dictionary<string, string>
                {
                    ["SqlServer"] = "GETDATE()",
                    ["Sqlite"] = "datetime('now', 'localtime')",
                    ["Postgres"] = "CURRENT_TIMESTAMP"
                }),
            
            CreateStandardSql("COUNT",
                [
                    new("value", DataType.Number, "*")
                ],
                DataType.Number,
                "COUNT({1})",
                "Counts rows or non-null values.",
                isAggregate: true),
            CreateStandardSql("SUM", [new("expression", DataType.Number)],
                DataType.Number, "SUM({1})", "Returns the sum of numeric values.", isAggregate: true),
            CreateStandardSql("AVG", [new("expression", DataType.Number)],
                DataType.Number, "AVG({1})", "Returns the average of numeric values.", isAggregate: true),
            CreateStandardSql("MAX", [new("expression", DataType.Number)],
                DataType.Number, "MAX({1})", "Returns the maximum value.", isAggregate: true),
            CreateStandardSql("MIN", [new("expression", DataType.Number)],
                DataType.Number, "MIN({1})", "Returns the minimum value.", isAggregate: true),
            CreateStandardSql("ROUND",
                [
                    new("value", DataType.Number),
                    new("digits", DataType.Number)
                ],
                DataType.Number, "ROUND({1}, {2})",
                "Rounds a number to the specified number of decimal places."),
            CreateStandardSql("NULLIF",
                [
                    new("value", DataType.Number),
                    new("otherValue", DataType.Number)
                ],
                DataType.Number, "NULLIF({1}, {2})",
                "Returns null when the two values are equal; otherwise returns the first value."),
            new(
                "DateFormat",
                [
                    new("value", DataType.Datetime),
                    new("format", DataType.String)
                ],
                DataType.String,
                sqlTemplates: new Dictionary<string, string>
                {
                    ["Sqlite"] = "STRFTIME({2}, {1})"
                },
                description: "Formats a date as year-month (%Y-%m) or weekday number (%w) on SQLite.")
        }.ToDictionary(function => function.Name, StringComparer.OrdinalIgnoreCase);

    private static SqlFunctionDefintion Create(
        string name,
        IReadOnlyList<SqlFunctionArgumentDefinition> arguments,
        DataType resultType,
        string description,
        IReadOnlyDictionary<string, string> sqlTemplates,
        bool isAggregate = false)
        => new(
            name,
            arguments,
            resultType,
            description,
            sqlTemplates,
            isAggregate);

    private static SqlFunctionDefintion CreateStandardSql(
        string name,
        IReadOnlyList<SqlFunctionArgumentDefinition> arguments,
        DataType resultType,
        string sqlTemplate,
        string description,
        bool isAggregate = false)
        => Create(name, arguments, resultType, description,
            new Dictionary<string, string>
            {
                ["SqlServer"] = sqlTemplate,
                ["Sqlite"] = sqlTemplate,
                ["Postgres"] = sqlTemplate
            },
            isAggregate);
}
