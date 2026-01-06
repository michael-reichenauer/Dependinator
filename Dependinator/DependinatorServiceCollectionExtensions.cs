using DependinatorCore;
using DependinatorCore.Parsing;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Dependinator;

public static class DependinatorServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorServices<TEntryAssemblyMarker>(this IServiceCollection services)
    {
        services.AddMudServices();
        services.AddDependinatorCoreServices();

        services.AddSingleton<IEmbeddedResources, EmbeddedResources<TEntryAssemblyMarker>>();
        services.AddAssemblyServices(typeof(DependinatorServiceCollectionExtensions));

        return services;
    }
}
