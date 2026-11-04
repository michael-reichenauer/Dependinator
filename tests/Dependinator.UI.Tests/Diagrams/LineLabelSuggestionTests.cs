using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Diagrams.Editing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// The AI suggestion for a line's label: the prompt (the line's ends, its links, its label now and
// the labels on other lines) and the answer, from structured output or an AI chat's pasted text.
public class LineLabelSuggestionTests
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

    // Namespace Parsing (commented) whose Parser.Parse() uses Model.Node and Model.Store.Save().
    static Line Setup(IModel model)
    {
        var parsing = AddNode(model, "Parsing", model.Root, NodeType.Namespace);
        parsing.SetDescription("Turns source into a model.");
        var modelNs = AddNode(model, "Model", model.Root, NodeType.Namespace);
        var parser = AddNode(model, "Parsing.Parser", parsing);
        var parse = AddNode(model, "Parsing.Parser.Parse()", parser, NodeType.MethodMember);
        var node = AddNode(model, "Model.Node", modelNs);
        var store = AddNode(model, "Model.Store", modelNs);
        var save = AddNode(model, "Model.Store.Save()", store, NodeType.MethodMember);
        AddLink(model, parse, node);
        AddLink(model, parse, save);
        return model.Lines.Values.Single(l => l.Source == parsing && l.Target == modelNs);
    }

    [Fact]
    public void Build_ShouldDescribeTheLine_ItsLinks_AndAskForTheFormat()
    {
        using var model = modelMgr.UseModel();
        var line = Setup(model);

        var prompt = LineLabelPrompt.Build(model, line).ReplaceLineEndings("\n");

        Assert.Contains(
            "## The line\n\nFrom namespace Parsing: \"Turns source into a model.\"\nto namespace Model\n",
            prompt
        );
        Assert.Contains("It stands for 2 links in the code:\n- Parser.Parse() → Node, Store (Save)\n", prompt);
        Assert.Contains("Label: <the label>\nDescription:", prompt);
        Assert.Contains("This is a question only", prompt);
        Assert.DoesNotContain("## Its label now", prompt);
        Assert.DoesNotContain("## Labels on other lines", prompt);
    }

    [Fact]
    public void Build_ShouldShowItsLabelNow_AndTheLabelsOnOtherLines()
    {
        using var model = modelMgr.UseModel();
        var line = Setup(model);
        line.SetLabel("uses", "the nodes");
        var other = model.Lines.Values.First(l => l != line);
        other.SetLabel("creates", "new nodes");

        var prompt = LineLabelPrompt.Build(model, line).ReplaceLineEndings("\n");

        Assert.Contains("## Its label now\n\nLabel: uses\nDescription: the nodes\n", prompt);
        Assert.Contains(
            $"## Labels on other lines\n\n- {other.Source.DisplayName} \"creates\" (new nodes) {other.Target.DisplayName}\n",
            prompt
        );
    }

    [Fact]
    public void Parse_ShouldReadTheFields_AndDropEmptyOrRepeatedAlternatives()
    {
        var suggestion = AssertOk(
            LineLabelSuggestion.Parse(
                """{ "label": " \"parses with\" ", "description": "", "alternatives": ["Parses with", "", "reads", "reads"] }""",
                AiSource.Copilot
            )
        );

        Assert.Equal("parses with", suggestion.Label);
        Assert.Equal("", suggestion.Description);
        Assert.Equal(["reads"], suggestion.Alternatives);
        Assert.Equal(AiSource.Copilot, suggestion.Source);
        AssertError(LineLabelSuggestion.Parse("""{ "label": " ", "description": "x", "alternatives": [] }"""));
        AssertError(LineLabelSuggestion.Parse("not json"));
    }

    [Fact]
    public void Parse_ShouldCutALongLabelAtAWord()
    {
        var suggestion = AssertOk(
            LineLabelSuggestion.Parse(
                """{ "label": "reads all the settings and the secrets it needs at start", "description": "", "alternatives": [] }"""
            )
        );

        Assert.Equal("reads all the settings and the secrets…", suggestion.Label);
        Assert.True(suggestion.Label.Length <= LineLabelSuggestion.MaxLabelLength + 1);
    }

    [Fact]
    public void ParseText_ShouldReadAPastedAnswersLines()
    {
        var suggestion = AssertOk(
            LineLabelSuggestion.ParseText(
                "Here you go:\n\n**Label:** stores models\nDescription: saves and loads the model files\n"
                    + "Alternatives: persists; saves to disk, writes\n"
            )
        );

        Assert.Equal("stores models", suggestion.Label);
        Assert.Equal("saves and loads the model files", suggestion.Description);
        Assert.Equal(["persists", "saves to disk", "writes"], suggestion.Alternatives);
        Assert.Equal(AiSource.Chat, suggestion.Source);
        AssertError(LineLabelSuggestion.ParseText("stores models"));
    }
}
