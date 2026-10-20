using Dependinator.Core.Claude;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Tests.Diagrams;

// Claude's verdict on a cycle: read from the JSON its structured output returns, and asked for
// with the same prompt the user can copy.
public class CycleSuggestionTests
{
    const string AcceptAnswer = """
        {
          "verdict": "Accept",
          "why": " Result and Result<T> are one union. ",
          "assumptions": "None",
          "reasonToStore": "One union, in non-generic and generic form.",
          "change": ""
        }
        """;

    static readonly Cycle Cycle = new(
        NodeId.FromName("C"),
        "C",
        "C",
        [new CycleMember(NodeId.FromName("C.A"), "A", "C.A"), new CycleMember(NodeId.FromName("C.B"), "B", "C.B")]
    );

    [Fact]
    public void Parse_ShouldReadAnAcceptingVerdict_WithTheReasonToStore()
    {
        var suggestion = AssertOk(CycleSuggestion.Parse(AcceptAnswer));

        Assert.True(suggestion.IsAccept);
        Assert.Equal("Result and Result<T> are one union.", suggestion.Why);
        Assert.Equal("One union, in non-generic and generic form.", suggestion.Reason);
    }

    [Fact]
    public void Parse_ShouldReadABreakingVerdict_WithTheChange()
    {
        var suggestion = AssertOk(
            CycleSuggestion.Parse(
                """{ "verdict": "Break", "why": "One misplaced class.", "assumptions": "UI only aliases Parsing.", "reasonToStore": "", "change": "Move Ext into Parsing." }"""
            )
        );

        Assert.False(suggestion.IsAccept);
        Assert.Equal("Move Ext into Parsing.", suggestion.Change);
        Assert.Equal("UI only aliases Parsing.", suggestion.Assumptions);
    }

    [Fact]
    public void Parse_ShouldFail_OnAnAnswerThatIsNotJson()
    {
        var error = AssertError(CycleSuggestion.Parse("Verdict: Accept"));

        Assert.Equal("Claude's answer could not be read.", error.Message);
    }

    readonly Mock<ICyclePromptService> prompts = new();
    readonly Mock<IClaudeService> claude = new();
    readonly List<SuggestStep> steps = [];

    public CycleSuggestionTests() => prompts.Setup(p => p.GetPromptAsync(Cycle)).ReturnsAsync("the prompt");

    Task<Result<CycleSuggestion>> SuggestAsync() =>
        new CycleSuggestionService(prompts.Object, claude.Object).SuggestAsync(Cycle, steps.Add);

    [Fact]
    public async Task SuggestAsync_ShouldAskClaudeWithTheCyclePromptAndTheAnswerSchema()
    {
        claude.Setup(c => c.GetApiKeyStateAsync()).ReturnsAsync(ClaudeApiKeyState.Set);
        claude
            .Setup(c => c.AskAsync("the prompt", CycleSuggestion.JsonSchema))
            .ReturnsAsync((Result<string>)AcceptAnswer);

        var suggestion = AssertOk(await SuggestAsync());

        Assert.True(suggestion.IsAccept);
        Assert.Equal([SuggestStep.AskingClaude], steps);
        claude.Verify(c => c.AskUserForApiKeyAsync(), Times.Never);
    }

    [Fact]
    public async Task SuggestAsync_ShouldAskForAKeyFirst_WhenNoneIsSet_AndSayWhileWaiting()
    {
        claude.Setup(c => c.GetApiKeyStateAsync()).ReturnsAsync(ClaudeApiKeyState.Missing);
        claude.Setup(c => c.AskUserForApiKeyAsync()).ReturnsAsync(Result.Ok);
        claude
            .Setup(c => c.AskAsync("the prompt", CycleSuggestion.JsonSchema))
            .ReturnsAsync((Result<string>)AcceptAnswer);

        AssertOk(await SuggestAsync());

        Assert.Equal([SuggestStep.EnteringApiKey, SuggestStep.AskingClaude], steps);
    }

    [Fact]
    public async Task SuggestAsync_ShouldStop_WhenTheUserEntersNoKey()
    {
        claude.Setup(c => c.GetApiKeyStateAsync()).ReturnsAsync(ClaudeApiKeyState.Missing);
        claude.Setup(c => c.AskUserForApiKeyAsync()).ReturnsAsync((Result)new Error("No Claude API key was entered."));

        var error = AssertError(await SuggestAsync());

        Assert.Equal("No Claude API key was entered.", error.Message);
        claude.Verify(c => c.AskAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SuggestAsync_ShouldAskForANewKey_WhenClaudeDoesNotAcceptTheStoredOne_AndTryAgain()
    {
        claude
            .SetupSequence(c => c.GetApiKeyStateAsync())
            .ReturnsAsync(ClaudeApiKeyState.Set)
            .ReturnsAsync(ClaudeApiKeyState.Rejected)
            .ReturnsAsync(ClaudeApiKeyState.Rejected);
        claude.Setup(c => c.AskUserForApiKeyAsync()).ReturnsAsync(Result.Ok);
        claude
            .SetupSequence(c => c.AskAsync("the prompt", CycleSuggestion.JsonSchema))
            .ReturnsAsync((Result<string>)new Error("Claude did not accept the API key."))
            .ReturnsAsync((Result<string>)AcceptAnswer);

        AssertOk(await SuggestAsync());

        Assert.Equal([SuggestStep.AskingClaude, SuggestStep.EnteringNewApiKey, SuggestStep.AskingClaude], steps);
        claude.Verify(c => c.AskUserForApiKeyAsync(), Times.Once);
    }

    [Fact]
    public async Task SuggestAsync_ShouldAskForANewKeyOnlyOnce_WhenThatOneIsNotAcceptedEither()
    {
        claude.Setup(c => c.GetApiKeyStateAsync()).ReturnsAsync(ClaudeApiKeyState.Rejected);
        claude.Setup(c => c.AskUserForApiKeyAsync()).ReturnsAsync(Result.Ok);
        claude
            .Setup(c => c.AskAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((Result<string>)new Error("Claude did not accept the API key."));

        var error = AssertError(await SuggestAsync());

        Assert.Equal("Claude did not accept the API key.", error.Message);
        Assert.Equal([SuggestStep.EnteringNewApiKey, SuggestStep.AskingClaude], steps);
    }

    [Fact]
    public async Task SuggestAsync_ShouldPassOnOtherErrors_WithoutAskingForAKey()
    {
        claude.Setup(c => c.GetApiKeyStateAsync()).ReturnsAsync(ClaudeApiKeyState.Set);
        claude
            .Setup(c => c.AskAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((Result<string>)new Error("Could not reach Claude."));

        var error = AssertError(await SuggestAsync());

        Assert.Equal("Could not reach Claude.", error.Message);
        claude.Verify(c => c.AskUserForApiKeyAsync(), Times.Never);
    }
}
