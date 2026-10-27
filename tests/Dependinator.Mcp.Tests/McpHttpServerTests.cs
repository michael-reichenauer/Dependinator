using System.Net;
using System.Net.Sockets;
using Dependinator.Core.ModelQueries;
using ModelContextProtocol.Client;

namespace Dependinator.Mcp.Tests;

// The LSP's own MCP server: it answers only with the token, prefers the port it was given and
// falls back to a free one, and it takes requests from this computer only.
public sealed class McpHttpServerTests
{
    readonly Mock<IModelQueryService> queries = new();

    [Fact]
    public async Task Start_ShouldRequireTheToken()
    {
        var token = Guid.NewGuid().ToString("N");
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
    public async Task Start_ShouldRefuseRequestsForAnotherHost()
    {
        var token = Guid.NewGuid().ToString("N");
        await using var server = Assert.IsType<McpHttpServer>(
            (await McpHttpServer.StartAsync(queries.Object, token, 0)).Value
        );
        using var http = new HttpClient();

        // A page whose host name was rebound to 127.0.0.1 sends its own name as Host.
        var request = new HttpRequestMessage(HttpMethod.Post, server.Url) { Content = new StringContent("{}") };
        request.Headers.Host = "evil.example";
        request.Headers.Authorization = new("Bearer", token);
        var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("localhost:5000", null, true)]
    [InlineData("127.0.0.1:5000", "http://localhost:6274", true)]
    [InlineData("[::1]:5000", null, true)]
    [InlineData("evil.example", null, false)]
    [InlineData("localhost:5000", "https://evil.example", false)]
    public void IsLocalRequest_ShouldAcceptOnlyThisComputer(string host, string? origin, bool isLocal)
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Host = new(host);
        if (origin is not null)
            context.Request.Headers.Origin = origin;

        Assert.Equal(isLocal, DependinatorMcpExtensions.IsLocalRequest(context.Request));
    }

    [Fact]
    public void IsLocalRequest_ShouldRefuseAnotherComputer()
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Host = new("localhost:5000");
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.20");

        Assert.False(DependinatorMcpExtensions.IsLocalRequest(context.Request));
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
