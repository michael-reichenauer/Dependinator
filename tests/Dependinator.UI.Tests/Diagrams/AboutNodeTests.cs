using Dependinator.Core.Claude;
using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// About a node: what the dialog shows of it, the prompt that asks an AI a question about it (the
// node, what it contains, what it uses and what uses it), and the answer, from structured output
// or an AI chat's pasted text.
public class AboutNodeTests
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

    // Namespace Parsing with a commented Parser (two methods) that uses Model.Node twice, and
    // App.Main that uses Parser.
    static Node Setup(IModel model)
    {
        var parsing = AddNode(model, "Parsing", model.Root, NodeType.Namespace);
        parsing.SetDescription("Turns source into a model.");
        var modelNs = AddNode(model, "Model", model.Root, NodeType.Namespace);
        var app = AddNode(model, "App", model.Root, NodeType.Namespace);
        var parser = AddNode(model, "Parsing.Parser", parsing);
        parser.SetDescription("Parses one file.");
        var parse = AddNode(model, "Parsing.Parser.Parse()", parser, NodeType.MethodMember);
        var read = AddNode(model, "Parsing.Parser.Read()", parser, NodeType.MethodMember);
        var node = AddNode(model, "Model.Node", modelNs);
        var main = AddNode(model, "App.Main", app);
        AddLink(model, parse, node);
        AddLink(model, read, node);
        AddLink(model, parse, read);
        AddLink(model, main, parse);
        return parsing;
    }

    [Fact]
    public void Facts_ShouldCountWhatItContains_AndTheTypesOutsideItOnEachSide()
    {
        using var model = modelMgr.UseModel();
        var parsing = Setup(model);

        var facts = NodePrompt.Facts(parsing);

        Assert.Equal("namespace", facts.Kind);
        Assert.Equal("Turns source into a model.", facts.Description);
        Assert.Equal("Contains 1 type", facts.Contents);
        Assert.Equal(1, facts.UsesCount);
        Assert.Equal(1, facts.UsedByCount);
        Assert.Equal("Contains 2 members", NodePrompt.Facts(model.Nodes[NodeId.FromName("Parsing.Parser")]).Contents);
    }

    [Fact]
    public void Facts_ShouldNameAMembersKind_AndLeaveOutLinksWithinItsOwnType()
    {
        using var model = modelMgr.UseModel();
        Setup(model);

        var facts = NodePrompt.Facts(model.Nodes[NodeId.FromName("Parsing.Parser.Parse()")]);

        Assert.Equal("method", facts.Kind);
        Assert.Equal(1, facts.UsesCount); // Model.Node; Read() is in its own type
        Assert.Equal(1, facts.UsedByCount);
    }

    [Fact]
    public void Build_ShouldDescribeTheNode_ItsContents_ItsLinksOutside_AndTheQuestion()
    {
        using var model = modelMgr.UseModel();
        var parsing = Setup(model);

        var prompt = NodePrompt.Build(model, parsing, "Why does it use Model?").ReplaceLineEndings("\n");

        Assert.Contains(
            "## The node\n\nnamespace Parsing\nIts comment in the code: \"Turns source into a model.\"\n",
            prompt
        );
        Assert.Contains("## What it contains\n\n- class Parser: \"Parses one file.\"\n", prompt);
        Assert.Contains(
            "## What it uses\n\nTypes outside it that it uses, most links first:\n- class Node (2 links)\n",
            prompt
        );
        Assert.Contains(
            "## What uses it\n\nTypes outside it that use it, most links first:\n- class Main (1 link)\n",
            prompt
        );
        Assert.Contains("## My question\n\nWhy does it use Model?\n", prompt);
        Assert.Contains("This is a question only", prompt);
    }

    [Fact]
    public void Build_ShouldListATypesMembers_AndAskTheDefaultQuestion_WhenNoneIsGiven()
    {
        using var model = modelMgr.UseModel();
        Setup(model);
        var parser = model.Nodes[NodeId.FromName("Parsing.Parser")];

        var prompt = NodePrompt.Build(model, parser, " ").ReplaceLineEndings("\n");

        Assert.Contains("It is in namespace Parsing: \"Turns source into a model.\"\n", prompt);
        Assert.Contains("- method Parse\n- method Read\n", prompt);
        Assert.Contains($"## My question\n\n{NodePrompt.DefaultQuestion}\n", prompt);
    }

    [Fact]
    public void Parse_ShouldReadTheAnswerField()
    {
        var answer = AssertOk(NodeAnswer.Parse("""{ "answer": " It parses. " }""", AiSource.ClaudeCode));

        Assert.Equal("It parses.", answer.Text);
        Assert.Equal(AiSource.ClaudeCode, answer.Source);
        AssertError(NodeAnswer.Parse("""{ "answer": "" }"""));
        AssertError(NodeAnswer.Parse("not json"));
    }

    [Fact]
    public void ParseText_ShouldTakeAllOfAPastedAnswer()
    {
        var answer = AssertOk(NodeAnswer.ParseText("\nIt parses.\n\n- one file at a time\n"));

        Assert.Equal("It parses.\n\n- one file at a time", answer.Text);
        Assert.Equal(AiSource.Chat, answer.Source);
        AssertError(NodeAnswer.ParseText("  "));
    }

    [Fact]
    public async Task AskAsync_ShouldAskClaudesQuickerModel_ForAQuickQuestion()
    {
        var claude = new Mock<IClaudeService>();
        claude.Setup(c => c.GetApiKeyStateAsync()).ReturnsAsync(ClaudeApiKeyState.Set);
        claude
            .Setup(c => c.AskQuicklyAsync("the prompt", NodeAnswer.JsonSchema))
            .ReturnsAsync((Result<string>)"""{ "answer": "It parses." }""");
        var ai = new AiService(claude.Object, Mock.Of<IConfigService>(), Mock.Of<IModelMgr>());

        var json = AssertOk(
            await ai.AskAsync(
                AiSource.Claude,
                () => Task.FromResult("the prompt"),
                NodeAnswer.JsonSchema,
                _ => { },
                isQuick: true
            )
        );

        Assert.Equal("It parses.", AssertOk(NodeAnswer.Parse(json)).Text);
        claude.Verify(c => c.AskAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
