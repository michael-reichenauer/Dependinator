using Microsoft.Extensions.DependencyInjection;

namespace Dependinator.Core.Rpc;

// Marks an interface as a remote RPC service; implementing classes are registered as local
// RPC targets by UseJsonRpcClasses, and remote proxies are registered by AddJsonRpcInterfaces.
[AttributeUsage(AttributeTargets.Interface)]
public class RpcAttribute : Attribute { }

public static class JsonRpcServiceCollectionExtensions
{
    public static IServiceCollection AddJsonRpcInterfaces(this IServiceCollection services, Type assemblyType)
    {
        Log.Info("AddJsonRpcInterfaces");
        assemblyType
            .Assembly.GetTypes()
            .Where(IsRpcInterface)
            .ForEach(i => services.AddSingleton(i, (sp) => sp.GetRequiredService<IJsonRpcService>().GetRemoteProxy(i)));
        return services;
    }

    public static IServiceProvider UseJsonRpcClasses(this IServiceProvider serviceProvider, Type assemblyType)
    {
        Log.Info("UseJsonRpcClasses", assemblyType.FullName);
        var jsonRpcService = serviceProvider.GetRequiredService<IJsonRpcService>();

        assemblyType
            .Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(c => c.GetInterfaces().Where(IsRpcInterface).Select(i => (Interface: i, Service: c)))
            .ForEach(t => jsonRpcService.AddLocalRpcTarget(t.Interface, serviceProvider.GetRequiredService(t.Service)));

        return serviceProvider;
    }

    public static IServiceProvider UseJsonRpc(this IServiceProvider serviceProvider)
    {
        Log.Info("UseJsonRpc");
        serviceProvider.GetRequiredService<IJsonRpcService>().StartListening();
        return serviceProvider;
    }

    static bool IsRpcInterface(this Type type) => type.IsInterface && type.HasAttribute<RpcAttribute>();
}
