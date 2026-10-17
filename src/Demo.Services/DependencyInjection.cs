using Demo.ServiceContracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddDemoServices(this IServiceCollection services)
    {
        services.AddSingleton<IMetadataService, MetadataService>();
        services.AddSingleton<IFunctionDefinitionService, FunctionDefinitionService>();
        services.AddSingleton<IDynamicQueryService, DynamicQueryService>();
        services.AddSingleton<IQueryRequestService, QueryRequestService>();
        services.AddSingleton<IRawDataService, RawDataService>();
        return services;
    }
}
