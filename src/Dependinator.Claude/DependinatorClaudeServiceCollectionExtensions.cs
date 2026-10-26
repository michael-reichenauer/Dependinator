using Microsoft.Extensions.DependencyInjection;

// Dependinator.Claude asks Claude (Anthropic's API) with the user's own API key, for the AI help
// in the UI, such as a suggested verdict on a cycle. Registered by the hosts that run the request:
// the LSP (for the VS Code webview) and the Blazor Server host. Each host also registers its
// IClaudeApiKeyProvider and IClaudeCodeSettings, and lists this assembly's classes as JSON-RPC targets.
namespace Dependinator.Claude;

public static class DependinatorClaudeServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorClaudeServices(this IServiceCollection services)
    {
        services.AddAssemblyServices(typeof(DependinatorClaudeServiceCollectionExtensions));
        return services;
    }
}
