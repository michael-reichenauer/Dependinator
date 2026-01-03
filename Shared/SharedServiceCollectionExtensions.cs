using Microsoft.Extensions.DependencyInjection;

namespace Dependinator.Shared;

public static class SharedServiceCollectionExtensions
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddAssemblyServices(typeof(SharedServiceCollectionExtensions));
        return services;
    }
}
