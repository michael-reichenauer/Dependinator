using Dependinator.Core.Claude;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Diagrams;

// The Copilot route of IAiService: the VS Code model picked in Settings › AI, with the model tools
// where the place asks for them, and never Claude's API key.
public class AiServiceCopilotTests
{
    readonly Mock<IClaudeService> claude = new();
    readonly Mock<IVsCodeModelService> vsCodeModels = new();
    readonly Mock<IConfigService> config = new();
    readonly List<SuggestStep> steps = [];

    public AiServiceCopilotTests() =>
        config.Setup(c => c.GetAsync()).ReturnsAsync(new Config { CopilotModelId = "gpt-model" });

    AiService Service() => new(claude.Object, vsCodeModels.Object, config.Object, Mock.Of<IModelMgr>());

    [Fact]
    public async Task AskWithModelToolsAsync_ShouldAskTheVsCodeModelWithTheTools_WhenCopilotIsTheRoute()
    {
        vsCodeModels
            .Setup(m => m.AskWithModelToolsAsync("the prompt", "{}", "gpt-model"))
            .ReturnsAsync((Result<ToolsAnswer>)new ToolsAnswer("{\"a\":1}", 3, null));

        var answer = AssertOk(
            await Service()
                .AskWithModelToolsAsync(AiSource.Copilot, () => Task.FromResult("the prompt"), "{}", steps.Add)
        );

        Assert.Equal(3, answer.ToolCalls);
        Assert.Equal([SuggestStep.AskingCopilot], steps);
        claude.Verify(c => c.GetApiKeyStateAsync(), Times.Never);
    }

    [Fact]
    public async Task AskAsync_ShouldUseTheToolLoop_WhenCopilotIsAskedWithModelTools()
    {
        vsCodeModels
            .Setup(m => m.AskWithModelToolsAsync("the prompt", "{}", "gpt-model"))
            .ReturnsAsync((Result<ToolsAnswer>)new ToolsAnswer("{\"a\":1}", 1, null));

        var json = AssertOk(
            await Service()
                .AskAsync(AiSource.Copilot, () => Task.FromResult("the prompt"), "{}", steps.Add, withModelTools: true)
        );

        Assert.Equal("{\"a\":1}", json);
        vsCodeModels.Verify(m => m.AskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task FixWithCopilotAsync_ShouldHandTheFixPromptToCopilotChat()
    {
        vsCodeModels.Setup(m => m.OpenAgentChatAsync("fix it")).ReturnsAsync(Result.Ok);

        AssertOk(await Service().FixWithCopilotAsync("fix it"));

        vsCodeModels.Verify(m => m.OpenAgentChatAsync("fix it"), Times.Once);
    }

    [Fact]
    public async Task FixWithCopilotAsync_ShouldOpenNoChat_WhenThereIsNoPrompt()
    {
        var error = AssertError(await Service().FixWithCopilotAsync(""));

        Assert.Equal(AiService.EmptyPromptError, error.Message);
        vsCodeModels.Verify(m => m.OpenAgentChatAsync(It.IsAny<string>()), Times.Never);
    }
}
