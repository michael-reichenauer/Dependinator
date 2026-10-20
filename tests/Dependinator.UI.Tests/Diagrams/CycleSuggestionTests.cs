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

    [Fact]
    public async Task SuggestAsync_ShouldAskClaudeWithTheCyclePromptAndTheAnswerSchema()
    {
        var prompts = new Mock<ICyclePromptService>();
        prompts.Setup(p => p.GetPromptAsync(Cycle)).ReturnsAsync("the prompt");
        var claude = new Mock<IClaudeService>();
        claude
            .Setup(c => c.AskAsync("the prompt", CycleSuggestion.JsonSchema))
            .ReturnsAsync((Result<string>)AcceptAnswer);
        var service = new CycleSuggestionService(prompts.Object, claude.Object);

        var suggestion = AssertOk(await service.SuggestAsync(Cycle));

        Assert.True(suggestion.IsAccept);
    }

    [Fact]
    public async Task SuggestAsync_ShouldPassOnClaudesError()
    {
        var prompts = new Mock<ICyclePromptService>();
        prompts.Setup(p => p.GetPromptAsync(Cycle)).ReturnsAsync("the prompt");
        var claude = new Mock<IClaudeService>();
        claude
            .Setup(c => c.AskAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((Result<string>)new Error("Claude did not accept the API key."));
        var service = new CycleSuggestionService(prompts.Object, claude.Object);

        var error = AssertError(await service.SuggestAsync(Cycle));

        Assert.Equal("Claude did not accept the API key.", error.Message);
    }
}
