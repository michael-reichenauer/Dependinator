using Microsoft.Extensions.DependencyInjection;

// Root namespace of Dependinator.Core: the host-agnostic core that parses code
// into a model of nodes and links and exposes the shared services, RPC, and utilities
// used by the UI and hosts. It has no dependency on any UI framework or host.
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
