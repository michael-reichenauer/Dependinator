using System.Text.Json;
using Dependinator.Core.Claude;

namespace Dependinator.Claude.Tests;

// The tool-use loop behind ClaudeService.AskWithModelToolsAsync: Claude's tool calls are run and
// their results sent back until it answers, against a local stand-in for Anthropic's API and a
// stand-in for the model tools.
public class ModelToolsAgentTests
{
    const string Schema = """{ "type": "object", "properties": {}, "additionalProperties": false }""";

    const string ToolCallResponse = """
        {
          "id": "msg_1", "type": "message", "role": "assistant", "model": "claude-sonnet-5-5",
          "content": [
            { "type": "thinking", "thinking": "Search first.", "signature": "sig-1" },
            { "type": "tool_use", "id": "toolu_1", "name": "search_nodes", "input": { "query": "Parser" } }
          ],
          "stop_reason": "tool_use", "stop_sequence": null,
          "usage": { "input_tokens": 1000, "output_tokens": 100, "cache_creation_input_tokens": 2000 }
        }
        """;

    const string AnswerResponse = """
        {
          "id": "msg_2", "type": "message", "role": "assistant", "model": "claude-sonnet-5-5",
          "content": [{ "type": "text", "text": "{ \"answer\": \"The parsers.\" }" }],
          "stop_reason": "end_turn", "stop_sequence": null,
          "usage": { "input_tokens": 500, "output_tokens": 200, "cache_read_input_tokens": 3000 }
        }
        """;

    readonly Mock<IClaudeApiKeyProvider> keys = new();
    readonly FakeTools tools = new();

    public ModelToolsAgentTests()
    {
        keys.Setup(k => k.SetKeyHint).Returns("Set a new one.");
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-test");
    }

    [Fact]
    public async Task AskWithModelToolsAsync_ShouldRunClaudesToolCalls_AndReturnItsAnswer()
    {
        using var api = new FakeAnthropicApi(ToolCallResponse, AnswerResponse);
        var service = Service(api);

        var answer = AssertOk(await service.AskWithModelToolsAsync("What parses?", Schema));

        Assert.Equal("""{ "answer": "The parsers." }""", answer.Json);
        Assert.Equal(1, answer.ToolCalls);
        Assert.Equal(("search_nodes", """{"query":"Parser"}"""), Assert.Single(tools.Calls));
        Assert.True(tools.IsDisposed);

        var requests = api.RequestBodies;
        Assert.Equal(2, requests.Count);
        Assert.Contains("\"model\":\"claude-sonnet-5-5\"", requests[0]);
        Assert.Contains("\"name\":\"search_nodes\"", requests[0]);
        Assert.Contains("\"cache_control\":{\"type\":\"ephemeral\"", requests[0]);
        Assert.Contains("\"tool_choice\":{\"type\":\"auto\"", requests[0]);
        // The second request repeats Claude's turn, thinking and signature too, and adds the result.
        Assert.Contains("\"signature\":\"sig-1\"", requests[1]);
        Assert.Contains("\"tool_use_id\":\"toolu_1\"", requests[1]);
        Assert.Contains("Found ParserService", requests[1]);
    }

    [Fact]
    public async Task AskWithModelToolsAsync_ShouldAddUpTheCostOfEveryTurn()
    {
        using var api = new FakeAnthropicApi(ToolCallResponse, AnswerResponse);

        var answer = AssertOk(await Service(api).AskWithModelToolsAsync("What parses?", Schema));

        // Turn 1: 1000 in, 2000 cache write, 100 out; turn 2: 500 in, 3000 cache read, 200 out.
        var expected = (1500 * 2.00m + 2000 * 2.50m + 3000 * 0.20m + 300 * 10.00m) / 1_000_000m;
        Assert.Equal(decimal.Round(expected, 4), answer.CostUsd);
    }

