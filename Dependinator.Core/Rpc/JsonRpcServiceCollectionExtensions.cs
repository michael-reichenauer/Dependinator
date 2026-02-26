using Microsoft.Extensions.DependencyInjection;

namespace Dependinator.Core.Rpc;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class RpcAttribute : Attribute { }

public static class JsonRpcServiceCollectionExtensions
{
    public static IServiceCollection AddJsonRpcInterfaces(this IServiceCollection services, Type assemblyType)
    {
        Log.Info("AddJsonRpcInterfaces");
        assemblyType
            .Assembly.GetTypes()
            .Where(IsRemoteInterface)
            .ForEach(i => services.AddSingleton(i, (sp) => sp.GetRequiredService<IJsonRpcService>().GetRemoteProxy(i)));
        return services;
    }

    public static IServiceProvider UseJsonRpcClasses(this IServiceProvider serviceProvider, Type assemblyType)
    {
        Log.Info("UseJsonRpcClasses", assemblyType.FullName);
        var jsonRpcService = serviceProvider.GetRequiredService<IJsonRpcService>();

        assemblyType
            .Assembly.GetTypes()
            .Where(IsRemoteClass)
            .ForEach(c =>
                jsonRpcService.AddLocalRpcTarget(c.GetRemoteInterface(), serviceProvider.GetRequiredService(c))
            );

        return serviceProvider;
    }

    public static IServiceProvider UseJsonRpc(this IServiceProvider serviceProvider)
    {
        Log.Info("UseJsonRpc");
        serviceProvider.GetRequiredService<IJsonRpcService>().StartListening();
        return serviceProvider;
    }

    static bool IsRemoteInterface(this Type type) => type.IsInterface && type.HasAttribute<RpcAttribute>();

    static bool IsRemoteClass(this Type type) =>
        type.IsClass && type.GetInterfaces().Any(i => i.HasAttribute<RpcAttribute>());

    static Type GetRemoteInterface(this Type type) => type.GetInterfaces().First(i => i.HasAttribute<RpcAttribute>());

    static bool HasAttribute(this Type type, Type attributeType) => type.IsDefined(attributeType, inherit: true);

    static bool HasAttribute<T>(this Type type)
        where T : Attribute => type.HasAttribute(typeof(T));
}
