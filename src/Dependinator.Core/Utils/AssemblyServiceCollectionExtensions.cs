using Microsoft.Extensions.DependencyInjection;

namespace Dependinator.Core.Utils;

[AttributeUsage(AttributeTargets.Class)]
public class SingletonAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public class ScopedAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public class TransientAttribute : Attribute { }

public static class AssemblyServiceCollectionExtensions
{
    public static IServiceCollection AddAssemblyServices(this IServiceCollection services, Type assemblyType)
    {
        // AsSelfWithInterfaces registers the class as itself and forwards the interface
        // registrations to that self registration, so resolving a singleton by interface
        // or by concrete class yields the same instance. (AsImplementedInterfaces().AsSelf()
        // would create a separate singleton instance per service type.)
        services.Scan(scan =>
            scan.FromAssembliesOf(assemblyType)
                .AddClasses(c => c.WithAttribute<SingletonAttribute>(), publicOnly: false)
                .AsSelfWithInterfaces()
                .WithSingletonLifetime()
                .AddClasses(c => c.WithAttribute<ScopedAttribute>(), publicOnly: false)
                .AsSelfWithInterfaces()
                .WithScopedLifetime()
                .AddClasses(c => c.WithAttribute<TransientAttribute>(), publicOnly: false)
                .AsSelfWithInterfaces()
                .WithTransientLifetime()
        );

        return services;
    }

    internal static bool HasAttribute(this Type type, Type attributeType) =>
        type.IsDefined(attributeType, inherit: true);

    internal static bool HasAttribute<T>(this Type type)
        where T : Attribute => type.HasAttribute(typeof(T));
}
