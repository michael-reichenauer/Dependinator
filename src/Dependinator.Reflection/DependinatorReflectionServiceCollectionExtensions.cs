using Microsoft.Extensions.DependencyInjection;

// Root namespace of Dependinator.Reflection: reflection-based parsing of compiled assemblies
// (Mono.Cecil + decompilation), MSBuild solutions, and json model files. Currently not
// registered by any host; call AddDependinatorReflectionServices to activate.
namespace Dependinator.Reflection;

public static class DependinatorReflectionServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorReflectionServices(this IServiceCollection services)
    {
        services.AddAssemblyServices(typeof(DependinatorReflectionServiceCollectionExtensions));
        return services;
    }
}
