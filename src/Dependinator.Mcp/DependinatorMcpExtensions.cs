using System.Net;
using Dependinator.Core;
using Dependinator.Core.ModelQueries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

    // Maps the endpoint, behind a check that the request comes from this computer.
    public static IEndpointConventionBuilder MapDependinatorMcp(this WebApplication app)
    {
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments(Path),
            local =>
                local.Use(
                    (context, next) =>
                    {
                        if (IsLocalRequest(context.Request))
                            return next(context);
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                )
        );
        return app.MapMcp(Path);
    }

    // Tells IModelToolsServer where the endpoint is once the host listens, so the Claude Code runs
    // the app starts can use the tools: the first address, a wildcard one by the loopback name.
    public static void PublishModelToolsAddress(this WebApplication app) =>
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var address = app.Urls.FirstOrDefault(u => u.StartsWith("http://", StringComparison.Ordinal));
            if (address is null || !Uri.TryCreate(address, UriKind.Absolute, out var uri))
                return;
            var host = uri.Host is "0.0.0.0" or "[::]" or "+" or "*" ? "localhost" : uri.Host;
            var url = new UriBuilder(uri) { Host = host, Path = Path }.Uri.ToString();
            app.Services.GetRequiredService<IModelToolsServer>().SetAddress(new ModelToolsAddress(url, null));
        });

    // The server is for agents on this computer: the connection must come from the loopback
    // address (the dev host may listen on others too). A web page can reach localhost as well,
    // through DNS rebinding (its own host name resolving to 127.0.0.1), so the Host must name the
    // loopback address, and a browser's Origin, when sent, must be a page on this computer.
    internal static bool IsLocalRequest(HttpRequest request) =>
        (request.HttpContext.Connection.RemoteIpAddress is not IPAddress remote || IPAddress.IsLoopback(remote))
        && IsLoopbackHost(request.Host.Host)
        && (
            string.IsNullOrEmpty(request.Headers.Origin)
            || Uri.TryCreate(request.Headers.Origin, UriKind.Absolute, out var origin) && IsLoopbackHost(origin.Host)
        );

    static bool IsLoopbackHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address);
}