    [Fact]
    public async Task AskWithModelToolsAsync_ShouldGiveClaudeTheError_WhenAToolFails()
    {
        using var api = new FakeAnthropicApi(ToolCallResponse, AnswerResponse);
        tools.Failure = new HttpRequestException("The connection was closed.");

        AssertOk(await Service(api).AskWithModelToolsAsync("What parses?", Schema));

        var second = api.RequestBodies[1];
        Assert.Contains("The tool failed: The connection was closed.", second);
        Assert.Contains("\"is_error\":true", second);
    }

    [Fact]
    public async Task AskWithModelToolsAsync_ShouldAllowNoToolCallsOnTheLastTurn_AndFailWhenClaudeStillCallsOne()
    {
        using var api = new FakeAnthropicApi(ToolCallResponse);

        var error = AssertError(await Service(api).AskWithModelToolsAsync("What parses?", Schema));

        Assert.Equal("Claude did not answer within the limit of tool calls. Please try again.", error.Message);
        var requests = api.RequestBodies;
        Assert.Equal(ModelToolsAgent.MaxTurns, requests.Count);
        Assert.Contains("\"tool_choice\":{\"type\":\"auto\"", requests[^2]);
        Assert.Contains("\"tool_choice\":{\"type\":\"none\"", requests[^1]);
        Assert.Contains("You have used all your tool calls.", requests[^1]);
        Assert.DoesNotContain("You have used all your tool calls.", requests[^2]);
        // The calls of the last turn are not run: nothing could use their results.
        Assert.Equal(ModelToolsAgent.MaxTurns - 1, tools.Calls.Count);
    }

    [Fact]
    public async Task AskWithModelToolsAsync_ShouldLeaveTheCostUnknown_WhenAnotherModelAnsweredATurn()
    {
        using var api = new FakeAnthropicApi(
            ToolCallResponse,
            AnswerResponse.Replace("\"model\": \"claude-sonnet-5-5\"", "\"model\": \"claude-opus-5-5\"")
        );

        var answer = AssertOk(await Service(api).AskWithModelToolsAsync("What parses?", Schema));

        Assert.Null(answer.CostUsd);
    }

    [Fact]
    public async Task AskWithModelToolsAsync_ShouldFail_WhenTheHostServesNoModelTools()
    {
        var service = new ClaudeService(keys.Object);

        var error = AssertError(await service.AskWithModelToolsAsync("What parses?", Schema));

        Assert.Equal("This host serves no model tools for Claude to look things up with.", error.Message);
    }

    [Fact]
    public async Task AskWithModelToolsAsync_ShouldFail_WithoutAKey_BeforeConnectingTheTools()
    {
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync((string?)null);
        var isConnected = false;
        var service = new ClaudeService(keys.Object)
        {
            ConnectTools = () =>
            {
                isConnected = true;
                return Task.FromResult<Result<IAgentTools>>(tools);
            },
        };

        var error = AssertError(await service.AskWithModelToolsAsync("What parses?", Schema));

        Assert.Equal("No Claude API key. Set a new one.", error.Message);
        Assert.False(isConnected);
    }

    ClaudeService Service(FakeAnthropicApi api) =>
        new(keys.Object) { BaseUrl = api.Url, ConnectTools = () => Task.FromResult<Result<IAgentTools>>(tools) };

    sealed class FakeTools : IAgentTools
    {
        public List<(string Name, string Input)> Calls { get; } = [];
        public Exception? Failure { get; set; }
        public bool IsDisposed { get; private set; }

        public IReadOnlyList<AgentTool> Tools { get; } =
        [
            new(
                "search_nodes",
                "Searches the model's nodes by name.",
                JsonDocument
                    .Parse(
                        """{ "type": "object", "properties": { "query": { "type": "string" } }, "required": ["query"] }"""
                    )
                    .RootElement
            ),
        ];

        public Task<AgentToolResult> CallAsync(string name, JsonElement input)
        {
            Calls.Add((name, input.GetRawText()));
            if (Failure is not null)
                throw Failure;
            return Task.FromResult(new AgentToolResult("Found ParserService", IsError: false));
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
