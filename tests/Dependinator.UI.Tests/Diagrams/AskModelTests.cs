using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Ai;
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
    }

    static Node AddNode(IModel model, string name, Node parent, NodeType type = NodeType.ClassType)
    {
        var node = new Node(name, parent) { Boundary = new Rect(0, 0, 100, 100), Type = type };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }
}
