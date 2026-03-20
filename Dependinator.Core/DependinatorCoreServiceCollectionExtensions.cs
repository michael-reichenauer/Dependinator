using Microsoft.Extensions.DependencyInjection;

namespace Dependinator.Core;

public static class DependinatorCoreServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorCoreServices(this IServiceCollection services)
    {
        services.AddTransient(typeof(Lazy<>), typeof(LazyService<>));
        services.AddAssemblyServices(typeof(DependinatorCoreServiceCollectionExtensions));
        return services;
    }
}
