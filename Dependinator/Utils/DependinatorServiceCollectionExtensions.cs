using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Dependinator.Utils;

public static class DependinatorServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorServices<TAssemblyMarker>(this IServiceCollection services)
    {
        services.AddSingleton<IEmbeddedResources, EmbeddedResources<TAssemblyMarker>>();
        services.AddMudServices();

        services.Scan(scan =>
            scan.FromAssembliesOf(typeof(Dependinator.RootClass))
                .AddClasses(c => c.WithAttribute<SingletonAttribute>(), publicOnly: false)
                .AsImplementedInterfaces()
                .WithSingletonLifetime()
                .AddClasses(c => c.WithAttribute<ScopedAttribute>(), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
                .AddClasses(c => c.WithAttribute<TransientAttribute>(), publicOnly: false)
                .AsImplementedInterfaces()
                .WithTransientLifetime()
        );

        return services;
    }
}
