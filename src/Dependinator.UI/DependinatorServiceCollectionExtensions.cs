using Dependinator.Core;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

// Root namespace of Dependinator.UI: the shared Blazor/MudBlazor UI — diagram canvas, modeling,
// and app services — reused by every host (Web, Wasm, VS Code). This file registers the UI
// services for DI.
namespace Dependinator.UI;

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
