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

        var error = AssertError(await service.AskAsync("Hello", Schema, null));

        Assert.Equal("No Claude API key. Set a new one.", error.Message);
        keys.Verify(k => k.AskUserForApiKeyAsync(), Times.Never);
    }

    [Fact]
    public async Task AskAsync_ShouldRefuseASchemaThatIsNotJson()
    {
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-test");
        var service = new ClaudeService(keys.Object);

        var error = AssertError(await service.AskAsync("Hello", "not json", null));

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

        var error = AssertError(await service.AskAsync("Hello", Schema, null));

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
    public async Task AskAsync_ShouldSaySo_WhenTheAccountHasNoCreditsLeft()
    {
        using var api = new FakeAnthropicApi(
            HttpStatusCode.BadRequest,
            """
            { "type": "error", "error": { "type": "invalid_request_error",
              "message": "Your credit balance is too low to access the Anthropic API. Please go to Plans & Billing to upgrade or purchase credits." } }
            """
        );
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-test");
        var service = new ClaudeService(keys.Object) { BaseUrl = api.Url };

        var error = AssertError(await service.AskAsync("Hello", Schema, null));

        Assert.Equal(
            "Your Anthropic account has no credits left. Add some under Billing at console.anthropic.com.",
            error.Message
        );
    }

    [Fact]
    public async Task AskAsync_ShouldPassOnTheApisOwnMessage_WithoutItsStatusAndJson()
    {
        using var api = new FakeAnthropicApi(
            HttpStatusCode.BadRequest,
            """{ "type": "error", "error": { "type": "invalid_request_error", "message": "prompt is too long" } }"""
        );
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-test");
        var service = new ClaudeService(keys.Object) { BaseUrl = api.Url };

        var error = AssertError(await service.AskAsync("Hello", Schema, null));

        Assert.Equal("Claude could not answer: prompt is too long", error.Message);
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

        var text = AssertOk(await service.AskAsync("Is this cycle fine?", Schema, null));

        Assert.Equal("""{ "verdict": "Accept" }""", text);
        var request = api.LastRequestBody!;
        Assert.Contains("\"model\":\"claude-opus-5-5\"", request);
        Assert.Contains("""{"role":"user","content":"Is this cycle fine?"}""", request);
        Assert.Contains("\"json_schema\"", request);
        Assert.Contains("\"fallbacks\":\"default\"", request);
    }

    [Fact]
    public async Task AskQuicklyAsync_ShouldAskTheFasterModel()
    {
        using var api = new FakeAnthropicApi(
            HttpStatusCode.OK,
            """
            {
              "id": "msg_test", "type": "message", "role": "assistant", "model": "claude-sonnet-5-5",
              "content": [{ "type": "text", "text": "{ \"answer\": \"A parser.\" }" }],
              "stop_reason": "end_turn", "stop_sequence": null,
              "usage": { "input_tokens": 12, "output_tokens": 7 }
            }
            """
        );
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-test");
        var service = new ClaudeService(keys.Object) { BaseUrl = api.Url };

        var text = AssertOk(await service.AskQuicklyAsync("What is this?", Schema, null));

        Assert.Equal("""{ "answer": "A parser." }""", text);
        Assert.Contains("\"model\":\"claude-sonnet-5-5\"", api.LastRequestBody!);
    }

    const string FableAnswer = """
        {
          "id": "msg_test", "type": "message", "role": "assistant", "model": "claude-fable-5-2",
          "content": [{ "type": "text", "text": "{ \"verdict\": \"Accept\" }" }],
          "stop_reason": "end_turn", "stop_sequence": null,
          "usage": { "input_tokens": 12, "output_tokens": 7 }
        }
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Asks_ShouldAskThePickedModel(bool isQuick)
    {
        using var api = new FakeAnthropicApi(HttpStatusCode.OK, FableAnswer);
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-test");
        var service = new ClaudeService(keys.Object) { BaseUrl = api.Url };

        AssertOk(
            isQuick
                ? await service.AskQuicklyAsync("Hello", Schema, " claude-fable-5-2 ")
                : await service.AskAsync("Hello", Schema, " claude-fable-5-2 ")
        );

        Assert.Contains("\"model\":\"claude-fable-5-2\"", api.LastRequestBody!);
        Assert.Contains("\"fallbacks\":\"default\"", api.LastRequestBody!);
    }

    [Fact]
    public async Task AskAsync_ShouldSendNoFallback_ToAModelWithoutOne()
    {
        using var api = new FakeAnthropicApi(HttpStatusCode.OK, FableAnswer);
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-test");
        var service = new ClaudeService(keys.Object) { BaseUrl = api.Url };

        AssertOk(await service.AskAsync("Hello", Schema, "claude-haiku-5-5"));

        Assert.Contains("\"model\":\"claude-haiku-5-5\"", api.LastRequestBody!);
        Assert.DoesNotContain("fallbacks", api.LastRequestBody!);
    }

    [Fact]
    public async Task AskAsync_ShouldPointToTheSettings_WhenClaudeDoesNotKnowThePickedModel()
    {
        using var api = new FakeAnthropicApi(
            HttpStatusCode.NotFound,
            """{ "type": "error", "error": { "type": "not_found_error", "message": "model: claude-gone-1" } }"""
        );
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-test");
        var service = new ClaudeService(keys.Object) { BaseUrl = api.Url };

        var error = AssertError(await service.AskAsync("Hello", Schema, "claude-gone-1"));

        Assert.Equal(
            "Claude has no model \"claude-gone-1\" for this API key. Pick another model in Settings › AI.",
            error.Message
        );
    }

    [Fact]
    public async Task AskAsync_ShouldNameThePickedModel_WhenClaudeCannotAnswerWithIt()
    {
        using var api = new FakeAnthropicApi(
            HttpStatusCode.BadRequest,
            """{ "type": "error", "error": { "type": "invalid_request_error", "message": "Not available here." } }"""
        );
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-test");
        var service = new ClaudeService(keys.Object) { BaseUrl = api.Url };

        var picked = AssertError(await service.AskAsync("Hello", Schema, "claude-fable-5-2"));
        var automatic = AssertError(await service.AskAsync("Hello", Schema, null));

        Assert.Equal(
            "Claude could not answer with the model \"claude-fable-5-2\" picked in Settings › AI: Not available here.",
            picked.Message
        );
        Assert.Equal("Claude could not answer: Not available here.", automatic.Message);
    }

    [Fact]
    public async Task GetModelsAsync_ShouldListTheModelsThatCanAnswerTheWayTheAppAsks()
    {
        const string Can = """
            "capabilities": { "structured_outputs": { "supported": true },
              "effort": { "supported": true, "low": { "supported": true }, "medium": { "supported": true },
                "high": { "supported": true }, "xhigh": { "supported": true }, "max": { "supported": true } } }
            """;
        const string Cannot = """
            "capabilities": { "structured_outputs": { "supported": true },
              "effort": { "supported": false, "low": { "supported": false }, "medium": { "supported": false },
                "high": { "supported": false }, "xhigh": { "supported": false }, "max": { "supported": false } } }
            """;
        using var api = new FakeAnthropicApi(
            HttpStatusCode.OK,
            $$"""
            {
              "data": [
                { "type": "model", "id": "claude-opus-5-5", "display_name": "Claude Opus 5.5",
                  "created_at": "2026-09-01T00:00:00Z", "max_input_tokens": 1000000, "max_tokens": 128000, {{Can}} },
                { "type": "model", "id": "claude-haiku-4-5-20251001", "display_name": "Claude Haiku 4.5",
                  "created_at": "2025-10-01T00:00:00Z", "max_input_tokens": 200000, "max_tokens": 64000, {{Cannot}} }
              ],
              "has_more": false, "first_id": "claude-opus-5-5", "last_id": "claude-haiku-4-5-20251001"
            }
            """
        );
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-test");
        var service = new ClaudeService(keys.Object) { BaseUrl = api.Url };

        var models = AssertOk(await service.GetModelsAsync());

        Assert.Equal([new ClaudeModel("claude-opus-5-5", "Claude Opus 5.5")], models);
    }

    [Fact]
    public async Task GetModelsAsync_ShouldFail_WithoutAKey()
    {
        keys.Setup(k => k.SetKeyHint).Returns("Set one.");
        var service = new ClaudeService(keys.Object);

        var error = AssertError(await service.GetModelsAsync());

        Assert.Equal("No Claude API key. Set one.", error.Message);
    }

    [Theory]
    [InlineData("claude-opus-5-5", 4.00)]
    [InlineData("claude-opus-5", 5.00)]
    [InlineData("claude-sonnet-4-6-20260115", 3.00)]
    public void PriceOf_ShouldKnowAModelByItsId_WithOrWithoutADate(string modelId, double inputPrice)
    {
        Assert.Equal((decimal)inputPrice, ClaudeModels.PriceOf(modelId)?.Input);
    }

    [Theory]
    [InlineData("claude-haiku-5-5")]
    [InlineData("claude-opus-5-5-fast")]
    [InlineData("claude-next-9")]
    public void PriceOf_ShouldNotGuessAPrice(string modelId)
    {
        Assert.Null(ClaudeModels.PriceOf(modelId));
    }
}
