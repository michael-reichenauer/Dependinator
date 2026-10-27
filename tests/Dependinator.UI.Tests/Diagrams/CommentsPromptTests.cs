using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;
using ParsedNode = Dependinator.Core.Parsing.Node;

namespace Dependinator.UI.Tests.Diagrams;

// Improve Comments: which nodes a run comments (the chosen ones, or everything inside them, that
// the parser reads a comment for), and the prompt that asks Claude Code to write the comments.
public class CommentsPromptTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());

    static Node AddNode(IModel model, string name, Node parent, NodeType type, int? line = null)
    {
        var node = new Node(name, parent) { Boundary = new Rect(0, 0, 100, 100), Type = type };
        if (line is { } l)
            node.Update(new ParsedNode(name, new() { FileSpan = new FileSpan($"/repo/src/{name}.cs", l, l + 10) }));
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

    // Project App with namespace Parsing (a commented Parser, a Lexer) and namespace Model (Node).
    static (Node App, Node Parsing) Setup(IModel model)
    {
        var app = AddNode(model, "App", model.Root, NodeType.Assembly);
        var parsing = AddNode(model, "Parsing", app, NodeType.Namespace, 2);
        var modelNs = AddNode(model, "Model", app, NodeType.Namespace, 0);
        var parser = AddNode(model, "Parsing.Parser", parsing, NodeType.ClassType, 4);
        parser.SetDescription("Parses.");
        var lexer = AddNode(model, "Parsing.Lexer", parsing, NodeType.ClassType, 8);
        var node = AddNode(model, "Model.Node", modelNs, NodeType.RecordType, 3);
        AddNode(model, "Parsing.Parser.Parse()", parser, NodeType.MethodMember, 6);
        AddLink(model, parser, node);
        AddLink(model, parser, lexer);
        return (app, parsing);
    }

    [Fact]
    public void Targets_ShouldBeTheNodesNamespacesAndTypes_AndWithIsBelowEverythingInside()
    {
        using var model = modelMgr.UseModel();
        var (app, parsing) = Setup(model);

        Assert.Empty(CommentsPrompt.Targets([app], isBelow: false));
        Assert.Equal(["Parsing"], CommentsPrompt.Targets([parsing], isBelow: false).Select(n => n.Name));
        Assert.Equal(
            ["Model", "Model.Node", "Parsing", "Parsing.Lexer", "Parsing.Parser"],
            CommentsPrompt.Targets([app, parsing], isBelow: true).Select(n => n.Name)
        );
    }

    [Fact]
    public void Build_ShouldListEachTarget_WithWhereItsCommentGoes_ItsCommentNow_AndItsLinks()
    {
        using var model = modelMgr.UseModel();
        var (_, parsing) = Setup(model);

        var prompt = CommentsPrompt
            .Build(model, CommentsPrompt.Targets([parsing], isBelow: true))
            .ReplaceLineEndings("\n");

        Assert.Contains("## The 3 to comment\n", prompt);
        Assert.StartsWith("Please add or improve the code comments of the namespaces and types listed below.", prompt);
        Assert.Contains(
            "### namespace Parsing\nIts comment goes in /repo/src/Parsing.cs, line 3\nComment now: none\n",
            prompt
        );
        Assert.Contains(
            "### class Parser\nDeclared in /repo/src/Parsing.Parser.cs, line 5\nComment now: \"Parses.\"\nUses: Lexer, Node\n",
            prompt
        );
        Assert.Contains(
            "### class Lexer\nDeclared in /repo/src/Parsing.Lexer.cs, line 9\nComment now: none\nUsed by: Parser\n",
            prompt
        );
        Assert.Contains("A namespace's comment goes right above its `namespace` line", prompt);
        Assert.DoesNotContain("@description", prompt);
        Assert.Contains("Change comments only", prompt);
    }

    [Fact]
    public void Build_ShouldBeEmpty_WithNothingToComment()
    {
        using var model = modelMgr.UseModel();

        Assert.Equal("", CommentsPrompt.Build(model, []));
    }
}
