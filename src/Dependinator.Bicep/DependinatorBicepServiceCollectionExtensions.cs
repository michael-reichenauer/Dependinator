using Microsoft.Extensions.DependencyInjection;

// Dependinator.Bicep parses Azure Bicep deployments (an entry-point .bicep file and the local
// modules it references) into the Dependinator.Core model: resources and modules become nodes
// with Azure service icons, parameters/variables/outputs become member rows, and the compiler's
// dependency graph becomes links. Registered by the hosts that parse locally (LSP, Blazor Server).
namespace Dependinator.Bicep;

public static class DependinatorBicepServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorBicepServices(this IServiceCollection services)
    {
        services.AddAssemblyServices(typeof(DependinatorBicepServiceCollectionExtensions));
        return services;
    }
}
