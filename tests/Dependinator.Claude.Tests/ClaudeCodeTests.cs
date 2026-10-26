using Dependinator.Core.Claude;

namespace Dependinator.Claude.Tests;

// The experimental local route: ClaudeService asks Claude Code only when the host has it turned on
// and the user confirmed, and ClaudeCodeRunner reads the answer out of `claude -p`'s JSON result.
public class ClaudeCodeTests
{
    const string Schema = """{ "type": "object", "properties": {}, "additionalProperties": false }""";

    readonly Mock<IClaudeApiKeyProvider> keys = new();
    readonly Mock<IClaudeCodeSettings> settings = new();
    readonly List<string> asked = [];

    public ClaudeCodeTests() => settings.Setup(s => s.EnableHint).Returns("Turn it on.");

    ClaudeService Service() =>
        new(keys.Object, settings.Object)
        {
            AskClaudeCode = (prompt, _) =>
            {
                asked.Add(prompt);
                return Task.FromResult<Result<string>>("""{"verdict":"Accept"}""");
            },
        };

    [Fact]
    public async Task AskClaudeCodeAsync_ShouldFail_WhenTheRouteIsOff()
    {
        settings.Setup(s => s.IsEnabledAsync()).ReturnsAsync(false);

        var error = AssertError(await Service().AskClaudeCodeAsync("Hello", Schema));

        Assert.Equal("Asking Claude Code on this computer is off. Turn it on.", error.Message);
        Assert.Empty(asked);
    }

    [Fact]
    public async Task AskClaudeCodeAsync_ShouldNotAsk_WhenTheUserDeclinesTheConfirmation()
    {
        settings.Setup(s => s.IsEnabledAsync()).ReturnsAsync(true);
        settings.Setup(s => s.ConfirmUseAsync()).ReturnsAsync((Result)new Error("Not confirmed."));

        var error = AssertError(await Service().AskClaudeCodeAsync("Hello", Schema));

        Assert.Equal("Not confirmed.", error.Message);
        Assert.Empty(asked);
    }

    [Fact]
    public async Task AskClaudeCodeAsync_ShouldAsk_WhenOnAndConfirmed()
    {
        settings.Setup(s => s.IsEnabledAsync()).ReturnsAsync(true);
        settings.Setup(s => s.ConfirmUseAsync()).ReturnsAsync(Result.Ok);

        var answer = AssertOk(await Service().AskClaudeCodeAsync("Hello", Schema));

        Assert.Equal("""{"verdict":"Accept"}""", answer);
        Assert.Equal(["Hello"], asked);
    }

    [Fact]
    public void Arguments_ShouldAskForOneStructuredAnswer_WithoutToolsOrASavedSession()
    {
        var arguments = ClaudeCodeRunner.Arguments("{}");

        Assert.Equal(
            ["-p", "--output-format", "json", "--json-schema", "{}", "--tools", "", "--no-session-persistence"],
            arguments
        );
    }

    [Fact]
    public void ParseOutput_ShouldReturnTheStructuredOutput()
    {
        var output = """
            {"type":"result","subtype":"success","is_error":false,"result":"{\"verdict\":\"Break\"}",
             "structured_output":{"verdict":"Break","why":"Two concepts."}}
            """;

        var answer = AssertOk(ClaudeCodeRunner.ParseOutput(output, "", 0));

        Assert.Equal("""{"verdict":"Break","why":"Two concepts."}""", answer);
    }

    [Fact]
    public void ParseOutput_ShouldFallBackToAJsonResult_WithoutStructuredOutput()
    {
        var output = """{"type":"result","is_error":false,"result":" {\"verdict\":\"Accept\"} "}""";

        Assert.Equal("""{"verdict":"Accept"}""", AssertOk(ClaudeCodeRunner.ParseOutput(output, "", 0)));
    }

    [Fact]
    public void ParseOutput_ShouldTellWhatWentWrong_WhenClaudeCodeReportsAnError()
    {
        var output = """{"type":"result","is_error":true,"result":"Not logged in · Please run /login"}""";

        var error = AssertError(ClaudeCodeRunner.ParseOutput(output, "", 1));

        Assert.Equal("Claude Code could not answer: Not logged in · Please run /login", error.Message);
    }

    [Fact]
    public void ParseOutput_ShouldUseTheErrorOutput_WhenThereIsNoJson()
    {
        var error = AssertError(ClaudeCodeRunner.ParseOutput("", "error: unknown option '--json-schema'\n", 1));

        Assert.Equal("Claude Code could not answer: error: unknown option '--json-schema'", error.Message);
    }
}
