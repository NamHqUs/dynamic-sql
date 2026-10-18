using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
#if NET10_0
using Demo.Contracts.Repositories;
using Demo.Repositories.FileStorage;
using Demo.Share;
using Demo.Services;
#endif

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
internal sealed class _35_EntityContextTests : TestBase
{
    [Test]
    public void QueryDefinitionCreatesIndependentContextsWithDifferentKeyMappings()
    {
        var query = _db.Query(new QueryDefinition
        {
            Entity = "InvoiceItems",
            EntityAliases =
            [
                new("Product1", "Product", [new("ProductId", "Id")]),
                new("Product2", "Product", [new("Id", "Id")])
            ],
            Fields = QueryDefinitionParser.ParseProjections(
                "Product.Name|MetadataProduct, Product1.Name|ProductByForeignKey, Product2.Name|ProductById"),
            Filter = QueryDefinitionParser.ParseFilter("Product1.Id < Product2.Id")
        });

        var sql = query.ToSql().CommandText;

        Assert.That(sql.Split("LEFT JOIN").Length - 1, Is.EqualTo(3));
        Assert.That(sql, Does.Contain("t1.\"Id\" = t0.\"ProductId\""));
        Assert.That(sql, Does.Contain("t2.\"Id\" = t0.\"Id\""));
        Assert.That(sql, Does.Contain("t3.\"Id\" = t0.\"ProductId\""));
        Assert.That(sql, Does.Contain("t1.\"Id\" < t2.\"Id\""));
    }

    [Test]
    public void QueryDefinitionJsonDeserializesEntityAliases()
    {
        var definition = QueryDefinition.FromJson("""
            {
              "entity": "InvoiceItems",
              "entityAliases": [{
                "alias": "Product2",
                "relationPath": "Product",
                "relatedKeys": [
                  { "fromDbColumn": "Id", "toDbColumn": "Id" }
                ]
              }]
            }
            """);

        Assert.That(definition.EntityAliases, Has.Count.EqualTo(1));
        Assert.That(definition.EntityAliases[0].Alias, Is.EqualTo("Product2"));
        Assert.That(definition.EntityAliases[0].RelatedKeys[0], Is.EqualTo(new RelationItem("Id", "Id")));
    }

    [Test]
    public void EntityAliasCanCloneNestedRelationPathForCrossEntityComparison()
    {
        var query = _db.Query(new QueryDefinition
        {
            Entity = "InvoiceItems",
            EntityAliases = [new("OtherItem", "Invoice.Items", [new("Id", "InvoiceId")])],
            Fields = QueryDefinitionParser.ParseProjections(
                "Product.Name|Product, OtherItem.Product.Name|FrequentlyBoughtWith, Count()|OrdersTogether"),
            Filter = QueryDefinitionParser.ParseFilter("Product.Id < OtherItem.Product.Id")
        });

        var sql = query.ToSql().CommandText;

        Assert.That(sql, Does.Contain("t1.\"Id\" = t0.\"InvoiceId\""));
        Assert.That(sql, Does.Contain("t2.\"InvoiceId\" = t1.\"Id\""));
        Assert.That(sql, Does.Contain("t3.\"Id\" < t4.\"Id\""));
        Assert.That(sql, Does.Contain("GROUP BY"));
    }

#if NET10_0
    [Test]
    public void QueryDefinitionTextRoundTripsEntityAliases()
    {
        var definition = new QueryDefinition
        {
            Entity = "InvoiceItems",
            EntityAliases =
            [
                new("Product1", "Product", [new("ProductId", "Id")]),
                new("Product2", "Product", [new("Id", "Id")])
            ],
            Fields = QueryDefinitionParser.ParseProjections("Product1.Name|First, Product2.Name|Second")
        };
        var service = new QueryRequestService(null!);

        var parsed = service.ParseQueryRequestString(definition.ToString());

        Assert.That(parsed.Entity, Is.EqualTo("InvoiceItems"));
        Assert.That(parsed.EntityAliases, Has.Count.EqualTo(2));
        Assert.That(parsed.EntityAliases[0].Alias, Is.EqualTo("Product1"));
        Assert.That(parsed.EntityAliases[0].RelationPath, Is.EqualTo("Product"));
        Assert.That(parsed.EntityAliases[0].RelatedKeys, Is.EqualTo(definition.EntityAliases[0].RelatedKeys));
        Assert.That(parsed.EntityAliases[1].Alias, Is.EqualTo("Product2"));
        Assert.That(parsed.EntityAliases[1].RelatedKeys, Is.EqualTo(definition.EntityAliases[1].RelatedKeys));
        Assert.That(parsed.Fields, Is.EqualTo(definition.Fields));
    }

