using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
#if NET10_0
using Demo.Contracts.Application;
using Demo.Contracts.Dto;
using Demo.Contracts.Repositories;
using Demo.Repositories.FileStorage;
using Demo.Share;
using Demo.Services;
using Microsoft.Extensions.Configuration;
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
    public void QueryDefinitionYmlParsesEntityAliases()
    {
        var definition = QueryDefinitionParser.ParseFromYml("""
            Entity: InvoiceItems
            EntityAliases:
              - "Product2: Product(Id)"
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
        var service = new ReportService(null!, new TestUserSectionManager());

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
    public void BuiltinReportsLoadAsReadOnlyAndRestoreFromTheirYamlText()
    {
        var directory = new DirectoryInfo("../../../../Demo.WebApi/FileStorage/QueryRequests").FullName;
        var files = Directory.EnumerateFiles(directory, "*.yml")
            .Select(path =>
            {
                var requestText = File.ReadAllText(path);
                return new ReportDto(
                    Path.GetFileName(path)!,
                    QueryDefinitionParser.ParseFromYml(requestText),
                    requestText)
                {
                    CanDelete = false
                };
            })
            .ToArray();
        var service = new ReportService(
            new TestReportRepository(files),
            new TestUserSectionManager());

        var reports = service.ListReports();

        Assert.That(reports, Has.Count.EqualTo(files.Length));
        Assert.That(reports, Has.All.Property(nameof(ReportDto.CanDelete)).False);
        foreach (var report in reports)
        {
            Assert.That(report.Query.Name, Is.Not.Null.And.Not.Empty);
            var restored = service.ParseQueryRequestString(report.RequestText!, report.Query.Entity);
            Assert.That(restored.Name, Is.EqualTo(report.Query.Name));
            Assert.That(restored.Entity, Is.EqualTo(report.Query.Entity));
            Assert.That(restored.Fields.Select(field => field.ToText()),
                Is.EqualTo(report.Query.Fields.Select(field => field.ToText())));
        }

        Assert.That(service.GetReport(reports[0].FileName)?.CanDelete, Is.False);
        Assert.That(service.UpdateReport(reports[0].FileName, reports[0].Query), Is.Null);
        Assert.That(service.DeleteReport(reports[0].FileName), Is.False);
    }

    [Test]
    public void BuiltinSqlFunctionsAreReadOnly()
    {
        var repository = new FunctionDefinitionFileRepository(null!);
        var builtins = new MetaProvider([]).GetFunctions()
            .Concat(CustomFunctionDefintions.Functions);
        var builtinNames = repository.GetBuiltin()
            .Select(function => function.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var function in builtins)
            Assert.That(builtinNames.Contains(function.Name), Is.True, function.Name);
    }

    [Test]
    public void MetadataResetDeletesOnlyTheSpecifiedUserSectionsData()
    {
        var root = Path.Combine(Path.GetTempPath(), "DynamicSqlTest", Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStoragePath"] = Path.GetRelativePath(Environment.CurrentDirectory, root)
            })
            .Build();
        var resolver = new FilePathResolver(configuration);
        var repository = new MetadataFileRepository(resolver);
        var firstSection = resolver.GetUserStoreFolder("section-a", "Metadata");
        var secondSection = resolver.GetUserStoreFolder("section-b", "Metadata");

        try
        {
            Directory.CreateDirectory(firstSection);
            Directory.CreateDirectory(secondSection);
            File.WriteAllText(Path.Combine(firstSection, "custom.json"), "{}");
            File.WriteAllText(Path.Combine(secondSection, "custom.json"), "{}");

            repository.Reset("section-a");

            Assert.That(Directory.Exists(firstSection), Is.False);
            Assert.That(File.Exists(Path.Combine(secondSection, "custom.json")), Is.True);
            Assert.That(repository.GetBuiltin(), Is.Not.Empty);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestUserSectionManager : IUserSectionManager
    {
        public string GetSectionId() => "test";
    }

    private sealed class TestReportRepository(
        IReadOnlyList<ReportDto> builtinReports) : IReportRepository
    {
        private readonly Dictionary<string, ReportDto> _reports = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<ReportDto> GetUserDefined(string userSectionId)
            => _reports.Values.ToArray();
        public IReadOnlyList<ReportDto> GetBuiltin() => builtinReports;
        public ReportDto? Get(string userSectionId, string fileName)
            => _reports.GetValueOrDefault(fileName);
        public void Save(string userSectionId, ReportDto report)
            => _reports[report.FileName] = report;
        public bool Delete(string userSectionId, string fileName) => _reports.Remove(fileName);
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
