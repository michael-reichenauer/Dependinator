using Microsoft.Extensions.DependencyInjection;

// Dependinator.TypeScript parses TypeScript and JavaScript projects (a tsconfig.json,
// jsconfig.json or package.json and the source files it covers) into the Dependinator.Core
// model: folders and files become containers, classes and interfaces nodes, functions,
// variables and members rows, and what the compiler resolves each name to becomes links.
// The parsing is done by a helper program on Node (Helper/, the TypeScript compiler), which
// TypeScriptParser runs. Registered by the hosts that parse locally (LSP, Blazor Server).
namespace Dependinator.TypeScript;

public static class DependinatorTypeScriptServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorTypeScriptServices(this IServiceCollection services)
    {
        services.AddAssemblyServices(typeof(DependinatorTypeScriptServiceCollectionExtensions));
        return services;
    }
}
