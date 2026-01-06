using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace DependinatorCore.Utils;

[AttributeUsage(AttributeTargets.Class)]
public class SingletonAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public class ScopedAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public class TransientAttribute : Attribute { }

public static class DependencyInjection
{
    public static void RegisterTypesInAssemblyOf<T>(this IServiceCollection services)
    {
        services.Register<T>(t => t.HasAttribute<SingletonAttribute>(), (s, i, t) => s.AddSingleton(i, t));
        services.Register<T>(t => t.HasAttribute<TransientAttribute>(), (s, i, t) => s.AddTransient(i, t));
        services.Register<T>(t => t.HasAttribute<ScopedAttribute>(), (s, i, t) => s.AddScoped(i, t));
        services.Register<T>(
            t =>
                !t.HasAttribute<SingletonAttribute>()
                && !t.HasAttribute<ScopedAttribute>()
                && !t.HasAttribute<TransientAttribute>(),
            (s, i, t) => s.AddTransient(i, t)
        );
    }

    static bool HasAttribute(this Type type, Type attributeType) => type.IsDefined(attributeType, inherit: true);

    static bool HasAttribute<T>(this Type type)
        where T : Attribute => type.HasAttribute(typeof(T));

    static void Register<T>(
        this IServiceCollection services,
        Func<Type, bool> predicate,
        Action<IServiceCollection, Type, Type> registerAction
    )
    {
        typeof(T)
            .Assembly.GetTypes()
            .Where(t => !t.IsGenericType)
            .Where(IsNonAbstractClass)
            .Where(predicate)
            .ForEach(t =>
                t.GetBaseTypes()
                    .Where(t => t != typeof(Object))
                    .ForEach(i =>
                    {
                        Log.Info($"Registering {i} as {t.Name}");
                        try
                        {
                            registerAction(services, i, t);
                        }
                        catch (Exception e)
                        {
                            Log.Exception(e, $"Failed to register {i.Name} as {t.Name}");
                        }
                    })
            );
    }

    static bool IsNonAbstractClass(this Type type)
    {
        if (type.IsSpecialName)
            return false;

        if (type.IsClass && !type.IsAbstract)
        {
            if (type.HasAttribute<CompilerGeneratedAttribute>())
            {
                return false;
            }

            return true;
        }

        return false;
    }

    static IEnumerable<Type> GetBaseTypes(this Type type)
    {
        foreach (var implementedInterface in type.GetInterfaces())
        {
            yield return implementedInterface;
        }

        var baseType = type.BaseType;
        while (baseType != null)
        {
            yield return baseType;
            baseType = baseType.BaseType;
        }
    }
}
