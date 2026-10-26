using Dependinator.Core.Claude;

namespace Dependinator.Claude.Tests;

// The experimental local route: ClaudeService runs Claude Code without an API key (the UI offers
// it only when turned on in Settings), and ClaudeCodeRunner reads the answer out of `claude -p`'s
// JSON result.
public class ClaudeCodeTests
{
    const string Schema = """{ "type": "object", "properties": {}, "additionalProperties": false }""";

    readonly Mock<IClaudeApiKeyProvider> keys = new();
    readonly List<string> asked = [];

    ClaudeService Service() =>
        new(keys.Object)
        {
            AskClaudeCode = (prompt, _, _) =>
            {
                asked.Add(prompt);
                return Task.FromResult<Result<string>>("""{"verdict":"Accept"}""");
            },
        };

    [Fact]
    public async Task AskClaudeCodeAsync_ShouldAsk_WithoutAnApiKey()
    {
        var answer = AssertOk(await Service().AskClaudeCodeAsync("Hello", Schema, null));

        Assert.Equal("""{"verdict":"Accept"}""", answer);
        Assert.Equal(["Hello"], asked);
    }

    [Fact]
    public void Arguments_ShouldAllowOnlyReadingTools_InTheCodesFolder()
    {
        var arguments = ClaudeCodeRunner.Arguments("{}", ClaudeCodeRunner.Mode.ReadCode);

        Assert.Equal(
            [
                "-p",
                "--output-format",
                "json",
                "--json-schema",
                "{}",
                "--tools",
                "Read,Grep,Glob",
                "--setting-sources",
                "user",
                "--strict-mcp-config",
                "--append-system-prompt",
                arguments[11],
                "--no-session-persistence",
            ],
            arguments
        );
        Assert.Contains("read and search it", arguments[11]);
    }

    [Fact]
    public void Arguments_ShouldAskForOneStructuredAnswer_WithoutToolsOrASavedSession()
    {
        var arguments = ClaudeCodeRunner.Arguments("{}", ClaudeCodeRunner.Mode.NoTools);

        Assert.Equal(
            ["-p", "--output-format", "json", "--json-schema", "{}", "--tools", "", "--no-session-persistence"],
            arguments
        );
    }

    [Fact]
    public void Arguments_ShouldAllowEditsButNoCommands_WhenFixing()
    {
        var arguments = ClaudeCodeRunner.Arguments("{}", ClaudeCodeRunner.Mode.FixCode);

        Assert.Equal("Read,Grep,Glob,Edit,Write", arguments[arguments.ToList().IndexOf("--tools") + 1]);
        Assert.Equal("acceptEdits", arguments[arguments.ToList().IndexOf("--permission-mode") + 1]);
        Assert.Equal("user", arguments[arguments.ToList().IndexOf("--setting-sources") + 1]);
        Assert.Contains("--strict-mcp-config", arguments);
        Assert.Contains("not run commands", arguments[arguments.ToList().IndexOf("--append-system-prompt") + 1]);
        Assert.DoesNotContain(arguments, a => a.Contains("Bash") || a.Contains("bypassPermissions"));
    }

    [Fact]
    public async Task FixWithClaudeCodeAsync_ShouldRefuseASecondFix_AndStopTheRunningOne()
    {
        var started = new TaskCompletionSource();
        var service = new ClaudeService(keys.Object)
        {
            FixWithClaudeCode = async (_, _, _, ct) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => { });
                return new Error("Claude Code was stopped.");
            },
        };

        var first = service.FixWithClaudeCodeAsync("Fix it", Schema, "/repo");
        await started.Task;
        var second = AssertError(await service.FixWithClaudeCodeAsync("Fix it too", Schema, "/repo"));
        Assert.True(await service.StopClaudeCodeFixAsync());

        Assert.StartsWith("Claude Code is already fixing", second.Message);
        Assert.Equal("Claude Code was stopped.", AssertError(await first).Message);
        Assert.False(await service.StopClaudeCodeFixAsync());
    }

    [Fact]
    public void CountLines_ShouldCountTheChangedFiles()
    {
        Assert.Equal(2, GitStatus.CountLines(" M src/A.cs\n?? src/B.cs\n"));
        Assert.Equal(0, GitStatus.CountLines(""));
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
