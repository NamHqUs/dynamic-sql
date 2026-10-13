using Namh.Data.DynamicSql;
using System.Text.Json;

namespace Demo.WebApi.Services;

public static partial class MetadataServices
{
    static class MetadataFilesManager
    {
        public readonly static string _metadataFilePath = Path.Combine(Environment.CurrentDirectory, "DataStore", "Metadata");

        public static List<Entity> LoadAllMetadataFromFiles()
        {
            if (!Directory.Exists(_metadataFilePath))
                return [];

            var metadataFiles = Directory.GetFiles(_metadataFilePath, "*.json");

            return metadataFiles.Select(file =>
            {
                var json = File.ReadAllText(file);
                return JsonSerializer.Deserialize<Entity>(json) ?? throw new InvalidOperationException($"Failed to deserialize metadata from file '{file}'.");
            }).ToList();
        }

        public static Entity LoadMetadataFromFile(string entityName)
        {
            var filePath = Path.Combine(_metadataFilePath, $"{entityName}.json");
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Metadata file for entity '{entityName}' not found.");
            var json = File.ReadAllText(filePath);

            return JsonSerializer.Deserialize<Entity>(json) ?? throw new InvalidOperationException($"Failed to deserialize metadata for entity '{entityName}'.");
        }

        public static void SaveMetadataToFile(Entity entity)
        {
            if (!Directory.Exists(_metadataFilePath))
                Directory.CreateDirectory(_metadataFilePath);
            var filePath = Path.Combine(_metadataFilePath, $"{entity.Name}.json");

            File.WriteAllText(filePath, JsonSerializer.Serialize(entity, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static void DeleteMetadataFile(string entityName)
        {
            var filePath = Path.Combine(_metadataFilePath, $"{entityName}.json");
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }


}
