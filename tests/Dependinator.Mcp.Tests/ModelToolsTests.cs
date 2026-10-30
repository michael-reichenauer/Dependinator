using Dependinator.Core.ModelQueries;
using Dependinator.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Dependinator.Mcp.Tests;

// The MCP server end to end: a real MCP client talks Streamable HTTP to the endpoint a host maps,
// and the tools answer from IModelQueryService (here a mock of the open diagram).
public sealed class ModelToolsTests : IAsyncLifetime
{
    readonly Mock<IModelQueryService> queries = new();
    WebApplication? app;
    McpClient? client;

    McpClient Client => client!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(queries.Object);
        builder.Services.AddDependinatorMcp();
        app = builder.Build();
        app.MapDependinatorMcp();
        await app.StartAsync();

        var address = app
            .Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(address + DependinatorMcpExtensions.Path) }
        );
        client = await McpClient.CreateAsync(transport);
    }

    public async Task DisposeAsync()
    {
        if (client is not null)
            await client.DisposeAsync();
        if (app is not null)
            await app.DisposeAsync();
    }

    [Fact]
    public async Task ListTools_ShouldOfferTheReadOnlyModelTools()
    {
        var tools = await Client.ListToolsAsync();

        Assert.Equal(
            [
                "check_rule",
                "find_paths",
                "get_dependencies",
                "get_model",
                "list_children",
                "list_cycles",
                "list_rules",
                "read_source",
                "search_nodes",
            ],
            tools.Select(t => t.Name).Order(StringComparer.Ordinal)
        );
        Assert.All(tools, t => Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint));
        Assert.Contains("Dependinator", Client.ServerInstructions);
    }

    [Fact]
    public async Task CallTool_ShouldAnswerWithCompactJson()
    {
        queries
            .Setup(q => q.CheckRuleAsync("Core", "UI"))
            .ReturnsAsync(new RuleResult("Core", "UI", false, true, 1, 3, [new("Core.A", "UI.B", 3)], [], 0));

        var result = await Client.CallToolAsync(
            "check_rule",
            new Dictionary<string, object?> { ["from"] = "Core", ["to"] = "UI" }
        );

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(
            """{"from":"Core","to":"UI","isRule":false,"isResolved":true,"violationCount":1,"linkCount":3,"violations":[{"source":"Core.A","target":"UI.B","linkCount":3}],"allowed":[],"allowedCount":0}""",
            Text(result)
        );
    }

    [Fact]
    public async Task CallTool_ShouldPassTheDefaults_AndLeaveOutEmptyFields()
    {
        var node = new QueryNode("Core", "Core (dll)", "Assembly", 2);
        queries
            .Setup(q => q.GetDependenciesAsync("Core", false))
            .ReturnsAsync(new NodeDependencies(node, false, [], 0));

        var result = await Client.CallToolAsync(
            "get_dependencies",
            new Dictionary<string, object?> { ["nodeName"] = "Core" }
        );

        Assert.Equal(
            """{"node":{"name":"Core","title":"Core (dll)","type":"Assembly","childCount":2},"isIncoming":false,"groups":[],"groupCount":0}""",
            Text(result)
        );
    }

    [Fact]
    public async Task CallTool_ShouldReturnTheErrorAsAnErrorResult()
    {
        queries
            .Setup(q => q.GetModelAsync())
            .ReturnsAsync(new Error("No Dependinator diagram is open. Open the diagram and try again."));

        var result = await Client.CallToolAsync("get_model");

        Assert.True(result.IsError);
        Assert.Equal("No Dependinator diagram is open. Open the diagram and try again.", Text(result));
    }

    [Fact]
    public async Task ReadSource_ShouldAnswerWithTheCodeAsPlainText()
    {
        var node = new QueryNode("Core.Repo", "Repo", "ClassType", 0);
        queries
            .Setup(q => q.ReadSourceAsync("Core.Repo"))
            .ReturnsAsync(new NodeSource(node, "src/Repo.cs, lines 2-3:\n    2  class Repo\n    3  {"));

        var result = await Client.CallToolAsync(
            "read_source",
            new Dictionary<string, object?> { ["nodeName"] = "Core.Repo" }
        );

        Assert.NotEqual(true, result.IsError);
        Assert.Equal("Core.Repo (ClassType):\n\nsrc/Repo.cs, lines 2-3:\n    2  class Repo\n    3  {", Text(result));
    }

    static string Text(CallToolResult result) => Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
