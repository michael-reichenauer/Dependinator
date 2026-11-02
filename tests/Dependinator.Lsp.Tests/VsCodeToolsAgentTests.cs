using System.Text.Json;
using Dependinator.Claude;
using Dependinator.Core.Claude;
using Dependinator.Lsp.VsCodeModels;

namespace Dependinator.Lsp.Tests;

// The Copilot provider's tool loop: the model's tool calls run with the model tools, their results
// go back in the next turn, and the answer tool's input is the answer.
public class VsCodeToolsAgentTests
{
    const string Schema = """{"type":"object","properties":{"answer":{"type":"string"}},"required":["answer"]}""";
    const string Answer = """{"answer":"Roslyn parses"}""";

    readonly List<VsCodeTurnParams> turns = [];
    readonly FakeTools tools = new();

    Task<Result<ToolsAnswer>> AskAsync(params VsCodeTurnResult[] results)
    {
        var next = 0;
        return VsCodeToolsAgent.AskAsync(
            turn =>
            {
                turns.Add(turn);
                return Task.FromResult(results[Math.Min(next++, results.Length - 1)]);
            },
            "gpt",
            "What parses?",
            Schema,
            tools
        );
    }

    static VsCodeTurnResult Calls(params (string Id, string Name, string Input)[] calls) =>
        new([.. calls.Select(c => new VsCodeTurnPart(VsCodeTurnPart.ToolCallKind, null, c.Id, c.Name, c.Input))], null);

    static VsCodeTurnResult Text(string text) => new([new VsCodeTurnPart(VsCodeTurnPart.TextKind, text)], null);

    [Fact]
    public async Task AskAsync_ShouldRunTheToolCalls_AndReturnTheAnswerToolsInput()
    {
        var answer = AssertOk(
            await AskAsync(
                Calls(("c1", "search_nodes", """{"query":"Parser"}"""), ("c2", "get_model", "{}")),
                Calls(("c3", "answer", Answer))
            )
        );

        Assert.Equal(Answer, answer.Json);
        Assert.Equal(2, answer.ToolCalls);
        Assert.Null(answer.CostUsd);
        Assert.Equal(["search_nodes:Parser", "get_model:"], tools.Called);
        Assert.Equal(["search_nodes", "get_model"], turns[0].Tools.Select(t => t.Name));
        var results = turns[1].Messages[^1];
        Assert.Equal("user", results.Role);
        Assert.Equal(["c1", "c2"], results.Parts.Select(p => p.CallId));
        Assert.Equal("result of search_nodes", results.Parts[0].Text);
        Assert.Equal("gpt", turns[1].ModelId);
    }

    [Fact]
    public async Task AskAsync_ShouldReadTheAnswerFromText_WhenTheModelWritesTheJson()
    {
        var answer = AssertOk(await AskAsync(Text($"Here it is:\n```json\n{Answer}\n```")));

        Assert.Equal(Answer, answer.Json);
    }

    [Fact]
    public async Task AskAsync_ShouldAskForTheAnswerTool_WhenTheModelAnswersInPlainText()
    {
        AssertOk(await AskAsync(Text("Roslyn parses."), Calls(("c1", "answer", Answer))));

        Assert.Equal(2, turns.Count);
        Assert.Contains("`answer` tool", turns[1].Messages[^1].Parts[0].Text);
    }

    [Fact]
    public async Task AskAsync_ShouldNotTakeAQuotedSnippet_ForTheAnswer()
    {
        AssertOk(await AskAsync(Text("""I will look up {"nodeName":"X"} next."""), Calls(("c1", "answer", Answer))));

        Assert.Equal(2, turns.Count);
    }

    [Fact]
    public async Task AskAsync_ShouldAskForTheAnswerOnlyOnce_WhenTheModelKeepsWritingText()
    {
        var error = AssertError(await AskAsync(Text("Roslyn parses."), Text("As I said, Roslyn parses.")));

        Assert.Equal(2, turns.Count);
        Assert.Contains("required format", error.Message);
    }

    [Fact]
    public async Task AskAsync_ShouldNotSendAnEmptyTurnBack()
    {
        AssertOk(await AskAsync(new VsCodeTurnResult([], null), Calls(("c1", "answer", Answer))));

        Assert.DoesNotContain(turns[1].Messages, m => m.Parts.Length == 0);
    }

    [Fact]
    public async Task AskAsync_ShouldGiveTheModelTheError_WhenAToolFails()
    {
        tools.FailWith = "no such node";

        AssertOk(
            await AskAsync(Calls(("c1", "get_dependencies", """{"nodeName":"X"}""")), Calls(("c2", "answer", Answer)))
        );

        Assert.StartsWith("Error: ", turns[1].Messages[^1].Parts[0].Text);
    }

    [Fact]
    public async Task AskAsync_ShouldAllowOnlyTheAnswerOnTheLastTurn_AndFailWhenThereIsNone()
    {
        var error = AssertError(await AskAsync(Calls(("c1", "get_model", "{}"))));

        Assert.Equal(VsCodeToolsAgent.MaxTurns, turns.Count);
        Assert.True(turns[^1].IsLastTurn);
        Assert.False(turns[^2].IsLastTurn);
        Assert.Contains("limit of tool calls", error.Message);
    }

    [Fact]
    public async Task AskAsync_ShouldReturnTheExtensionsError()
    {
        var error = AssertError(await AskAsync(new VsCodeTurnResult(null, "No permission")));

        Assert.Equal("No permission", error.Message);
    }

    sealed class FakeTools : IAgentTools
    {
        public List<string> Called { get; } = [];
        public string? FailWith { get; set; }

        public IReadOnlyList<AgentTool> Tools { get; } =
        [
            new("search_nodes", "Finds nodes", JsonDocument.Parse("""{"type":"object"}""").RootElement),
            new("get_model", "The model", JsonDocument.Parse("""{"type":"object"}""").RootElement),
        ];

        public Task<AgentToolResult> CallAsync(string name, JsonElement input)
        {
            var first = input.EnumerateObject().Select(p => p.Value.GetString()).FirstOrDefault() ?? "";
            Called.Add($"{name}:{first}");
            return Task.FromResult(
                FailWith is { } message
                    ? new AgentToolResult(message, true)
                    : new AgentToolResult($"result of {name}", false)
            );
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
