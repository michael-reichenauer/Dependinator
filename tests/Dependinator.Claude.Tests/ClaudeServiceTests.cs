using System.Net;
using Dependinator.Core.Claude;

namespace Dependinator.Claude.Tests;

// ClaudeService: the state of the user's API key, asking the user for one, and a request to
// Claude, run through the SDK against a local stand-in for Anthropic's API.
public class ClaudeServiceTests
{
    const string Schema = """{ "type": "object", "properties": {}, "additionalProperties": false }""";

    readonly Mock<IClaudeApiKeyProvider> keys = new();

    public ClaudeServiceTests() => keys.Setup(k => k.SetKeyHint).Returns("Set a new one.");

    [Fact]
    public async Task GetApiKeyStateAsync_ShouldTellAMissingKeyFromAStoredOne()
    {
        var service = new ClaudeService(keys.Object);

        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync((string?)null);
        Assert.Equal(ClaudeApiKeyState.Missing, await service.GetApiKeyStateAsync());

        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-test");
        Assert.Equal(ClaudeApiKeyState.Set, await service.GetApiKeyStateAsync());
    }

    [Fact]
    public async Task AskUserForApiKeyAsync_ShouldPassOnWhetherTheUserGaveOne()
    {
        var service = new ClaudeService(keys.Object);

        keys.Setup(k => k.AskUserForApiKeyAsync()).ReturnsAsync((Result<string>)"sk-ant-test");
        AssertOk(await service.AskUserForApiKeyAsync());

        keys.Setup(k => k.AskUserForApiKeyAsync()).ReturnsAsync((Result<string>)new Error("No key was entered."));
        Assert.Equal("No key was entered.", AssertError(await service.AskUserForApiKeyAsync()).Message);
    }

    [Fact]
    public async Task AskAsync_ShouldFail_WithoutAKey_AndLeaveAskingTheUserToTheCaller()
    {
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync((string?)null);
        var service = new ClaudeService(keys.Object);

        var error = AssertError(await service.AskAsync("Hello", Schema));

        Assert.Equal("No Claude API key. Set a new one.", error.Message);
        keys.Verify(k => k.AskUserForApiKeyAsync(), Times.Never);
    }

    [Fact]
    public async Task AskAsync_ShouldRefuseASchemaThatIsNotJson()
    {
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-test");
        var service = new ClaudeService(keys.Object);

        var error = AssertError(await service.AskAsync("Hello", "not json"));

        Assert.Equal("The answer schema is not valid JSON.", error.Message);
    }

    [Fact]
    public async Task AskAsync_ShouldMarkTheKeyRejected_WhenClaudeDoesNotAcceptIt_UntilAnotherKeyIsSet()
    {
        using var api = new FakeAnthropicApi(
            HttpStatusCode.Unauthorized,
            """{ "type": "error", "error": { "type": "authentication_error", "message": "invalid x-api-key" } }"""
        );
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-wrong");
        var service = new ClaudeService(keys.Object) { BaseUrl = api.Url };

        var error = AssertError(await service.AskAsync("Hello", Schema));

        Assert.Equal("Claude did not accept the API key. Set a new one.", error.Message);
        Assert.Equal(ClaudeApiKeyState.Rejected, await service.GetApiKeyStateAsync());

        // No new key given (or the host cannot ask): the error says why one is needed.
        keys.Setup(k => k.AskUserForApiKeyAsync()).ReturnsAsync((Result<string>)new Error("No key was entered."));
        Assert.Equal(
            "Claude did not accept the API key. Set a new one.",
            AssertError(await service.AskUserForApiKeyAsync()).Message
        );

        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-new");
        Assert.Equal(ClaudeApiKeyState.Set, await service.GetApiKeyStateAsync());
    }

    [Fact]
    public async Task AskAsync_ShouldSendThePromptWithTheSchema_AndReturnClaudesText()
    {
        using var api = new FakeAnthropicApi(
            HttpStatusCode.OK,
            """
            {
              "id": "msg_test", "type": "message", "role": "assistant", "model": "claude-opus-5-5",
              "content": [{ "type": "text", "text": "{ \"verdict\": \"Accept\" }" }],
              "stop_reason": "end_turn", "stop_sequence": null,
              "usage": { "input_tokens": 12, "output_tokens": 7 }
            }
            """
        );
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-test");
        var service = new ClaudeService(keys.Object) { BaseUrl = api.Url };

        var text = AssertOk(await service.AskAsync("Is this cycle fine?", Schema));

        Assert.Equal("""{ "verdict": "Accept" }""", text);
        var request = api.LastRequestBody!;
        Assert.Contains("\"model\":\"claude-opus-5-5\"", request);
        Assert.Contains("Is this cycle fine?", request);
        Assert.Contains("\"json_schema\"", request);
        Assert.Contains("\"fallbacks\":\"default\"", request);
    }
}
