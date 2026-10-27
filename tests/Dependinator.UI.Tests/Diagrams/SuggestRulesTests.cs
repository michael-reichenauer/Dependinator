using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using MudBlazor;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// Suggest rules: the prompt lists the model's containers and the dependencies between them, an
// answer is read from structured output or a pasted chat answer, and Dependinator checks each
// suggested rule on the model itself.
public class SuggestRulesTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly SuggestRulesService service;

    // UI (assembly) with UI.Views.View, Data (assembly) with Data.Repo; View uses Repo twice.
    public SuggestRulesTests()
    {
        var ruleService = new RuleService(modelMgr, Mock.Of<ICommandService>());
        var queries = new ModelQueries(
            modelMgr,
            new NodeSearchService(modelMgr),
            new CycleService(modelMgr, Mock.Of<ICommandService>()),
            ruleService,
            new ModelQueryService()
        );
        service = new SuggestRulesService(
            modelMgr,
            queries,
            ruleService,
            Mock.Of<IAiService>(),
            Mock.Of<IApplicationEvents>(),
            Mock.Of<IDialogService>()
        );

        using var model = modelMgr.UseModel();
        var ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
        var views = AddNode(model, "UI.Views", ui, NodeType.Namespace);
        var view = AddNode(model, "UI.Views.View", views);
        var load = AddNode(model, "UI.Views.View.Load()", view, NodeType.MethodMember);
        var data = AddNode(model, "Data", model.Root, NodeType.Assembly);
        var repo = AddNode(model, "Data.Repo", data);
        var get = AddNode(model, "Data.Repo.Get()", repo, NodeType.MethodMember);
        AddLink(model, view, repo);
        AddLink(model, load, get);
    }

    [Fact]
    public void GetPrompt_ShouldListTheContainers_AndTheDependenciesBetweenThem()
    {
        var prompt = service.GetPrompt();

        Assert.Contains("- `UI` (project, 1 type)", prompt);
        Assert.Contains("  - `UI.Views` (namespace, 1 type)", prompt);
        Assert.Contains("- `Data` (project, 1 type)", prompt);
        // Load() -> Get() and View -> Repo, counted between the containers listed deepest.
        Assert.Contains("- `UI.Views` → `Data`: 2", prompt);
        Assert.DoesNotContain("`UI` → `Data`", prompt);
        Assert.Contains("Rule: <name> must not use <name>", prompt);
    }

    [Fact]
    public void GetPrompt_ShouldBeEmpty_WithoutAModel()
    {
        var empty = new SuggestRulesService(
            new ModelMgr(new StateMgr()),
            Mock.Of<IModelQueries>(),
            Mock.Of<IRuleService>(),
            Mock.Of<IAiService>(),
            Mock.Of<IApplicationEvents>(),
            Mock.Of<IDialogService>()
        );

        Assert.Equal("", empty.GetPrompt());
    }

    [Fact]
    public void Check_ShouldCountWhatEachRuleBreaks_AndSayWhyOneCannotBeAdded()
    {
        var suggestions = new RuleSuggestions([
            new SuggestedRule("UI", "Data", "Layering."),
            new SuggestedRule("Data", "UI", "Layering."),
            new SuggestedRule("UI", "UI.Views", "Nested."),
            new SuggestedRule("Nowhere", "Data", "Missing."),
        ]);

        var rules = service.Check(suggestions).Rules;

        // View -> Repo, by two links (one of them between their members).
        Assert.Equal((1, 2), (rules[0].Check.ViolationCount, rules[0].Check.LinkCount));
        Assert.True(rules[0].CanAdd);
        Assert.Equal(0, rules[1].Check.ViolationCount);
        Assert.Equal("Data", rules[1].Check.FromName);
        Assert.False(rules[2].CanAdd);
        Assert.Contains("neither contains the other", rules[2].Check.Error);
        Assert.Contains("No node is named \"Nowhere\"", rules[3].Check.Error);
    }

    [Fact]
    public void AddRules_ShouldAddTheNewRules_AsOneUndoStep()
    {
        var commands = new Mock<ICommandService>();
        var ruleService = new RuleService(modelMgr, commands.Object);

        var added = ruleService.AddRules([
            new ArchitectureRule("UI", "Data"),
            new ArchitectureRule("Data", "UI"),
            new ArchitectureRule("UI", "Data"), // Named twice
            new ArchitectureRule("Gone", "Data"), // Not in the model
        ]);

        Assert.Equal(2, added);
        commands.Verify(c => c.Do(It.Is<CompositeCommand>(cc => cc.Commands.Count == 2)), Times.Once);
    }

    [Fact]
    public void Parse_ShouldReadTheStructuredAnswer()
    {
        var json = """{"rules":[{"from":"`UI`","to":"Data","why":"Layering."},{"from":"","to":"X","why":""}]}""";

        var suggestions = AssertOk(RuleSuggestions.Parse(json, AiSource.ClaudeCode));

        Assert.Equal([new SuggestedRule("UI", "Data", "Layering.")], suggestions.Rules);
        Assert.Equal(AiSource.ClaudeCode, suggestions.Source);
        Assert.Equal("1 rule", suggestions.Verdict);
    }

    [Fact]
    public void ParseText_ShouldReadEachRule_AndTheWhyAfterIt()
    {
        var text = """
            Here are my suggestions:

            1. **Rule:** `UI` must not use `Data`
               **Why:** The UI goes through the services.
            2. Rule: Data must not depend on UI.
            Rule: Core must not use MudBlazor
            Why: Core stays free of UI frameworks.
            """;

        var suggestions = AssertOk(RuleSuggestions.ParseText(text));

        Assert.Equal(
            [
                new SuggestedRule("UI", "Data", "The UI goes through the services."),
                new SuggestedRule("Data", "UI", ""),
                new SuggestedRule("Core", "MudBlazor", "Core stays free of UI frameworks."),
            ],
            suggestions.Rules
        );
        Assert.Equal(AiSource.Chat, suggestions.Source);
    }

    [Fact]
    public void ParseText_ShouldFail_WithoutRuleLines()
    {
        AssertError(RuleSuggestions.ParseText("I would keep the UI apart from the data."));
    }

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
}
