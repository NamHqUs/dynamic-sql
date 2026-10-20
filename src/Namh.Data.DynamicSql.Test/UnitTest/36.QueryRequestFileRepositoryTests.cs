#if NET10_0
using Demo.Repositories.FileStorage;
using Microsoft.Extensions.Configuration;
using Namh.Data.DynamicSql;
using System.Text.Json;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public sealed class _36_QueryRequestFileRepositoryTests
{
    [Test]
    public void GetBuiltinNormalizesLegacyProjectionAndFilterGroups()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"dynamic-sql-{Guid.NewGuid():N}");
        var queryRequestsPath = Path.Combine(storagePath, "QueryRequests");
        Directory.CreateDirectory(queryRequestsPath);

        try
        {
            File.WriteAllText(Path.Combine(queryRequestsPath, "legacy.json"), """
                {
                  "name": "legacy",
                  "projection": [
                    [{ "field": { "kind": "field", "field": "Id" }, "alias": null }],
                    [{ "field": { "kind": "field", "field": "Name" }, "alias": null }]
                  ],
                  "filters": [{
                    "logicalOperation": 0,
                    "filters": [{
                      "logicalOperator": 0,
                      "left": { "kind": "field", "field": "Id" },
                      "operation": 0,
                      "right": { "kind": "value", "value": 1 },
                      "parameterized": true
                    }]
                  }],
                  "sorts": null,
                  "page": null,
                  "pageSize": null,
                  "filterPlanText": "Name = <Alice Johnson> AND Id = <1>",
                  "isBuiltin": true,
                  "entity": "User",
                  "EntityAliases": [{ "Alias": "Manager2", "RelationPath": "Manager", "RelatedKeys": [] }]
                }
                """);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileStoragePath"] = storagePath
                })
                .Build();
            var repository = new QueryRequestFileRepository(new FilePathResolver(configuration));

            var queryRequest = repository.GetBuiltin().Single();

            Assert.That(queryRequest.Projection, Has.Count.EqualTo(3));
            Assert.That(queryRequest.Projection[1].IsNewLine, Is.True);
            Assert.That(queryRequest.Filters, Is.TypeOf<FilterDefinition>());
            Assert.That(queryRequest.Filters!.Filters, Has.Count.EqualTo(1));
            Assert.That(queryRequest.Filters.Filters[0], Is.TypeOf<FilterItemDto>());
            Assert.That(queryRequest.Relations.Single().Name, Is.EqualTo("Manager2"));
            Assert.That(queryRequest.FilterPlanText, Is.EqualTo("Name = <Alice Johnson> AND Id = <1>"));

            repository.Save("test-user", queryRequest);
            using var savedFile = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(storagePath, "UserData", "test-user", "QueryRequests", "legacy.json")));
            var savedProjection = savedFile.RootElement.GetProperty("Projection");
            Assert.That(savedProjection.GetArrayLength(), Is.EqualTo(3));
            Assert.That(savedProjection[1].GetProperty("alias").GetString(), Is.EqualTo("NEW_LINE"));
            Assert.That(savedFile.RootElement.GetProperty("Filters").ValueKind, Is.EqualTo(JsonValueKind.Object));
            var savedRelations = savedFile.RootElement.GetProperty("Relations");
            Assert.That(savedRelations[0].GetProperty("Name").GetString(), Is.EqualTo("Manager2"));
            Assert.That(savedRelations[0].TryGetProperty("Alias", out _), Is.False);
            Assert.That(savedFile.RootElement.GetProperty("FilterPlanText").GetString(),
                Is.EqualTo("Name = <Alice Johnson> AND Id = <1>"));
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }
}
#endif
