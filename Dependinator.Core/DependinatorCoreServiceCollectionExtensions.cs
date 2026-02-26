using Microsoft.Extensions.DependencyInjection;

namespace Dependinator.Core;

public static class DependinatorCoreServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorCoreServices(this IServiceCollection services)
    {
        services.AddAssemblyServices(typeof(DependinatorCoreServiceCollectionExtensions));
        return services;
    }
}
