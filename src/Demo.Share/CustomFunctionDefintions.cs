using Namh.Data.DynamicSql;

namespace Demo.Share;

public class CustomFunctionDefintions
{
    public static FunctionDefinition[] Functions { get; } = [
        new ("Day", resultType: DataType.Number,
                description: "Extracts the day part from a date.",
                arguments: [new("extractDate", DataType.Datetime)],
                sqlTemplates: new Dictionary<string, string>
                {
                    ["SqlServer"] = "DAY({1})",
                    ["Sqlite"] = "STRFTIME('%d', {1})",
                    ["Postgres"] = "CAST(EXTRACT(DAY FROM {1}) AS INTEGER)"
                }),

        new ("Month", resultType: DataType.Number,
        description: "Extracts the month part from a date.",
        arguments: [new("extractDate", DataType.Datetime)],
        sqlTemplates: new Dictionary<string, string>
        {
            ["SqlServer"] = "MONTH({1})",
            ["Sqlite"] = "STRFTIME('%m', {1})",
            ["Postgres"] = "CAST(EXTRACT(MONTH FROM {1}) AS INTEGER)"
        }),

        new ("Year", resultType: DataType.Number,
        description: "Extracts the year part from a date.",
        arguments: [new("extractDate", DataType.Datetime)],
        sqlTemplates: new Dictionary<string, string>
        {
            ["SqlServer"] = "YEAR({1})",
            ["Sqlite"] = "STRFTIME('%Y', {1})",
            ["Postgres"] = "CAST(EXTRACT(YEAR FROM {1}) AS INTEGER)"
        }),

        new (
            "Average",
            [new("number", DataType.Number)],
            DataType.Number,
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = "AVG({1})",
                ["Sqlite"] = "AVG({1})",
                ["Postgres"] = "AVG({1})"
            },
            description: "Returns the average of numeric values.",
            isAggregate: true),

        CreateDateDifference("DateSub", "day",
            "Returns the number of elapsed whole days from startDate to endDate."),
        CreateDateAdjustment("DateAdd", "day", "days",
            "Adds the specified number of days to a date or date-time."),

        CreateDateDifference("MonthSub", "month",
            "Returns the number of calendar-month boundaries from startDate to endDate."),
        CreateDateAdjustment("MonthAdd", "month", "months",
            "Adds the specified number of months to a date or date-time."),

        CreateDateDifference("YearSub", "year",
            "Returns the number of calendar-year boundaries from startDate to endDate."),
        CreateDateAdjustment("YearAdd", "year", "years",
            "Adds the specified number of years to a date or date-time."),
    ];

    private static FunctionDefinition CreateDateDifference(
        string name,
        string datePart,
        string description)
    {
        var sqliteTemplate = datePart == "month"
            ? "((CAST(strftime('%Y', date({2})) AS INTEGER) - CAST(strftime('%Y', date({1})) AS INTEGER)) * 12 + CAST(strftime('%m', date({2})) AS INTEGER) - CAST(strftime('%m', date({1})) AS INTEGER))"
            : datePart == "day"
                ? "CAST(julianday(date({2})) - julianday(date({1})) AS INTEGER)"
                : "(CAST(strftime('%Y', date({2})) AS INTEGER) - CAST(strftime('%Y', date({1})) AS INTEGER))";
        var postgresTemplate = datePart == "month"
            ? "((EXTRACT(YEAR FROM {2}) - EXTRACT(YEAR FROM {1})) * 12 + EXTRACT(MONTH FROM {2}) - EXTRACT(MONTH FROM {1}))"
            : datePart == "day"
                ? "(CAST({2} AS DATE) - CAST({1} AS DATE))"
                : "(EXTRACT(YEAR FROM {2}) - EXTRACT(YEAR FROM {1}))";

        return new FunctionDefinition(
            name,
            [
                new("startDate", DataType.Datetime),
                new("endDate", DataType.Datetime)
            ],
            DataType.Number,
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
            DataType.Datetime,
            description,
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = $"DATEADD({datePart}, {{2}}, {{1}})",
                ["Sqlite"] = $"datetime({{1}}, printf('%+d {sqliteModifierUnit}', {{2}}))",
                ["Postgres"] = $"({{1}} + ({{2}} * INTERVAL '1 {datePart}'))"
            });
    }

}
