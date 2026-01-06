using Microsoft.Extensions.DependencyInjection;

namespace DependinatorCore;

public static class DependinatorCoreServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorCoreServices(this IServiceCollection services)
    {
        services.AddAssemblyServices(typeof(DependinatorCoreServiceCollectionExtensions));
        return services;
    }
}
