using System.Text.Json;
using Dependinator.Core.ModelQueries;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Dependinator.Claude;

// Dependinator's model tools for ModelToolsAgent, through the host's own MCP server: the agent is
// an MCP client like Claude Code or Copilot, so it gets the same tools, with the same names,
// descriptions and answers, and nothing is defined twice. The server asks the open diagram.
sealed class McpAgentTools(McpClient client, IReadOnlyList<AgentTool> tools) : IAgentTools
{
    public IReadOnlyList<AgentTool> Tools => tools;

    public static async Task<Result<IAgentTools>> ConnectAsync(ModelToolsAddress address)
    {
        McpClient? client = null;
        try
        {
            var options = new HttpClientTransportOptions { Endpoint = new Uri(address.Url), Name = "dependinator" };
            if (address.Token is { Length: > 0 } token)
                options.AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" };
            client = await McpClient.CreateAsync(new HttpClientTransport(options));
            var listed = await client.ListToolsAsync();
            List<AgentTool> tools =
            [
                .. listed.Select(t => new AgentTool(t.Name, t.Description ?? "", t.JsonSchema.Clone())),
            ];
            return new McpAgentTools(client, tools);
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            if (client is not null)
                await client.DisposeAsync();
            return new Error($"Could not reach Dependinator's model tools: {e.Message}", e);
        }
    }

    public async Task<AgentToolResult> CallAsync(string name, JsonElement input)
    {
        Dictionary<string, object?> arguments =
            input.ValueKind == JsonValueKind.Object
                ? input.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone())
                : [];
        var result = await client.CallToolAsync(name, arguments);
        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        return new AgentToolResult(text, result.IsError == true);
    }

    public ValueTask DisposeAsync() => client.DisposeAsync();
}
