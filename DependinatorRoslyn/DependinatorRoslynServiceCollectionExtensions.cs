using Microsoft.Extensions.DependencyInjection;

namespace Dependinator.Roslyn;

public static class DependinatorRoslynServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorRoslynServices(this IServiceCollection services)
    {
        services.AddAssemblyServices(typeof(DependinatorRoslynServiceCollectionExtensions));
        return services;
    }
}
