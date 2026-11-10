using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Diagrams.Editing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// AI labels for several lines at once: which lines are asked about, the prompt that numbers them,
// and the answer (structured output, or an AI chat's pasted text) matched back to the lines.
public class LineLabelsSuggestionTests
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

    // Namespaces Parsing, Model and Utils. Parsing.Parser.Parse() uses Model.Node and
    // Model.Store.Save(); Model.Store uses Model.Node and Utils.Log.
    static Node Setup(IModel model)
    {
        var parsing = AddNode(model, "Parsing", model.Root, NodeType.Namespace);
        parsing.SetDescription("Turns source into a model.");
        var modelNs = AddNode(model, "Model", model.Root, NodeType.Namespace);
        var utils = AddNode(model, "Utils", model.Root, NodeType.Namespace);
        var parser = AddNode(model, "Parsing.Parser", parsing);
        var parse = AddNode(model, "Parsing.Parser.Parse()", parser, NodeType.MethodMember);
        var node = AddNode(model, "Model.Node", modelNs);
        var store = AddNode(model, "Model.Store", modelNs);
        var save = AddNode(model, "Model.Store.Save()", store, NodeType.MethodMember);
        var log = AddNode(model, "Utils.Log", utils);
        AddLink(model, parse, node);
        AddLink(model, parse, save);
        AddLink(model, store, node);
        AddLink(model, store, log);
        // Lines to the external references are not asked about.
        var externals = AddNode(model, "Externals", model.Root, NodeType.Externals);
        AddLink(model, store, AddNode(model, "Externals.Json", externals));
        return modelNs;
    }

    static string Names(IEnumerable<Line> lines) =>
        string.Join(", ", lines.Select(l => $"{l.Source.LongName}→{l.Target.LongName}"));

    [Fact]
    public void Candidates_ShouldBeTheUnlabeledSiblingLines_OuterLevelsAndMostLinksFirst()
    {
        using var model = modelMgr.UseModel();
        Setup(model);

        var lines = LineLabelsPrompt.Candidates(model, model.Root).Lines;

        Assert.Equal("Parsing→Model, Model→Utils, Store→Node", Names(lines));
        Assert.Equal(3, LineLabelsPrompt.Candidates(model, model.Root).Total);

        // Lines asked about before come last.
        HashSet<LineId> asked = [lines[0].Id];
        Assert.Equal(
            "Model→Utils, Store→Node, Parsing→Model",
            Names(LineLabelsPrompt.Candidates(model, model.Root, asked).Lines)
        );
    }

    [Fact]
    public void Candidates_ShouldSkipLabeledLines_AndStayInsideTheScope()
    {
        using var model = modelMgr.UseModel();
        var modelNs = Setup(model);
        model.Lines.Values.Single(l => l.Source.LongName == "Model" && l.Target.LongName == "Utils").SetLabel("logs");

        Assert.Equal("Parsing→Model, Store→Node", Names(LineLabelsPrompt.Candidates(model, model.Root).Lines));
        Assert.Equal("Store→Node", Names(LineLabelsPrompt.Candidates(model, modelNs).Lines));
    }

    [Fact]
    public void Build_ShouldNumberTheLines_ListTheirEndsAndLinks_AndAskForTheFormat()
    {
        using var model = modelMgr.UseModel();
        Setup(model);
        var lines = LineLabelsPrompt.Candidates(model, model.Root).Lines;

        var prompt = LineLabelsPrompt.Build(model, lines).ReplaceLineEndings("\n");

        Assert.Contains("- namespace `Parsing`: \"Turns source into a model.\"\n", prompt);
        Assert.Contains("1. `Parsing` → `Model`\n   - Parser.Parse() → Node, Store (Save)\n", prompt);
        Assert.Contains("3. `Store` → `Node`\n", prompt);
        Assert.Contains("<number>. <label> | <description, or empty>", prompt);
        Assert.Contains("This is a question only", prompt);
        Assert.DoesNotContain("## Labels other lines already have", prompt);
    }

    [Fact]
    public void Build_ShouldKeepTheNumbers_WhenALineIsGone_AndShowOtherLabels()
    {
        using var model = modelMgr.UseModel();
        Setup(model);
        var lines = LineLabelsPrompt.Candidates(model, model.Root).Lines;
        lines[1].SetLabel("logs to", "errors");

        var prompt = LineLabelsPrompt.Build(model, [lines[0], null, lines[2]]).ReplaceLineEndings("\n");

        Assert.Contains("1. `Parsing` → `Model`\n", prompt);
        Assert.DoesNotContain("\n2. ", prompt);
        Assert.Contains("3. `Store` → `Node`\n", prompt);
        Assert.Contains("## Labels other lines already have\n\n- Model \"logs to\" (errors) Utils\n", prompt);
        Assert.Equal("", LineLabelsPrompt.Build(model, [null]));
    }

    [Fact]
    public void Parse_ShouldReadTheLabels_AndCleanThem()
    {
        var json = """
            {"labels":[{"line":2,"label":"\"Logs to\"","description":""},{"line":1,"label":"builds","description":"the nodes of the model"},{"line":3,"label":" ","description":"x"}]}
            """;

        var suggestion = AssertOk(LineLabelsSuggestion.Parse(json, AiSource.Copilot));

        Assert.Equal(AiSource.Copilot, suggestion.Source);
        Assert.Equal("2 labels", suggestion.Verdict);
        Assert.Equal(new SuggestedLineLabel(2, "Logs to", ""), suggestion.Labels[0]);
        Assert.Equal(new SuggestedLineLabel(1, "builds", "the nodes of the model"), suggestion.Labels[1]);
        AssertError(LineLabelsSuggestion.Parse("""{"labels":[]}"""));
        AssertError(LineLabelsSuggestion.Parse("no json"));
    }

    [Fact]
    public void ParseText_ShouldReadNumberedLines_WithOrWithoutADescription()
    {
        var text = """
            Here are the labels:

            1. builds | the nodes of the model
            - **2.** logs to
            Line 3: creates | 
            Some closing words.
            """;

        var suggestion = AssertOk(LineLabelsSuggestion.ParseText(text));

        Assert.Equal(AiSource.Chat, suggestion.Source);
        Assert.Equal(
            [
                new SuggestedLineLabel(1, "builds", "the nodes of the model"),
                new SuggestedLineLabel(2, "logs to", ""),
                new SuggestedLineLabel(3, "creates", ""),
            ],
            suggestion.Labels
        );
        AssertError(LineLabelsSuggestion.ParseText("I cannot help with that."));
    }

    [Fact]
    public void ParseText_ShouldSkipTheQuestionsOwnLines_AndTakeTheLastLabelOfANumber()
    {
        var text = """
            1. I looked at the links first.
            1. `Parsing` → `Model`
            2. Store → Node
            1. builds | the nodes

            """;

        var suggestion = AssertOk(LineLabelsSuggestion.ParseText(text));

        Assert.Equal([new SuggestedLineLabel(1, "builds", "the nodes")], suggestion.Labels);
        AssertError(LineLabelsSuggestion.ParseText("1. `Parsing` → `Model`"));
    }

    [Fact]
    public void Resolve_ShouldFindTheLines_AndLeaveOutLabelsForNoLineOrALineTwice()
    {
        using var model = modelMgr.UseModel();
        Setup(model);
        var lines = LineLabelsPrompt.Candidates(model, model.Root).Lines;
        List<LineId> ids = [.. lines.Select(l => l.Id)];
        var suggestion = new LineLabelsSuggestion([
            new SuggestedLineLabel(3, "creates", ""),
            new SuggestedLineLabel(1, "builds", "the nodes"),
            new SuggestedLineLabel(1, "again", ""),
            new SuggestedLineLabel(4, "none", ""),
            new SuggestedLineLabel(0, "none", ""),
        ]);

        var resolved = AssertOk(LineLabelsSuggestion.Resolve(suggestion, model, ids));

        Assert.Equal(3, resolved.LeftOutCount);
        Assert.Equal([1, 3], resolved.Labels.Select(l => l.Number));
        Assert.Equal(lines[0].Id, resolved.Labels[0].Id);
        Assert.Equal("builds", resolved.Labels[0].Label);
        Assert.Equal("Parsing", resolved.Labels[0].SourceName);
        Assert.Equal("Model", resolved.Labels[0].TargetName);
        Assert.Equal("Store → Node", resolved.Labels[1].LongName);
        AssertError(
            LineLabelsSuggestion.Resolve(new LineLabelsSuggestion([new SuggestedLineLabel(9, "x", "")]), model, ids)
        );
    }

    [Fact]
    public void LineLabelsCommand_ShouldSetAllLabels_AndRevertThemInOneStep()
    {
        using var model = modelMgr.UseModel();
        Setup(model);
        var lines = LineLabelsPrompt.Candidates(model, model.Root).Lines;
        lines[1].SetLabel("old", "text");
        var command = new LineLabelsCommand([
            new LineLabelCommand(lines[0].Id) { Label = "builds", Description = "the nodes" },
            new LineLabelCommand(lines[1].Id) { Label = "logs to", Description = "" },
        ]);

        command.Execute(model);
        Assert.Equal(("builds", "the nodes"), (lines[0].Label, lines[0].LabelDescription));
        Assert.Equal(("logs to", null), (lines[1].Label, lines[1].LabelDescription));

        command.Revert(model);
        Assert.False(lines[0].HasLabel);
        Assert.Equal(("old", "text"), (lines[1].Label, lines[1].LabelDescription));

        command.Execute(model);
        Assert.Equal("builds", lines[0].Label);
        Assert.Equal("logs to", lines[1].Label);
    }
}