    [Test]
    public void BuiltinQueryRequestsLoadAsReadOnlyAndRestoreFromTheirYamlText()
    {
        var directory = new DirectoryInfo("../../../../Demo.WebApi/FileStorage/QueryRequests").FullName;
        var files = Directory.EnumerateFiles(directory, "*.yml")
            .Select(path => new BuiltinQueryRequestFile(Path.GetFileName(path)!, File.ReadAllText(path)))
            .ToArray();
        var service = new QueryRequestService(new TestQueryRequestRepository(files));

        var requests = service.ListQueryRequests();

        Assert.That(requests, Has.Count.EqualTo(files.Length));
        Assert.That(requests, Has.All.Property(nameof(StoredQueryRequest.IsBuiltin)).True);
        foreach (var request in requests)
        {
            Assert.That(request.Query.Name, Is.Not.Null.And.Not.Empty);
            var restored = service.ParseQueryRequestString(request.RequestText!, request.Query.Entity);
            Assert.That(restored.Name, Is.EqualTo(request.Query.Name));
            Assert.That(restored.Entity, Is.EqualTo(request.Query.Entity));
            Assert.That(restored.Fields.Select(field => field.ToText()),
                Is.EqualTo(request.Query.Fields.Select(field => field.ToText())));
        }

        Assert.That(service.LoadQueryRequest(requests[0].FileName)?.IsBuiltin, Is.True);
        Assert.That(service.UpdateQueryRequest(requests[0].FileName, requests[0].Query), Is.Null);
        Assert.That(service.DeleteQueryRequest(requests[0].FileName), Is.False);
    }

    [Test]
    public void BuiltinSqlFunctionsAreReadOnly()
    {
        var repository = new FunctionDefinitionFileRepository(null!);
        var builtins = new MetaProvider([]).GetFunctions()
            .Concat(CustomFunctionDefintions.Functions);

        foreach (var function in builtins)
            Assert.That(repository.IsBuiltin(function.Name), Is.True, function.Name);
    }

    private sealed class TestQueryRequestRepository(
        IReadOnlyList<BuiltinQueryRequestFile> builtinFiles) : IQueryRequestRepository
    {
        private readonly Dictionary<string, StoredQueryRequest> _requests = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<StoredQueryRequest> GetAllCustom() => _requests.Values.ToArray();
        public IReadOnlyList<BuiltinQueryRequestFile> GetBuiltinFiles() => builtinFiles;
        public StoredQueryRequest? Get(string fileName) => _requests.GetValueOrDefault(fileName);
        public void Save(StoredQueryRequest request) => _requests[request.FileName] = request;
        public bool Delete(string fileName) => _requests.Remove(fileName);
    }
#endif

    [Test]
    public void QueryDefinitionRequiresKeysForEveryEntityAlias()
    {
        var query = _db.Query(new QueryDefinition
        {
            Entity = "InvoiceItems",
            EntityAliases = [new("Product2", "Product", [])],
            Fields = QueryDefinitionParser.ParseProjections("Product2.Name")
        });

        var exception = Assert.Throws<FormatException>(() => query.ToSql());

        Assert.That(exception!.Message, Is.EqualTo("Entity alias [Product2] requires at least one relation key."));
    }
}
