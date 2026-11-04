using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Diagrams.Cycles;
using Dependinator.UI.Diagrams.Navigation;
using Dependinator.UI.Diagrams.Rules;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
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
        var commands = new Mock<ICommandService>();
        commands
            .Setup(c => c.Do(It.IsAny<Command>(), true, true))
            .Callback<Command, bool, bool>((command, _, _) => modelMgr.WithModel(m => command.Execute(m)));
        var ruleService = new RuleService(modelMgr, commands.Object);
        var queries = new ModelQueries(
            modelMgr,
            new NodeSearchService(modelMgr),
            new CycleService(modelMgr, Mock.Of<ICommandService>()),
            ruleService,
            Mock.Of<Dependinator.Core.Shared.IWorkspaceFileService>(),
            new ModelQueryService()
        );
        service = new SuggestRulesService(
            modelMgr,
            queries,
            ruleService,
            Mock.Of<IAiService>(),
            Mock.Of<IApplicationEvents>()
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
            Mock.Of<IApplicationEvents>()
        );

        Assert.Equal("", empty.GetPrompt());
    }

    [Fact]
    public void Check_ShouldCountWhatEachRuleBreaks_AndSayWhyOneCannotBeAdded()
    {
        var suggestions = new RuleSuggestions([
            new SuggestedRule("UI", "Data", "Layering."),
            new SuggestedRule("Data", "UI", "Layering."),
            new SuggestedRule(RuleKind.OnlyMayUse, ["UI", "UI.Views"], "Nested."),
            new SuggestedRule("Nowhere", "Data", "Missing."),
        ]);

        var rules = service.Check(suggestions).Rules;

        // View -> Repo, by two links (one of them between their members).
        Assert.Equal((1, 2), (rules[0].Check.ViolationCount, rules[0].Check.LinkCount));
        Assert.True(rules[0].CanAdd);
        Assert.Equal(0, rules[1].Check.ViolationCount);
        Assert.Equal("Data", rules[1].Check.Rule!.FromName);
        Assert.False(rules[2].CanAdd);
        Assert.Contains("is inside", rules[2].Check.Error);
        Assert.Contains("No node is named \"Nowhere\"", rules[3].Check.Error);
    }

    [Fact]
    public void Check_ShouldCheckEveryKind()
    {
        var suggestions = new RuleSuggestions([
            new SuggestedRule(RuleKind.Layers, ["Data", "UI"], "Upside down."),
            new SuggestedRule(RuleKind.Layers, ["UI", "Data"], "Layering."),
            new SuggestedRule(RuleKind.Independent, ["UI"], "Breaks."),
            new SuggestedRule(RuleKind.SiblingsIndependent, ["UI"], "One child."),
            new SuggestedRule(RuleKind.NoCycles, ["Nowhere"], "Missing."),
            new SuggestedRule(RuleKind.Layers, ["UI"], "One layer."),
        ]);

        var rules = service.Check(suggestions).Rules;

        Assert.Equal(
            [(1, null), (0, null), (1, null), (0, null)],
            rules.Take(4).Select(r => (r.Check.ViolationCount, r.Check.Error))
        );
        Assert.Equal(RuleKind.Independent, rules[2].Check.Rule!.Kind);
        Assert.Equal(["UI (dll)"], rules[2].Check.Titles);
        Assert.Contains("No node is named \"Nowhere\"", rules[4].Check.Error);
        Assert.NotNull(rules[5].Check.Error);
        Assert.Equal(ArchitectureRule.ForNode(RuleKind.SiblingsIndependent, "UI"), rules[3].Key);
    }

    [Fact]
    public void Add_ShouldAddTheCheckedRulesOfAnyKind_WithTheirWhy()
    {
        var suggestions = service.Check(
            new RuleSuggestions([
                new SuggestedRule(RuleKind.NoCycles, ["Nowhere"], "Missing."),
                new SuggestedRule(RuleKind.Layers, ["UI", "Data"], "Layering."),
            ])
        );

        service.Add(suggestions.Rules);

        using var model = modelMgr.UseModel();
        Assert.Equal([new ArchitectureRule("UI", "Data", "Layering.", RuleKind.Layers, ["UI", "Data"])], model.Rules);
        Assert.Equal("Layering.", model.Rules.Single().Reason);
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

        Assert.Equal([(RuleKind.MustNotUse, "UI | Data", "Layering.")], Shown(suggestions.Rules));
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
                (RuleKind.MustNotUse, "UI | Data", "The UI goes through the services."),
                (RuleKind.MustNotUse, "Data | UI", ""),
                (RuleKind.MustNotUse, "Core | MudBlazor", "Core stays free of UI frameworks."),
            ],
            Shown(suggestions.Rules)
        );
        Assert.Equal(AiSource.Chat, suggestions.Source);
    }

    [Fact]
    public void Parse_ShouldReadEveryKind_AndLeaveOutUnknownKinds()
    {
        var json = """
            {"rules":[
              {"kind":"only_may_use","nodes":["Api","Data"],"why":"a"},
              {"kind":"layers","nodes":["UI","Core","Data"],"why":"b"},
              {"kind":"independent","nodes":["Utils"],"why":"c"},
              {"kind":"no_cycles","nodes":["Core"],"why":"d"},
              {"kind":"siblings_independent","nodes":["Features"],"why":"e"},
              {"kind":"name_pattern","nodes":["X"],"why":"f"},
              {"kind":"layers","nodes":["UI",""],"why":"g"}
            ]}
            """;

        var suggestions = AssertOk(RuleSuggestions.Parse(json));

        Assert.Equal(
            [
                (RuleKind.OnlyMayUse, "Api | Data", "a"),
                (RuleKind.Layers, "UI | Core | Data", "b"),
                (RuleKind.Independent, "Utils", "c"),
                (RuleKind.NoCycles, "Core", "d"),
                (RuleKind.SiblingsIndependent, "Features", "e"),
            ],
            Shown(suggestions.Rules)
        );
    }

    [Fact]
    public void ParseText_ShouldReadEveryKind()
    {
        var text = """
            Rule: Only `Api` may use `Data`
            Rule: Layers: `UI` > `Core` > `Data` (each may use only the layers below it)
            Rule: `Utils` is independent
            Rule: `UI` must not use `Data`, so UI is independent of storage
            Rule: No cycles inside `Core`
            Rule: The parts of `Features` are independent
            Why: Features stay apart.
            """;

        var suggestions = AssertOk(RuleSuggestions.ParseText(text));

        Assert.Equal(
            [
                (RuleKind.OnlyMayUse, "Api | Data", ""),
                (RuleKind.Layers, "UI | Core | Data", ""),
                (RuleKind.Independent, "Utils", ""),
                (RuleKind.MustNotUse, "UI | Data", ""),
                (RuleKind.NoCycles, "Core", ""),
                (RuleKind.SiblingsIndependent, "Features", "Features stay apart."),
            ],
            Shown(suggestions.Rules)
        );
    }

    static IEnumerable<(RuleKind, string, string)> Shown(IEnumerable<SuggestedRule> rules) =>
        rules.Select(r => (r.Kind, string.Join(" | ", r.Nodes), r.Why));

    [Fact]
    public void Parse_ShouldAcceptAnAnswerWithNoRules()
    {
        var suggestions = AssertOk(RuleSuggestions.Parse("""{"rules":[]}"""));

        Assert.Empty(suggestions.Rules);
        Assert.Equal("no new rules", suggestions.Verdict);
    }

    [Fact]
    public void ParsePasted_ShouldLeaveOutDismissedRules_AndListThemInThePrompt()
    {
        using (var model = modelMgr.UseModel())
            model.AddDismissedRule(DismissedRule.Of(new ArchitectureRule("Data", "UI"), "Not intended."));

        var suggestions = AssertOk(service.ParsePasted("Rule: UI must not use Data\nRule: Data must not use UI\n"));

        Assert.Equal(["UI"], suggestions.Rules.Select(r => r.From));
        Assert.Equal(1, suggestions.LeftOutCount);
        Assert.Contains("## Rules not to suggest", service.GetPrompt());
        Assert.Contains("- `Data` must not use `UI` (dismissed: Not intended.)", service.GetPrompt());
    }

    [Fact]
    public void ParsePasted_ShouldLeaveOutRulesAnEarlierAnswerSuggested_AndListThemInThePrompt()
    {
        AssertOk(service.ParsePasted("Rule: UI must not use Data"));

        var second = AssertOk(service.ParsePasted("Rule: UI must not use Data\nRule: Data must not use UI"));

        Assert.Equal(["Data"], second.Rules.Select(r => r.From));
        Assert.Equal(1, second.LeftOutCount);
        Assert.Contains("- `UI` must not use `Data` (suggested before)", service.GetPrompt());
    }

    [Fact]
    public void AllowSuggestingAgain_ShouldLetALaterAnswerSuggestTheRuleAgain()
    {
        AssertOk(service.ParsePasted("Rule: UI must not use Data"));

        service.AllowSuggestingAgain(new ArchitectureRule("UI", "Data"));

        var second = AssertOk(service.ParsePasted("Rule: UI must not use Data\nRule: UI must not use Data"));
        Assert.Single(second.Rules);
        Assert.Equal(0, second.LeftOutCount);
    }

    [Fact]
    public void Check_ShouldMarkADismissedRule_SoItCannotBeAdded()
    {
        using (var model = modelMgr.UseModel())
            model.AddDismissedRule(DismissedRule.Of(new ArchitectureRule("UI", "Data")));

        var rule = Assert.Single(service.Check(new RuleSuggestions([new SuggestedRule("UI", "Data", "")])).Rules);

        Assert.True(rule.Check.IsDismissed);
        Assert.False(rule.CanAdd);
    }

    [Fact]
    public void GetPrompt_ShouldListTheRulesWithTheirReasons()
    {
        using (var model = modelMgr.UseModel())
            model.AddRule(new ArchitectureRule("UI", "Data", "The UI goes through services."));

        Assert.Contains("- `UI` must not use `Data` — The UI goes through services.", service.GetPrompt());
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
