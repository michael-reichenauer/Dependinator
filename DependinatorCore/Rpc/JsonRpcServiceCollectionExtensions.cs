using Microsoft.Extensions.DependencyInjection;

namespace DependinatorCore.Rpc;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class JsonRpcAttribute : Attribute { }

public static class JsonRpcServiceCollectionExtensions
{
    public static IServiceCollection AddJsonRpcInterfaces(this IServiceCollection services, Type assemblyType)
    {
        assemblyType
            .Assembly.GetTypes()
            .Where(IsRemoteInterface)
            .ForEach(i => services.AddSingleton(i, (sp) => sp.GetRequiredService<IJsonRpcService>().GetRemoteProxy(i)));
        return services;
    }

    public static IServiceProvider UseJsonRpcClasses(this IServiceProvider serviceProvider, Type assemblyType)
    {
        var jsonRpcService = serviceProvider.GetRequiredService<IJsonRpcService>();

        assemblyType
            .Assembly.GetTypes()
            .Where(IsRemoteClass)
            .ForEach(c => jsonRpcService.AddLocalRpcTarget(serviceProvider.GetRequiredService(c)));

        return serviceProvider;
    }

    public static IServiceProvider UseJsonRpc(this IServiceProvider serviceProvider)
    {
        serviceProvider.GetRequiredService<IJsonRpcService>().StartListening();
        return serviceProvider;
    }

    static bool IsRemoteInterface(this Type type) => type.IsInterface && type.HasAttribute<JsonRpcAttribute>();

    static bool IsRemoteClass(this Type type) =>
        type.IsClass && type.GetInterfaces().Any(i => i.HasAttribute<JsonRpcAttribute>());

    static bool HasAttribute(this Type type, Type attributeType) => type.IsDefined(attributeType, inherit: true);

    static bool HasAttribute<T>(this Type type)
        where T : Attribute => type.HasAttribute(typeof(T));
}
