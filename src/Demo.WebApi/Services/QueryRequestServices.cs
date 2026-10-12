using Namh.Data.DynamicSql;
using System.Text.Json;

namespace Demo.WebApi.Services;

public static class QueryRequestServices
{
    private static readonly string _StorePath = Path.Combine(Environment.CurrentDirectory, "DataStore", "QueryRequests");
    public static void SaveQueryRequest(QueryRequest query)
    {
        var json = JsonSerializer.Serialize(query, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter<DataType>() }
        });
        var fileName = $"query_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        var filePath = Path.Combine(_StorePath, fileName);
        File.WriteAllText(filePath, json);
        Console.WriteLine($"Query request saved to {filePath}");
    }

    public static void LoadQueryRequest(string fileName)
    {
        var filePath = Path.Combine(_StorePath , fileName);
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File {filePath} does not exist.");
            return;
        }
        var json = File.ReadAllText(filePath);
        var query = JsonSerializer.Deserialize<QueryRequest>(json, new JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter<DataType>() }
        });
        if (query is not null)
        {
            Console.WriteLine($"Loaded query request from {filePath}:\n{query}");
        }
    }
}
