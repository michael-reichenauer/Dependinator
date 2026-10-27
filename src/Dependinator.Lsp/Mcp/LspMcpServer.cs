using Dependinator.Core.ModelQueries;
using Dependinator.Core.Rpc;
using Dependinator.Core.Utils;
using Dependinator.Core.Utils.Logging;
using Dependinator.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

// Dependinator's MCP server in VS Code: the LSP runs it, since the LSP runs while VS Code is
// open, and its tools ask the webview's model over the RPC tunnel (with a time limit, as the
// webview may be closed, and a quick check that it is open first). The extension keeps the token and the port (initializationOptions
// { mcp: { token, port } }), so the URL and token stay the same across restarts, and learns the
// actual URL from mcp/serverStarted to register the server with VS Code and Claude Code.
namespace Dependinator.Lsp.Mcp;

static class LspMcpServer
{
    public const string StartedMethod = "mcp/serverStarted";

    // Long enough for a question on a large model, short enough for an agent waiting on a closed webview.
    static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(20);

    // A tool call skips the connection check this soon after one succeeded, as agents ask in
    // bursts; kept short, since a call after the webview closed waits out the whole time limit.
    static readonly TimeSpan ConnectedRecently = TimeSpan.FromSeconds(5);

    // Runs as long as the LSP process does.
    static McpHttpServer? running;
    static DateTime lastConnected = DateTime.MinValue;

    // Never throws: the MCP server is optional, and a failed background task stops the LSP.
    public static async Task StartAsync(ILanguageServer server, object? initializationOptions)
    {
        try
        {
            await TryStartAsync(server, initializationOptions);
        }
        catch (Exception e)
        {
            Log.Warn($"Could not start the MCP server: {e}");
        }
    }

    static async Task TryStartAsync(ILanguageServer server, object? initializationOptions)
    {
        var options = (initializationOptions as JToken)?["mcp"];
        var token = options?.Value<string>("token");
        if (string.IsNullOrEmpty(token))
        {
            Log.Info("No MCP server: the extension gave no token");
            return;
        }

        var jsonRpc = server.Services.GetRequiredService<IJsonRpcService>();
        var queries = new TimeLimitedModelQueries(
            jsonRpc.GetRemoteProxy<IModelQueryService>(),
            TimeLimit,
            () => IsWebviewConnectedAsync(jsonRpc)
        );
        var started = await McpHttpServer.StartAsync(queries, token, options?.Value<int?>("port") ?? 0);
        if (started is not McpHttpServer mcp)
        {
            Log.Warn($"Could not start the MCP server: {started.Error.AllMessages()}");
            return;
        }

        running = mcp;
        Log.Info("MCP server at", mcp.Url);
        server.SendNotification(StartedMethod, new McpServerStarted(mcp.Url));
    }

    // The webview answers the RPC connection check within moments while it is open.
    static async Task<bool> IsWebviewConnectedAsync(IJsonRpcService jsonRpc)
    {
        if (DateTime.UtcNow - lastConnected < ConnectedRecently)
            return true;
        try
        {
            await jsonRpc.CheckConnectionAsync(TimeSpan.FromSeconds(2));
            lastConnected = DateTime.UtcNow;
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}

public record McpServerStarted(string Url);
