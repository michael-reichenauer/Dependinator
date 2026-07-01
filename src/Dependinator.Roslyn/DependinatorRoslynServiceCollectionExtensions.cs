using Microsoft.Extensions.DependencyInjection;

// Root namespace of Dependinator.Roslyn: Roslyn-based source parsing that enriches the core
// model with metadata (descriptions, source locations, links) not available from compiled
// assemblies. This file registers its services for DI.
namespace Dependinator.Roslyn;

public static class DependinatorRoslynServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorRoslynServices(this IServiceCollection services)
    {
        services.AddAssemblyServices(typeof(DependinatorRoslynServiceCollectionExtensions));
        return services;
    }
}
