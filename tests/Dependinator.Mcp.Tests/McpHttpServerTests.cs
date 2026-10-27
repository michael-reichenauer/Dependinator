using System.Net;
using System.Net.Sockets;
using Dependinator.Core.ModelQueries;
using ModelContextProtocol.Client;

namespace Dependinator.Mcp.Tests;

// The LSP's own MCP server: it answers only with the token, prefers the port it was given and
// falls back to a free one, and a question the webview does not answer ends with an error.
public sealed class McpHttpServerTests
{
    readonly Mock<IModelQueryService> queries = new();

    [Fact]
    public async Task Start_ShouldRequireTheToken()
    {
        var token = McpHttpServer.NewToken();
        await using var server = Assert.IsType<McpHttpServer>(
            (await McpHttpServer.StartAsync(queries.Object, token, 0)).Value
        );
        using var http = new HttpClient();

        var withoutToken = await http.PostAsync(server.Url, new StringContent("{}"));
        await using var client = await McpClient.CreateAsync(
            new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri(server.Url),
                    AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
                }
            )
        );

        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);
        Assert.NotEmpty(await client.ListToolsAsync());
        Assert.StartsWith("http://127.0.0.1:", server.Url);
        Assert.EndsWith("/mcp", server.Url);
    }

    [Fact]
    public async Task Start_ShouldUseThePreferredPort_OrAFreeOneWhenItIsTaken()
    {
        var port = FreePort();
        await using var first = Assert.IsType<McpHttpServer>(
            (await McpHttpServer.StartAsync(queries.Object, "a", port)).Value
        );
        await using var second = Assert.IsType<McpHttpServer>(
            (await McpHttpServer.StartAsync(queries.Object, "b", port)).Value
        );

        Assert.Equal($"http://127.0.0.1:{port}/mcp", first.Url);
        Assert.NotEqual(first.Url, second.Url);
    }

    [Fact]
    public async Task TimeLimitedQueries_ShouldReturnAnError_WhenNobodyAnswers()
    {
        queries.Setup(q => q.GetModelAsync()).Returns(new TaskCompletionSource<Result<ModelSummary>>().Task);
        var limited = new TimeLimitedModelQueries(queries.Object, TimeSpan.FromMilliseconds(50));

        var result = await limited.GetModelAsync();

        Assert.Equal(TimeLimitedModelQueries.NoAnswerMessage, Assert.IsType<Error>(result.Value).Message);
    }

    [Fact]
    public async Task TimeLimitedQueries_ShouldNotAsk_WhenTheOtherSideIsNotConnected()
    {
        var limited = new TimeLimitedModelQueries(
            queries.Object,
            TimeSpan.FromMinutes(1),
            () => Task.FromResult(false)
        );

        var result = await limited.GetModelAsync();

        Assert.Equal(TimeLimitedModelQueries.NoAnswerMessage, Assert.IsType<Error>(result.Value).Message);
        queries.Verify(q => q.GetModelAsync(), Times.Never);
    }

    static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
