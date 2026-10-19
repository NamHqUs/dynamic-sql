namespace Namh.Data.DynamicSql.Contracts.QueryDefinitions;

public record YamlQueryDefinition()
{
    public string? Name { get; set; }
    public string? Entity { get; set; }
    public List<string?>? EntityAliases { get; set; }
    public List<string?>? Fields { get; set; }
    public List<List<string>>? ProjectionRows { get; set; }
    public string? Filter { get; set; }
    public string? Sorts { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
