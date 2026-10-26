using Dependinator.Core.Claude;
using Dependinator.Core.Parsing;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// The review of a dependency that breaks an architecture rule: the prompt that asks an AI whether
// to fix or allow it (with the rule, the dependency, its links and what was allowed before), the
// fix prompt, and the AI's verdict, from structured output or an AI chat's pasted answer.
public class RuleReviewTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());

    static Node AddNode(IModel model, string name, Node parent, NodeType type = NodeType.ClassType)
    {
        var node = new Node(name, parent) { Boundary = new Rect(0, 0, 100, 100), Type = type };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static void AddLink(IModel model, Node source, Node target)
    {
        var link = new Link(source, target);
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        new LineService().AddLinesFromSourceToTarget(model, link);
    }

    // "UI must not use Data", broken by UI.View.Load() calling Data.Repo.Get().
    (ArchitectureRule Rule, RuleViolation Violation) Setup(IModel model)
    {
        var ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
        var data = AddNode(model, "Data", model.Root, NodeType.Assembly);
        data.SetDescription("Database access.");
        var view = AddNode(model, "UI.View", ui);
        var repo = AddNode(model, "Data.Repo", data);
        var load = AddNode(model, "UI.View.Load()", view, NodeType.MethodMember);
        var get = AddNode(model, "Data.Repo.Get()", repo, NodeType.MethodMember);
        AddLink(model, load, get);
        var rule = new ArchitectureRule(ui.Name, data.Name);
        model.AddRule(rule);
        return (rule, Assert.Single(Assert.Single(RuleService.Compute(model).Reports).Violations));
    }

    [Fact]
    public void Build_ShouldDescribeTheRule_TheDependencyWithItsLinks_AndTheAnswerFormat()
    {
        using var model = modelMgr.UseModel();
        var (rule, violation) = Setup(model);
        model.AddAllowedViolation(new AllowedViolation(rule.FromName, rule.ToName, "UI.Other", "Data.Repo", "Legacy"));

        var prompt = RulePrompt.Build(model, rule, violation).ReplaceLineEndings("\n");

        Assert.Contains(
            "## The rule\n\nproject UI (dll) must not use project Data (dll): nothing at or inside",
            prompt
        );
        Assert.Contains("- Data (dll): \"Database access.\"\n", prompt);
        Assert.Contains("- UI.Other → Repo: \"Legacy\"\n", prompt);
        Assert.Contains("## The dependency that breaks it\n\nclass View\nuses class Repo\n\n1 link:\n", prompt);
        Assert.Contains("- View.Load() → Repo (Get)\n", prompt);
        Assert.Contains("Verdict: Fix or Allow", prompt);
        Assert.Contains("This is a review only", prompt);
    }

    [Fact]
    public void BuildFix_ShouldAskToRemoveTheDependency_AsSuggested()
    {
        using var model = modelMgr.UseModel();
        var (rule, violation) = Setup(model);
        var suggestion = new RuleSuggestion(true, "UI should not query.", "Get is pure.", "", "Pass the data in.");

        var prompt = RulePrompt.BuildFix(model, rule, violation, suggestion).ReplaceLineEndings("\n");

        Assert.StartsWith("Please remove a dependency that breaks an architecture rule", prompt);
        Assert.Contains("## The suggested change\n\nPass the data in.\n", prompt);
        Assert.Contains("Assumptions to check first: Get is pure.", prompt);
        Assert.Contains("Dependinator shows whether the rule is kept after its next parse.", prompt);
    }

    [Fact]
    public void Parse_ShouldReadAFixingVerdict_AndDropTheReasonOfAFix()
    {
        var suggestion = AssertOk(
            RuleSuggestion.Parse(
                """{ "verdict": "Fix", "why": "Layering.", "assumptions": "None", "reasonToStore": "x", "change": " Pass it in. " }""",
                AiSource.ClaudeCode
            )
        );

        Assert.True(suggestion.IsFix);
        Assert.Equal("Fix", suggestion.Verdict);
        Assert.Equal("Pass it in.", suggestion.Change);
        Assert.Equal("", suggestion.Reason);
        Assert.Equal(AiSource.ClaudeCode, suggestion.Source);
    }

    [Fact]
    public void ParseText_ShouldReadAnAllowingAnswer_PastedFromAnAiChat()
    {
        var suggestion = AssertOk(
            RuleSuggestion.ParseText(
                """
                **Verdict:** Allow
                **Why:** It only reads a constant.
                **Reason to store:** (only if Allow) "Reads one constant; moving it costs more."
                **Change:** N/A
                """
            )
        );

        Assert.False(suggestion.IsFix);
        Assert.Equal(AiSource.Chat, suggestion.Source);
        Assert.Equal("Reads one constant; moving it costs more.", suggestion.Reason);
        Assert.Equal("", suggestion.Change);
    }

    [Fact]
    public void ParseText_ShouldFail_WithoutAVerdict()
    {
        var error = AssertError(RuleSuggestion.ParseText("Verdict: Accept"));

        Assert.StartsWith("The answer has no \"Verdict: Fix\" or \"Verdict: Allow\" line", error.Message);
    }

    [Fact]
    public async Task SuggestAsync_ShouldAskWithTheRulePromptAndTheAnswerSchema()
    {
        RuleViolation violation;
        ArchitectureRule rule;
        using (var model = modelMgr.UseModel())
            (rule, violation) = Setup(model);
        var claude = new Mock<IClaudeService>();
        claude.Setup(c => c.GetApiKeyStateAsync()).ReturnsAsync(ClaudeApiKeyState.Set);
        claude
            .Setup(c => c.AskAsync(It.Is<string>(p => p.Contains("## The rule")), RuleSuggestion.JsonSchema))
            .ReturnsAsync(
                (Result<string>)
                    """{ "verdict": "Allow", "why": "Fine.", "assumptions": "None", "reasonToStore": "Ok", "change": "" }"""
            );
        var service = new RuleReviewService(modelMgr, Mock.Of<IWorkspaceFileService>(), new AiService(claude.Object));
        List<SuggestStep> steps = [];

        var suggestion = AssertOk(await service.SuggestAsync(rule, violation, AiSource.Claude, steps.Add));

        Assert.False(suggestion.IsFix);
        Assert.Equal("Ok", suggestion.Reason);
        Assert.Equal([SuggestStep.AskingClaude], steps);
    }
}
