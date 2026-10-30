using System.Net;
using System.Security.Cryptography;
using System.Text;
using Dependinator.Core.ModelQueries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dependinator.Mcp;

// The MCP server on its own, for a host that is no web app itself (the LSP): a small Kestrel app
// on the loopback address. Every request must carry the token as a bearer token, so other local
// programs, and web pages that reach localhost, cannot read the model. The preferred port keeps
// the URL stable across restarts, so a client's saved configuration stays valid; when it is
// taken (a second VS Code window), any free port is used instead.
sealed class McpHttpServer : IAsyncDisposable
{
    readonly WebApplication app;

    McpHttpServer(WebApplication app, string url)
    {
        this.app = app;
        Url = url;
    }

    public string Url { get; }

    public static async Task<Result<McpHttpServer>> StartAsync(
        IModelQueryService queries,
        string token,
        int preferredPort
    )
    {
        if (string.IsNullOrEmpty(token))
            return new Error("The MCP server needs a token.");
        if (preferredPort > 0 && await TryStartAsync(queries, token, preferredPort) is McpHttpServer server)
            return server;
        return await TryStartAsync(queries, token, 0);
    }

    static async Task<Result<McpHttpServer>> TryStartAsync(IModelQueryService queries, string token, int port)
    {
        // The LSP runs in the user's workspace folder: no content root, appsettings.json or
        // ASPNETCORE_ variables of theirs may add endpoints or file watchers here.
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions
            {
                Args = [],
                ContentRootPath = AppContext.BaseDirectory,
                EnvironmentName = Environments.Production,
            }
        );
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders(); // The LSP's stdout is its protocol stream
        // The host process decides about Ctrl+C and termination, not this server.
        builder.Services.AddSingleton<IHostLifetime, HostProcessLifetime>();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));
        builder.Services.AddSingleton(queries);
        builder.Services.AddDependinatorMcp();
        var app = builder.Build();
        app.Use(RequireToken(token));
        app.MapDependinatorMcp();
        try
        {
            await app.StartAsync();
        }
        catch (Exception e)
        {
            await app.DisposeAsync();
            return new Error($"Could not start the MCP server on port {port}", e);
        }

        var address = app
            .Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
        return new McpHttpServer(app, address.TrimEnd('/') + DependinatorMcpExtensions.Path);
    }

    static Func<HttpContext, RequestDelegate, Task> RequireToken(string token)
    {
        var expected = Encoding.UTF8.GetBytes($"Bearer {token}");
        return (context, next) =>
        {
            var actual = Encoding.UTF8.GetBytes(context.Request.Headers.Authorization.ToString());
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            return next(context);
        };
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    sealed class HostProcessLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
