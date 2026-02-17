using Microsoft.Extensions.DependencyInjection;

namespace DependinatorCore.Utils;

public static class AssemblyServiceCollectionExtensions
{
    public static IServiceCollection AddAssemblyServices(this IServiceCollection services, Type assemblyType)
    {
        services.Scan(scan =>
            scan.FromAssembliesOf(assemblyType)
                .AddClasses(c => c.WithAttribute<SingletonAttribute>(), publicOnly: false)
                .AsImplementedInterfaces()
                .AsSelf()
                .WithSingletonLifetime()
                .AddClasses(c => c.WithAttribute<ScopedAttribute>(), publicOnly: false)
                .AsImplementedInterfaces()
                .AsSelf()
                .WithScopedLifetime()
                .AddClasses(c => c.WithAttribute<TransientAttribute>(), publicOnly: false)
                .AsImplementedInterfaces()
                .AsSelf()
                .WithTransientLifetime()
        );

        return services;
    }
}
