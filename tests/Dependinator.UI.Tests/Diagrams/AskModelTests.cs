using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Diagrams.Ask;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// Ask about the model: the prompt gives an overview and the question, and an answer is read from
// structured output with the node names it gives.
public class AskModelTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());

    public AskModelTests()
    {
        using var model = modelMgr.UseModel();
        var ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
        AddNode(model, "UI.View", ui);
        AddNode(model, "Data", model.Root, NodeType.Assembly);
    }

    [Fact]
    public void Build_ShouldListTheTopLevel_AndAskTheQuestion()
    {
        using var model = modelMgr.UseModel();

        var prompt = AskModelPrompt.Build(model, "  Where is the view?  ", []);

        Assert.Contains("- `Data` (project, titled \"Data (dll)\")", prompt);
        Assert.Contains("- `UI` (project", prompt);
        Assert.Contains("## The question\n\nWhere is the view?\n", prompt.Replace("\r", ""));
        Assert.Contains("search_nodes", prompt);
        Assert.DoesNotContain("Earlier in this conversation", prompt);
    }

    [Fact]
    public void Build_ShouldGiveAFollowUpTheLastExchanges_WithLongAnswersCut()
    {
        using var model = modelMgr.UseModel();
        List<AskExchange> earlier =
        [
            new("First?", "One."),
            new("Second?", "Two."),
            new("Third?", "Three."),
            new("Fourth?", new string('x', AskModelPrompt.MaxEarlierAnswerLength + 10)),
        ];

        var prompt = AskModelPrompt.Build(model, "And then?", earlier).Replace("\r", "");

        Assert.Contains("## Earlier in this conversation\n\n(1 earlier question left out.)", prompt);
        Assert.DoesNotContain("First?", prompt);
        Assert.Contains("**Question:** Second?\n\n**Answer:** Two.", prompt);
        Assert.Contains(new string('x', AskModelPrompt.MaxEarlierAnswerLength) + " … (cut)", prompt);
        Assert.Contains("## The question (it may follow up on the conversation above)\n\nAnd then?\n", prompt);
    }

    [Fact]
    public void Build_ShouldSayWhatTheQuestionIsAbout_WithWhatIsInsideTheSelectedNode()
    {
        using var model = modelMgr.UseModel();
        var ui = model.Nodes[NodeId.FromName("UI")];

        var prompt = AskModelPrompt.Build(model, "What is this?", [], new AskContext([ui.Id], true)).Replace("\r", "");

        Assert.Contains("## What the question is about\n\nThe user selected this node", prompt);
        Assert.Contains("asks about it, with everything inside it", prompt);
        Assert.Contains("- `UI` (project", prompt);
        Assert.Contains("Contains 1 type; uses 0 types outside it, used by 0 types.", prompt);
        Assert.Contains("  Directly inside: `View`", prompt);
        Assert.True(prompt.IndexOf("## What the question is about") < prompt.IndexOf("## The question\n"));
    }

    [Fact]
    public void Build_ShouldNameSeveralSelectedNodes_AndLeaveOutWhatIsInside_WhenNotAsked()
    {
        using var model = modelMgr.UseModel();
        NodeId[] ids = [NodeId.FromName("UI"), NodeId.FromName("Data"), NodeId.FromName("Gone")];

        var prompt = AskModelPrompt.Build(model, "How do these work together?", [], new AskContext(ids, false));

        Assert.Contains("The user selected these 2 nodes in the diagram and asks about them (", prompt);
        Assert.Contains("- `Data` (project", prompt);
        Assert.DoesNotContain("Directly inside", prompt);
        Assert.Contains("not about each thing inside them", prompt);
    }

    [Fact]
    public void Build_ShouldSayWhatAnEarlierQuestionWasAbout()
    {
        using var model = modelMgr.UseModel();

        var prompt = AskModelPrompt.Build(model, "And then?", [new("What is this?", "A view.", "View")]);

        Assert.Contains("**Question (about View):** What is this?", prompt);
        Assert.DoesNotContain("## What the question is about", prompt);
    }

    [Fact]
    public void Build_ShouldNotSpeakOfWhatIsInside_ForANodeWithNothingInside()
    {
        using var model = modelMgr.UseModel();
        var view = model.Nodes[NodeId.FromName("UI.View")];

        var prompt = AskModelPrompt.Build(model, "What is this?", [], new AskContext([view.Id], true));

        Assert.Contains("asks about it (", prompt);
        Assert.DoesNotContain(
            "inside",
            prompt[prompt.IndexOf("## What the question is about")..prompt.IndexOf("## The question")]
                .Replace("Contains nothing parsed", "")
        );
    }

    [Fact]
    public void AboutNames_ShouldGiveTheFullNames_ForAFollowUpsPrompt()
    {
        using var model = modelMgr.UseModel();
        NodeId[] ids = [NodeId.FromName("UI.View"), NodeId.FromName("Data")];

        Assert.Equal("`UI.View`, `Data`", AskModelPrompt.AboutNames(model, new AskContext(ids, true)));
        Assert.Equal("", AskModelPrompt.AboutNames(model, AskContext.Model));
    }

    [Fact]
    public void Examples_ShouldFitWhatTheQuestionIsAbout()
    {
        Assert.Contains("codebase", AskExamples.For(0)[0].Top[0]);
        Assert.Equal("What is this and why is it here?", AskExamples.For(1)[0].Top[0]);
        Assert.Contains("these", AskExamples.For(3)[0].Top[0]);
        Assert.All(
            new[] { 0, 1, 2 }.SelectMany(AskExamples.For),
            g => Assert.True(g.Top.Count > 0 && g.More.Count > 0)
        );
    }

    [Fact]
    public void Build_ShouldBeEmpty_WithoutAModel()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();

        Assert.Equal("", AskModelPrompt.Build(model, "Anything?", []));
    }

    [Fact]
    public void Parse_ShouldReadTheAnswer_AndItsNodes()
    {
        var json = """
            {"answer":"The view is `UI.View`, see `Data`.","nodes":[
              {"name":"`UI.View`","why":"the view"},{"name":"UI.View","why":"twice"},{"name":" ","why":"none"}]}
            """;

        var answer = AssertOk(ModelAnswer.Parse(json, AiSource.ClaudeCode));

        Assert.Equal("The view is `UI.View`, see `Data`.", answer.Text);
        Assert.Equal([new AnswerNode("UI.View", "the view")], answer.Nodes);
        Assert.Equal(AiSource.ClaudeCode, answer.Source);
        Assert.Equal(["UI.View", "Data"], answer.Names());
    }

    [Fact]
    public void Parse_ShouldReadTheProposedChange_OnOneLine_OrNone()
    {
        var with = AssertOk(
            ModelAnswer.Parse("""{"answer":"Split it.","nodes":[],"change":" Split View\n into two. "}""")
        );
        var none = AssertOk(ModelAnswer.Parse("""{"answer":"It is a view.","nodes":[],"change":"None."}"""));
        var missing = AssertOk(ModelAnswer.Parse("""{"answer":"It is a view.","nodes":[]}"""));

        Assert.Equal("Split View into two.", with.Change);
        Assert.Equal("", none.Change);
        Assert.Equal("", missing.Change);
    }

    [Fact]
    public void ChangePrompt_ShouldHoldTheChange_TheAnswer_TheFilesAndTheRules()
    {
        using var model = modelMgr.UseModel();
        var view = model.Nodes[NodeId.FromName("UI.View")];
        view.Update(
            new Dependinator.Core.Parsing.Node(
                "UI.View",
                new NodeProperties { FileSpan = new FileSpan("/src/UI/View.cs", 1, 9) }
            )
        );
        model.AddRule(new ArchitectureRule("Data", "UI"));
        var answer = new ModelAnswer("Split `UI.View` in two.", [])
        {
            Question = "Should it be split?",
            Change = "Split View into a view and a model.",
            Links = new Dictionary<string, NodeLink> { ["UI.View"] = new(view.Id, "View", "UI.View") },
        };

        var prompt = AskChangePrompt
            .Build(model, answer, new AskContext([view.Id], true), "`UI.View`")
            .Replace("\r", "");

        Assert.Contains("## The change\n\nSplit View into a view and a model.\n", prompt);
        Assert.Contains("**Question (about `UI.View`):** Should it be split?", prompt);
        Assert.Contains("**Answer:** Split `UI.View` in two.", prompt);
        Assert.Contains("- `UI.View`: /src/UI/View.cs", prompt);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(prompt, "View.cs"));
        Assert.Contains("## Architecture rules to keep", prompt);
        Assert.Contains("Build and run the tests if you can run commands", prompt);
    }

    [Fact]
    public void ChangePrompt_ShouldListTheFilesInsideAnAskedContainer_ThatHasNoFileOfItsOwn()
    {
        using var model = modelMgr.UseModel();
        var ui = model.Nodes[NodeId.FromName("UI")];
        model
            .Nodes[NodeId.FromName("UI.View")]
            .Update(
                new Dependinator.Core.Parsing.Node(
                    "UI.View",
                    new NodeProperties { FileSpan = new FileSpan("/src/UI/View.cs", 1, 9) }
                )
            );
        var answer = new ModelAnswer("Split it.", []) { Question = "Split?", Change = "Split UI." };

        var inside = AskChangePrompt.Build(model, answer, new AskContext([ui.Id], true), "`UI`");
        var alone = AskChangePrompt.Build(model, answer, new AskContext([ui.Id], false), "`UI`");

        Assert.Contains("- `UI.View`: /src/UI/View.cs", inside);
        Assert.DoesNotContain("View.cs", alone);
    }

    [Fact]
    public void PromptForOtherAi_ShouldHoldTheQuestionAndItsNodes_AndSayToUseTheMcpServerIfThere()
    {
        using var model = modelMgr.UseModel();
        var view = model.Nodes[NodeId.FromName("UI.View")];
        view.Update(
            new Dependinator.Core.Parsing.Node(
                "UI.View",
                new NodeProperties { FileSpan = new FileSpan("/src/UI/View.cs", 1, 9) }
            )
        );
        var context = new AskContext([view.Id], false);

        var prompt = AskModelPrompt.BuildForOtherAi(model, "What uses it?", [], context);
        var inPanel = AskModelPrompt.Build(model, "What uses it?", [], context);

        Assert.Contains("What uses it?", prompt);
        Assert.Contains("`UI.View`", prompt);
        Assert.Contains("MCP server named dependinator", prompt);
        Assert.Contains("If you do not have those tools", prompt);
        // It may have no tools, so only the panel's own prompt says to use them whatever.
        Assert.Contains("Start from these nodes, by these names.", prompt);
        Assert.Contains("Look these nodes up with the tools", inPanel);
        Assert.DoesNotContain("up with the tools", prompt);
        // Without the tools it finds the node in the code by its file.
        Assert.Contains("In the file: /src/UI/View.cs", prompt);
        Assert.DoesNotContain("In the file:", inPanel);
        Assert.DoesNotContain("Under change", prompt);
    }

    [Fact]
    public void ChangePrompt_ShouldBeEmpty_ForAnAnswerThatProposesNoChange()
    {
        using var model = modelMgr.UseModel();

        Assert.Equal("", AskChangePrompt.Build(model, new ModelAnswer("It is a view.", []), AskContext.Model, ""));
    }

    [Fact]
    public void Parse_ShouldFail_ForAnEmptyAnswer()
    {
        AssertError(ModelAnswer.Parse("""{"answer":" ","nodes":[]}"""));
        AssertError(ModelAnswer.Parse("not json"));
    }

    [Fact]
    public void FindLinkedNode_ShouldFindANodeByTheEndOfItsTitle_WhenOnlyOneEndsSo()
    {
        using var model = modelMgr.UseModel();
        var parser = AddNode(model, "Lib*dll.Lib.Parsing.Parser", model.Root);
        AddNode(model, "Lib*dll.Lib.Parsing.Parser.Read(string)", parser, NodeType.MethodMember);
        var readNumber = AddNode(model, "Lib*dll.Lib.Parsing.Parser.Read(int)", parser, NodeType.MethodMember);
        AddNode(model, "Lib*dll.Lib.Other.Read()", model.Root, NodeType.MethodMember);

        Assert.Equal(parser, AskModelService.FindLinkedNode(model, "Parsing.Parser"));
        Assert.Equal(parser, AskModelService.FindLinkedNode(model, "Parser"));
        Assert.Equal(readNumber, AskModelService.FindLinkedNode(model, "Parser.Read()")); // Overloads: the first by name
        Assert.Null(AskModelService.FindLinkedNode(model, "Read")); // Two methods end so
        Assert.Null(AskModelService.FindLinkedNode(model, "ser")); // Not at a dot
        Assert.Null(AskModelService.FindLinkedNode(model, "new Parser()"));

        // Two namespaces with one title (a project and its tests): no guess.
        AddNode(model, "Lib*dll.Lib.Shared", model.Root, NodeType.Namespace);
        AddNode(model, "LibTests*dll.Lib.Shared", model.Root, NodeType.Namespace);
        Assert.Null(AskModelService.FindLinkedNode(model, "Lib.Shared"));
    }

    static Node AddNode(IModel model, string name, Node parent, NodeType type = NodeType.ClassType)
    {
        var node = new Node(name, parent) { Boundary = new Rect(0, 0, 100, 100), Type = type };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }
}
