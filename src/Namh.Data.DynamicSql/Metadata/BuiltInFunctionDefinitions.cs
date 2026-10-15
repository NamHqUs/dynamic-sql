#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

internal static class BuiltInFunctionDefinitions
{
    private static readonly IReadOnlyDictionary<string, FunctionDefinition> Functions =
        new FunctionDefinition[]
        {
            Create("GETDATE", [], typeof(DateTime), "CURRENT_TIMESTAMP",
                description: "Returns the current date and time.",
                sqlTemplates: new Dictionary<string, string>
                {
                    ["SqlServer"] = "GETDATE()",
                    ["Sqlite"] = "datetime('now', 'localtime')"
                }),
            new ("Day", resultType: typeof(string),
                description: "Extracts the day part from a date.", 
                arguments: [new("extractDate", DataType.Datetime)],
                sqlTemplates: new Dictionary<string, string>
                {
                    ["SqlServer"] = "DAY({1})",
                    ["Sqlite"] = "STRFTIME('%d', {1})"
                }),

             new ("Month", resultType: typeof(string),
                description: "Extracts the month part from a date.", 
                arguments: [new("extractDate", DataType.Datetime)],
                sqlTemplates: new Dictionary<string, string>
                {
                    ["SqlServer"] = "MONTH({1})",
                    ["Sqlite"] = "STRFTIME('%m', {1})"
                }),
             new ("Year", resultType: typeof(string),
                description: "Extracts the year part from a date.", 
                arguments: [new("extractDate", DataType.Datetime)],
                sqlTemplates: new Dictionary<string, string>
                {
                    ["SqlServer"] = "YEAR({1})",
                    ["Sqlite"] = "STRFTIME('%Y', {1})"
                }),
            new(
                "DateSub",
                [
                    new("startDate", DataType.Datetime),
                    new("endDate", DataType.Datetime)
                ],
                typeof(int),
                sqlTemplates: new Dictionary<string, string>
                {
                    ["SqlServer"] = "DATEDIFF(day, {1}, {2})",
                    ["Sqlite"] = "CAST(julianday(date({2})) - julianday(date({1})) AS INTEGER)",
                    ["Postgres"] = "CAST({2} AS DATE) - CAST({1} AS DATE)"
                },
                description: "Returns the number of whole days from startDate to endDate."),
            CreateDateDifference("MonthSub", "month",
                "Returns the number of calendar-month boundaries from startDate to endDate."),
            CreateDateDifference("YearSub", "year",
                "Returns the number of calendar-year boundaries from startDate to endDate."),
            CreateDateAdjustment("DateAdd", "day", "days",
                "Adds the specified number of days to a date or date-time."),
            CreateDateAdjustment("MonthAdd", "month", "months",
                "Adds the specified number of months to a date or date-time."),
            CreateDateAdjustment("YearAdd", "year", "years",
                "Adds the specified number of years to a date or date-time."),
            Create("COUNT",
                [
                    new("value", DataType.Number, "*")
                ],
                typeof(long),
                "COUNT({1})",
                isAggregate: true,
                description: "Counts rows or non-null values."),
            Create("SUM", [new("expression", DataType.Number)],
                typeof(decimal), "SUM({1})", isAggregate: true,
                description: "Returns the sum of numeric values."),
            Create("AVG", [new("expression", DataType.Number)],
                typeof(decimal), "AVG({1})", isAggregate: true,
                description: "Returns the average of numeric values."),
            Create("AVERAGE", [new("expression", DataType.Number)],
                typeof(decimal), "AVG({1})", isAggregate: true,
                description: "Returns the average of numeric values."),
            Create("MAX", [new("expression", DataType.Number)],
                typeof(decimal), "MAX({1})", isAggregate: true,
                description: "Returns the maximum value."),
            Create("MIN", [new("expression", DataType.Number)],
                typeof(decimal), "MIN({1})", isAggregate: true,
                description: "Returns the minimum value."),
            Create("ROUND",
                [
                    new("value", DataType.Number),
                    new("digits", DataType.Number)
                ],
                typeof(decimal), "ROUND({1}, {2})",
                description: "Rounds a number to the specified number of decimal places."),
            Create("NULLIF",
                [
                    new("value", DataType.Number),
                    new("otherValue", DataType.Number)
                ],
                typeof(decimal), "NULLIF({1}, {2})",
                description: "Returns null when the two values are equal; otherwise returns the first value."),
            new(
                "DateFormat",
                [
                    new("value", DataType.Datetime),
                    new("format", DataType.String)
                ],
                typeof(string),
                sqlTemplates: new Dictionary<string, string>
                {
                    ["Sqlite"] = "STRFTIME({2}, {1})"
                },
                description: "Formats a date as year-month (%Y-%m) or weekday number (%w) on SQLite.")
        }.ToDictionary(function => function.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, SqlTypeDefinition> SqlTypes =
        new SqlTypeDefinition[]
        {
            new("Integer", new Dictionary<string, string>
            {
                ["SqlServer"] = "int",
                ["Postgres"] = "integer"
            }, defaultSqlType: "INTEGER"),
            new("Decimal", new Dictionary<string, string>
            {
                ["SqlServer"] = "decimal(38,18)",
                ["Postgres"] = "numeric"
            }, defaultSqlType: "NUMERIC"),
            new("String", new Dictionary<string, string>
            {
                ["SqlServer"] = "nvarchar(max)",
                ["Postgres"] = "text"
            }, defaultSqlType: "TEXT")
        }.ToDictionary(type => type.Name, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<FunctionDefinition> AllFunctions => Functions.Values;
    public static IEnumerable<SqlTypeDefinition> AllSqlTypes => SqlTypes.Values;

    public static FunctionDefinition GetFunction(string name)
        => !string.IsNullOrWhiteSpace(name) && Functions.TryGetValue(name, out var function)
            ? function
            : throw new FormatException($"Unsupported function or argument count [{name}].");

    public static bool TryGetFunction(string name, out FunctionDefinition? function)
    {
        if (!string.IsNullOrWhiteSpace(name) && Functions.TryGetValue(name, out var found))
        {
            function = found;
            return true;
        }

        function = null;
        return false;
    }

    public static SqlTypeDefinition GetSqlType(string name)
        => !string.IsNullOrWhiteSpace(name) && SqlTypes.TryGetValue(name, out var sqlType)
            ? sqlType
            : throw new FormatException($"Unsupported logical SQL type [{name}].");

    private static FunctionDefinition Create(
        string name,
        IReadOnlyList<FunctionArgumentDefinition> arguments,
        Type resultType,
        string defaultSqlTemplate,
        string description,
        bool isAggregate = false,
        IReadOnlyDictionary<string, string>? sqlTemplates = null)
        => new(
            name,
            arguments,
            resultType,
            description,
            defaultSqlTemplate,
            sqlTemplates,
            isAggregate);

    private static FunctionDefinition CreateDateDifference(
        string name,
        string datePart,
        string description)
    {
        var sqliteTemplate = datePart == "month"
            ? "((CAST(strftime('%Y', date({2})) AS INTEGER) - CAST(strftime('%Y', date({1})) AS INTEGER)) * 12 + CAST(strftime('%m', date({2})) AS INTEGER) - CAST(strftime('%m', date({1})) AS INTEGER))"
            : "(CAST(strftime('%Y', date({2})) AS INTEGER) - CAST(strftime('%Y', date({1})) AS INTEGER))";
        var postgresTemplate = datePart == "month"
            ? $"((EXTRACT(YEAR FROM {2}) - EXTRACT(YEAR FROM {1})) * 12 + EXTRACT(MONTH FROM {2}) - EXTRACT(MONTH FROM {1}))"
            : $"(EXTRACT(YEAR FROM {2}) - EXTRACT(YEAR FROM {1}))";

        return new FunctionDefinition(
            name,
            [
                new("startDate", DataType.Datetime),
                new("endDate", DataType.Datetime)
            ],
            typeof(int),
            description,
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = $"DATEDIFF({datePart}, {{1}}, {{2}})",
                ["Sqlite"] = sqliteTemplate,
                ["Postgres"] = postgresTemplate
            });
    }

    private static FunctionDefinition CreateDateAdjustment(
        string name,
        string datePart,
        string sqliteModifierUnit,
        string description)
    {
        return new FunctionDefinition(
            name,
            [
                new("value", DataType.Datetime),
                new("amount", DataType.Number)
            ],
            typeof(DateTime),
            description,
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = $"DATEADD({datePart}, {{2}}, {{1}})",
                ["Sqlite"] = $"datetime({{1}}, printf('%+d {sqliteModifierUnit}', {{2}}))",
                ["Postgres"] = $"({{1}} + ({{2}} * INTERVAL '1 {datePart}'))"
            });
    }
}
