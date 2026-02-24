using Microsoft.Extensions.DependencyInjection;

namespace DependinatorRoslyn;

public static class DependinatorRoslynServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorRoslynServices(this IServiceCollection services)
    {
        services.AddAssemblyServices(typeof(DependinatorRoslynServiceCollectionExtensions));
        return services;
    }
}
