using Dependinator.Core.Claude;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

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

    [Fact]
    public void ParseText_ShouldReadThePromptsAnswerFormat_AsPastedFromAnAiChat()
    {
        var suggestion = AssertOk(
            CycleSuggestion.ParseText(
                """
                Here is my assessment.

                **Verdict:** Accept
                **Why:** Result and Result<T> are one union.
                **Assumptions:** None
                **Reason to store:** (only if Accept) "One union, in non-generic and generic form."
                **Change:** N/A

                Let me know if you want more detail.
                """
            )
        );

        Assert.True(suggestion.IsAccept);
        Assert.True(suggestion.IsPasted);
        Assert.Equal("Result and Result<T> are one union.", suggestion.Why);
        Assert.Equal("None", suggestion.Assumptions);
        Assert.Equal("One union, in non-generic and generic form.", suggestion.Reason);
        Assert.Equal("", suggestion.Change);
    }

    [Fact]
    public void ParseText_ShouldReadAChangeOverSeveralLines_AndLeaveOutTheReasonOfABreak()
    {
        var suggestion = AssertOk(
            CycleSuggestion.ParseText(
                """
                - Verdict: break
                - Why: One misplaced class.
                - Reason to store: -
                - Change: Remove the Utils → Parsing direction:
                  1. Move Ext into Parsing.
                  2. Import it in UI.
                """
            )
        );

        Assert.False(suggestion.IsAccept);
        Assert.Equal("", suggestion.Reason);
        Assert.Equal(
            "Remove the Utils → Parsing direction:\n  1. Move Ext into Parsing.\n  2. Import it in UI.",
            suggestion.Change.ReplaceLineEndings("\n")
        );
    }

    [Fact]
    public void ParseText_ShouldTakeTheVerdictWordThatComesFirst()
    {
        var suggestion = AssertOk(CycleSuggestion.ParseText("Verdict: Break (do not accept it)\nChange: Move it."));

        Assert.False(suggestion.IsAccept);
    }

    [Fact]
    public void ParseText_ShouldFail_WithoutAVerdict()
    {
        var error = AssertError(
            CycleSuggestion.ParseText("I think this cycle is fine because the types belong together.")
        );

        Assert.StartsWith("The answer has no \"Verdict: Accept\" or \"Verdict: Break\" line", error.Message);
    }

    readonly Mock<ICyclePromptService> prompts = new();
    readonly Mock<IClaudeService> claude = new();
    readonly List<SuggestStep> steps = [];

    public CycleSuggestionTests() => prompts.Setup(p => p.GetPromptAsync(Cycle)).ReturnsAsync("the prompt");

    Task<Result<CycleSuggestion>> SuggestAsync(AiSource route = AiSource.Claude) =>
        new CycleSuggestionService(
            prompts.Object,
            new AiService(claude.Object, Mock.Of<IConfigService>())
        ).SuggestAsync(Cycle, route, steps.Add);

    [Fact]
    public async Task SuggestAsync_ShouldAskClaudeCode_WithoutAnApiKey_WhenThatRouteIsChosen()
    {
        claude
            .Setup(c => c.AskClaudeCodeAsync("the prompt", CycleSuggestion.JsonSchema))
            .ReturnsAsync((Result<string>)AcceptAnswer);

        var suggestion = AssertOk(await SuggestAsync(AiSource.ClaudeCode));

        Assert.True(suggestion.IsAccept);
        Assert.Equal(AiSource.ClaudeCode, suggestion.Source);
        Assert.Equal([SuggestStep.AskingClaudeCode], steps);
        claude.Verify(c => c.GetApiKeyStateAsync(), Times.Never);
        claude.Verify(c => c.AskAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

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
