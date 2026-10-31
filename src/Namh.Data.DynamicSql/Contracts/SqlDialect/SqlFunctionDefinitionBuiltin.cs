#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

internal static class SqlFunctionDefinitionBuiltin
{
    public static IEnumerable<SqlFunctionDefintion> AllFunctions
        => new SqlFunctionDefinitionBuiltinBase[]
            {
                new SqlServerFunctionDefinitionBuiltin(),
                new SqliteFunctionDefinitionBuiltin(),
                new PostgresFunctionDefinitionBuiltin(),
                new FirebirdFunctionDefinitionBuiltin(),
                new OracleFunctionDefinitionBuiltin()
            }
            .SelectMany(provider => provider.CreateAllFunctions())
            .GroupBy(function => function.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new SqlFunctionDefintion(
                group.First().Name,
                group.First().Arguments,
                group.First().ResultType,
                group.First().Description,
                group.SelectMany(function => function.SqlTemplates)
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                group.First().IsAggregate));
}
