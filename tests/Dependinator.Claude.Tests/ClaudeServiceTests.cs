using Dependinator.Claude;

namespace Dependinator.Claude.Tests;

// What ClaudeService does before it sends anything: find the user's API key, or ask for one,
// and read the answer schema. (Requests to Claude itself need a key and the network, so they are
// tried by hand.)
public class ClaudeServiceTests
{
    const string Schema = """{ "type": "object", "properties": {}, "additionalProperties": false }""";

    [Fact]
    public async Task AskAsync_ShouldAskTheUserForAKey_AndStop_WhenNoneIsGiven()
    {
        var keys = new Mock<IClaudeApiKeyProvider>();
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync((string?)null);
        keys.Setup(k => k.AskUserForApiKeyAsync()).ReturnsAsync((Result<string>)new Error("No key was entered."));
        var service = new ClaudeService(keys.Object);

        var error = AssertError(await service.AskAsync("Hello", Schema));

        Assert.Equal("No key was entered.", error.Message);
        keys.Verify(k => k.AskUserForApiKeyAsync(), Times.Once);
    }

    [Fact]
    public async Task AskAsync_ShouldRefuseASchemaThatIsNotJson_BeforeAskingClaude()
    {
        var keys = new Mock<IClaudeApiKeyProvider>();
        keys.Setup(k => k.GetApiKeyAsync()).ReturnsAsync("sk-ant-test");
        var service = new ClaudeService(keys.Object);

        var error = AssertError(await service.AskAsync("Hello", "not json"));

        Assert.Equal("The answer schema is not valid JSON.", error.Message);
        keys.Verify(k => k.AskUserForApiKeyAsync(), Times.Never);
    }
}
