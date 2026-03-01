using Dependinator.Core;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Dependinator;

public static class DependinatorServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorServices<TEntryAssemblyMarker>(this IServiceCollection services)
    {
        services.AddMudServices();
        services.AddDependinatorCoreServices();

        services.AddAssemblyServices(typeof(DependinatorServiceCollectionExtensions));

        return services;
    }
}
