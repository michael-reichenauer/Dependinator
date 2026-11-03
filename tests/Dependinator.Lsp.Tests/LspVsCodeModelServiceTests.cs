using Dependinator.Core.ModelQueries;
using Dependinator.Core.VsCodeModels;
using Dependinator.Lsp.VsCodeModels;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Dependinator.Lsp.Tests;

// The Copilot provider's requests to the extension (languageModels.ts): what the UI gets back for
// each answer the extension gives, or when it gives none.
public class LspVsCodeModelServiceTests
{
    readonly Mock<ILanguageServerFacade> server = new();

    LspVsCodeModelService CreateSut() => new(server.Object, new ModelToolsServer());

    void SetupAsk(Func<Task<VsCodeAskResult>> answer)
    {
        Mock<IResponseRouterReturns> returns = new();
        returns.Setup(r => r.Returning<VsCodeAskResult>(It.IsAny<CancellationToken>())).Returns(answer);
        server
            .Setup(s =>
                s.SendRequest(
                    VsCodeAskParams.Method,
                    It.Is<VsCodeAskParams>(p => p.Prompt == "the prompt" && p.JsonSchema == "{}" && p.ModelId == "gpt")
                )
            )
            .Returns(returns.Object);
    }

    void SetupModels(Func<Task<VsCodeModelsResult>> answer)
    {
        Mock<IResponseRouterReturns> returns = new();
        returns.Setup(r => r.Returning<VsCodeModelsResult>(It.IsAny<CancellationToken>())).Returns(answer);
        server
            .Setup(s => s.SendRequest(VsCodeModelsParams.Method, It.IsAny<VsCodeModelsParams>()))
            .Returns(returns.Object);
    }

    void SetupOpenChat(Func<Task<VsCodeOpenChatResult>> answer)
    {
        Mock<IResponseRouterReturns> returns = new();
        returns.Setup(r => r.Returning<VsCodeOpenChatResult>(It.IsAny<CancellationToken>())).Returns(answer);
        server
            .Setup(s =>
                s.SendRequest(VsCodeOpenChatParams.Method, It.Is<VsCodeOpenChatParams>(p => p.Prompt == "fix it"))
            )
            .Returns(returns.Object);
    }

    [Fact]
    public async Task OpenAgentChatAsync_ShouldSucceed_WhenTheExtensionOpenedTheChat()
    {
        SetupOpenChat(() => Task.FromResult(new VsCodeOpenChatResult(null)));

        AssertOk(await CreateSut().OpenAgentChatAsync("fix it"));
    }

    [Fact]
    public async Task OpenAgentChatAsync_ShouldReturnTheExtensionsError_WhenTheChatDidNotOpen()
    {
        SetupOpenChat(() => Task.FromResult(new VsCodeOpenChatResult("No chat")));

        var error = AssertError(await CreateSut().OpenAgentChatAsync("fix it"));

        Assert.Equal("No chat", error.Message);
    }

    [Fact]
    public async Task OpenAgentChatAsync_ShouldReturnAnError_WhenTheRequestFails()
    {
        SetupOpenChat(() => Task.FromException<VsCodeOpenChatResult>(new IOException("gone")));

        var error = AssertError(await CreateSut().OpenAgentChatAsync("fix it"));

        Assert.Contains("gone", error.Message);
    }

    [Fact]
    public async Task AskAsync_ShouldReturnTheJson_WhenTheModelAnswered()
    {
        SetupAsk(() => Task.FromResult(new VsCodeAskResult("{\"a\":1}", null)));

        var json = AssertOk(await CreateSut().AskAsync("the prompt", "{}", "gpt"));

        Assert.Equal("{\"a\":1}", json);
    }

    [Fact]
    public async Task AskAsync_ShouldReturnTheExtensionsError_WhenThereIsNoAnswer()
    {
        SetupAsk(() => Task.FromResult(new VsCodeAskResult(null, "No permission")));

        var error = AssertError(await CreateSut().AskAsync("the prompt", "{}", "gpt"));

        Assert.Equal("No permission", error.Message);
    }

    [Fact]
    public async Task AskAsync_ShouldReturnAnError_WhenTheRequestFails()
    {
        SetupAsk(() => Task.FromException<VsCodeAskResult>(new IOException("gone")));

        var error = AssertError(await CreateSut().AskAsync("the prompt", "{}", "gpt"));

        Assert.Contains("gone", error.Message);
    }

    [Fact]
    public async Task GetModelsAsync_ShouldListTheExtensionsModels()
    {
        SetupModels(() =>
            Task.FromResult(new VsCodeModelsResult([new VsCodeModelInfo("gpt", "GPT", "copilot", "gpt-5", 128000)]))
        );

        var models = await CreateSut().GetModelsAsync();

        Assert.Equal([new VsCodeModel("gpt", "GPT", "copilot", "gpt-5", 128000)], models);
    }

    [Fact]
    public async Task GetModelsAsync_ShouldBeEmpty_WhenTheRequestFails()
    {
        SetupModels(() => Task.FromException<VsCodeModelsResult>(new IOException("no handler")));

        Assert.Empty(await CreateSut().GetModelsAsync());
    }

    [Fact]
    public async Task AskWithModelToolsAsync_ShouldFail_BeforeTheModelToolsServerHasStarted()
    {
        var error = AssertError(await CreateSut().AskWithModelToolsAsync("the prompt", "{}", "gpt"));

        Assert.Contains("model tools are not ready", error.Message);
        server.VerifyNoOtherCalls();
    }
}
