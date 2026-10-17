using Demo.Repositories.DataProviders;
using Demo.Repositories.FileStorage;
using Demo.Repositories.InMemory;
using Demo.ServiceContracts.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.Repositories;

public static class DependencyInjection
{
    public static IServiceCollection AddDemoRepositories(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        var provider = configuration["StorageProvider"] ?? "Files";
        if (provider.Equals("Files", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<FilePathResolver>();
            services.AddSingleton<IMetadataRepository, MetadataFileRepository>();
            services.AddSingleton<IFunctionDefinitionRepository, FunctionDefinitionFileRepository>();
            services.AddSingleton<IQueryRequestRepository, QueryRequestFileRepository>();
        }
        else if (provider.Equals("Memory", StringComparison.OrdinalIgnoreCase))
        {
            //services.AddSingleton<IMetadataRepository, MetadataMemoryRepository>();
            //services.AddSingleton<IFunctionDefinitionRepository, FunctionDefinitionMemoryRepository>();
            //services.AddSingleton<IQueryRequestRepository, QueryRequestMemoryRepository>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported StorageProvider '{provider}'. Supported providers are Files and Memory.");
        }

        services.AddSingleton<IRawDataRepository, RawDataRepository>();
        return services;
    }
}
