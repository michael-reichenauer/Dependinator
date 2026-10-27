using Dependinator.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

// Dependinator.Mcp offers the model open in the diagram to AI agents (Claude Code, Copilot, and
// Dependinator's own Claude Code route) as Model Context Protocol tools over Streamable HTTP. A
// host registers the server and maps its endpoint; the tools ask IModelQueryService, which the
// host provides (an RPC proxy to the UI).
namespace Dependinator.Mcp;

public static class DependinatorMcpExtensions
{
    public const string Path = "/mcp";

    // Sent to the agent when it connects; the tools' own descriptions say what each one does.
    const string Instructions = """
        Dependinator shows a codebase (a .NET solution, or an Azure Bicep deployment) as a dependency
        model: projects, namespaces, types and members, and the links between them. These tools answer
        on the model open in the Dependinator diagram, read-only. Nodes are named by "name" in the
        answers: pass it back as it is (search_nodes finds a name; a node's title also works when it is
        unique). A member's dependencies count for its type. The diagram must be open, and its model is
        as of the last refresh, so it may lag behind unsaved or very recent edits.
        """;

    public static IServiceCollection AddDependinatorMcp(this IServiceCollection services)
    {
        services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation { Name = "dependinator", Version = Build.Version };
                options.ServerInstructions = Instructions;
            })
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<ModelTools>();
        return services;
    }

    public static IEndpointConventionBuilder MapDependinatorMcp(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapMcp(Path);
}
