using Namh.Data.DynamicSql;
using static Namh.Data.DynamicSql.SqlFunctionDefintion;

namespace Demo.Share;

public class CustomFunctionDefintions
{
    public static SqlFunctionDefintion[] Functions { get; } = [
        new ("Day", resultType: DataType.Number,
                description: "Extracts the day part from a date.",
                arguments: [new("extractDate", DataType.Datetime)],
                sqlTemplates: new Dictionary<string, string>
                {
                    ["SqlServer"] = "DAY({1})",
                    ["Sqlite"] = "CAST(STRFTIME('%d', {1}) AS INTEGER)",
                    ["Postgres"] = "CAST(EXTRACT(DAY FROM {1}) AS INTEGER)"
                }),

        new ("Month", resultType: DataType.Number,
        description: "Extracts the month part from a date.",
        arguments: [new("extractDate", DataType.Datetime)],
        sqlTemplates: new Dictionary<string, string>
        {
            ["SqlServer"] = "MONTH({1})",
            ["Sqlite"] = "CAST(STRFTIME('%m', {1}) AS INTEGER)",
            ["Postgres"] = "CAST(EXTRACT(MONTH FROM {1}) AS INTEGER)"
        }),

        new ("Year", resultType: DataType.Number,
        description: "Extracts the year part from a date.",
        arguments: [new("extractDate", DataType.Datetime)],
        sqlTemplates: new Dictionary<string, string>
        {
            ["SqlServer"] = "YEAR({1})",
            ["Sqlite"] = "CAST(STRFTIME('%Y', {1}) AS INTEGER)",
            ["Postgres"] = "CAST(EXTRACT(YEAR FROM {1}) AS INTEGER)"
        }),


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

        CreateStandardFunction(
            "COUNT_DISTINCT",
            [new("expression", DataType.Number)],
            DataType.Number,
            "COUNT(DISTINCT {1})",
            "Counts distinct non-null values.",
            isAggregate: true),
        CreateStandardFunction(
            "CountDistinct",
            [new("expression", DataType.Number)],
            DataType.Number,
            "COUNT(DISTINCT {1})",
            "Counts distinct non-null values in a related collection.",
            isAggregate: true),
        CreateStandardFunction(
            "COUNT_IF",
            [new("predicate", DataType.Boolean)],
            DataType.Number,
            "COUNT(CASE WHEN {1} THEN 1 END)",
            "Counts rows that satisfy a predicate.",
            isAggregate: true),
        CreateStandardFunction(
            "CountIf",
            [new("predicate", DataType.Boolean)],
            DataType.Number,
            "COUNT(CASE WHEN {1} THEN 1 END)",
            "Counts related rows that satisfy a predicate.",
            isAggregate: true),
        CreateStandardFunction(
            "COUNT_DISTINCT_IF",
            [
                new("predicate", DataType.Boolean),
                new("expression", DataType.Number)
            ],
            DataType.Number,
            "COUNT(DISTINCT CASE WHEN {1} THEN {2} END)",
            "Counts distinct non-null values for rows that satisfy a predicate.",
            isAggregate: true),
        CreateStandardFunction(
            "CountDistinctIf",
            [
                new("predicate", DataType.Boolean),
                new("expression", DataType.Number)
            ],
            DataType.Number,
            "COUNT(DISTINCT CASE WHEN {1} THEN {2} END)",
            "Counts distinct related values for rows that satisfy a predicate.",
            isAggregate: true),
        CreateStandardFunction(
            "SUM_IF",
            [
                new("predicate", DataType.Boolean),
                new("expression", DataType.Number)
            ],
            DataType.Number,
            "SUM(CASE WHEN {1} THEN {2} END)",
            "Sums values for rows that satisfy a predicate.",
            isAggregate: true),
        CreateStandardFunction(
            "SumIf",
            [
                new("predicate", DataType.Boolean),
                new("expression", DataType.Number)
            ],
            DataType.Number,
            "SUM(CASE WHEN {1} THEN {2} END)",
            "Sums related values for rows that satisfy a predicate.",
            isAggregate: true),
        CreateStandardFunction(
            "AVG_IF",
            [
                new("predicate", DataType.Boolean),
                new("expression", DataType.Number)
            ],
            DataType.Number,
            "AVG(CASE WHEN {1} THEN {2} END)",
            "Averages values for rows that satisfy a predicate.",
            isAggregate: true),
        CreateStandardFunction(
            "AverageIf",
            [
                new("predicate", DataType.Boolean),
                new("expression", DataType.Number)
            ],
            DataType.Number,
            "AVG(CASE WHEN {1} THEN {2} END)",
            "Averages related values for rows that satisfy a predicate.",
            isAggregate: true),
        CreateStandardFunction(
            "MAX_IF",
            [
                new("predicate", DataType.Boolean),
                new("expression", DataType.Number)
            ],
            DataType.Number,
            "MAX(CASE WHEN {1} THEN {2} END)",
            "Returns the maximum value for rows that satisfy a predicate.",
            isAggregate: true),
        CreateStandardFunction(
            "MaxIf",
            [
                new("predicate", DataType.Boolean),
                new("expression", DataType.Datetime)
            ],
            DataType.Datetime,
            "MAX(CASE WHEN {1} THEN {2} END)",
            "Returns the maximum related date for rows that satisfy a predicate.",
            isAggregate: true),
        CreateStandardFunction(
            "COALESCE",
            [
                new("value", DataType.Number),
                new("fallback", DataType.Number)
            ],
            DataType.Number,
            "COALESCE({1}, {2})",
            "Returns the first non-null numeric value."),
        new(
            "CONCAT",
            [
                new("first", DataType.String),
                new("separator", DataType.String),
                new("last", DataType.String)
            ],
            DataType.String,
            "Concatenates three text values.",
            new Dictionary<string, string>
            {
                ["SqlServer"] = "CONCAT({1}, {2}, {3})",
                ["Sqlite"] = "({1} || {2} || {3})",
                ["Postgres"] = "({1} || {2} || {3})"
            }),
        CreateStandardFunction(
            "MonthKey",
            [new("value", DataType.Datetime)],
            DataType.String,
            "CONVERT(char(7), {1}, 126)",
            "Formats a date as a year-month key.",
            sqliteTemplate: "STRFTIME('%Y-%m', {1})",
            postgresTemplate: "TO_CHAR({1}, 'YYYY-MM')"),
        CreateStandardFunction(
            "AgeOn",
            [
                new("birthDate", DataType.Datetime),
                new("asOfDate", DataType.Datetime)
            ],
            DataType.Number,
            "(DATEDIFF(year, {1}, {2}) - CASE WHEN DATEPART(month, {2}) * 100 + DATEPART(day, {2}) < DATEPART(month, {1}) * 100 + DATEPART(day, {1}) THEN 1 ELSE 0 END)",
            "Returns completed years between a start date and an as-of date.",
            sqliteTemplate: "(CAST(STRFTIME('%Y', {2}) AS INTEGER) - CAST(STRFTIME('%Y', {1}) AS INTEGER) - CASE WHEN STRFTIME('%m%d', {2}) < STRFTIME('%m%d', {1}) THEN 1 ELSE 0 END)",
            postgresTemplate: "(EXTRACT(YEAR FROM {2}) - EXTRACT(YEAR FROM {1}) - CASE WHEN EXTRACT(MONTH FROM {2}) * 100 + EXTRACT(DAY FROM {2}) < EXTRACT(MONTH FROM {1}) * 100 + EXTRACT(DAY FROM {1}) THEN 1 ELSE 0 END)"),
        CreateStandardFunction(
            "ElapsedYears",
            [
                new("startDate", DataType.Datetime),
                new("endDate", DataType.Datetime)
            ],
            DataType.Number,
            "(DATEDIFF(day, {1}, {2}) / 365.2425)",
            "Returns fractional years elapsed between two dates.",
            sqliteTemplate: "((JULIANDAY({2}) - JULIANDAY({1})) / 365.2425)",
            postgresTemplate: "(EXTRACT(EPOCH FROM ({2} - {1})) / 31556952.0)"),
        CreateStandardFunction(
            "Difference",
            [
                new("left", DataType.Number),
                new("right", DataType.Number)
            ],
            DataType.Number,
            "({1} - {2})",
            "Returns the difference between two numeric values."),
        CreateStandardFunction(
            "LAG",
            [
                new("expression", DataType.Number),
                new("offset", DataType.Number),
                new("orderBy", DataType.String)
            ],
            DataType.Number,
            "LAG({1}, {2}) OVER (ORDER BY {3} ASC)",
            "Returns the previous row's value in ascending order."),
        CreateStandardFunction(
            "WINDOW_SUM",
            [
                new("expression", DataType.Number),
                new("orderBy", DataType.String)
            ],
            DataType.Number,
            "SUM({1}) OVER (ORDER BY {2} ASC ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)",
            "Returns a running total in ascending order."),
    ];

    private static SqlFunctionDefintion CreateDateDifference(
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

        return new SqlFunctionDefintion(
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

    private static SqlFunctionDefintion CreateDateAdjustment(
        string name,
        string datePart,
        string sqliteModifierUnit,
        string description)
    {
        return new SqlFunctionDefintion(
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

    private static SqlFunctionDefintion CreateStandardFunction(
        string name,
        IReadOnlyList<SqlFunctionArgumentDefinition> arguments,
        DataType resultType,
        string sqlServerTemplate,
        string description,
        bool isAggregate = false,
        string? sqliteTemplate = null,
        string? postgresTemplate = null)
        => new(
            name,
            arguments,
            resultType,
            description,
            new Dictionary<string, string>
            {
                ["SqlServer"] = sqlServerTemplate,
                ["Sqlite"] = sqliteTemplate ?? sqlServerTemplate,
                ["Postgres"] = postgresTemplate ?? sqlServerTemplate
            },
            isAggregate);
}
