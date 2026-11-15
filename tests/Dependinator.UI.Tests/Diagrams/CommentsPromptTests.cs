using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.NodeHelp;
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
    public void Build_ShouldAskForAProjectsDescription_WhenTheProjectHasAPlaceForIt()
    {
        using var model = modelMgr.UseModel();
        var (app, _) = Setup(model);
        app.Update(new ParsedNode("App", new() { FileSpan = new FileSpan("/repo/src/Usings.cs", 0, 0) }));
        app.SetDescription("Runs the app.");

        var targets = CommentsPrompt.Targets([app], isBelow: false);
        Assert.Equal(["App"], targets.Select(n => n.Name));
        Assert.Equal(6, CommentsPrompt.Targets([app], isBelow: true).Count);

        var prompt = CommentsPrompt.Build(model, targets).ReplaceLineEndings("\n");
        Assert.StartsWith("Please add or improve the code comments of the projects listed below.", prompt);
        Assert.Contains(
            "### project App (dll)\nIts description goes in /repo/src/Usings.cs, line 1\nDescription now: \"Runs the app.\"\n",
            prompt
        );
        Assert.Contains("`[assembly: AssemblyDescription(\"...\")]` attribute", prompt);
        Assert.Contains("`<Description>`", prompt);
        Assert.DoesNotContain("A namespace's comment goes", prompt);

        var all = CommentsPrompt.Build(model, CommentsPrompt.Targets([app], isBelow: true));
        Assert.StartsWith("Please add or improve the code comments of the projects, namespaces and types listed", all);
        Assert.Contains("A namespace's comment goes", all);
    }

    // A TypeScript or JavaScript project: its files and types are commented, with where the
    // parser reads a file's comment from.
    [Fact]
    public void Build_ShouldAskForFileAndTypeComments_ForATypeScriptProject()
    {
        using var model = modelMgr.UseModel();
        var project = AddNode(model, "app", model.Root, NodeType.Solution);
        var file = AddScriptNode(model, "app.main*ts", project, NodeType.SourceFile, 0);
        AddScriptNode(model, "app.main*ts.Server", file, NodeType.ClassType, 4);
        AddScriptNode(model, "app.main*ts.start", file, NodeType.FunctionMember, 20);

        var targets = CommentsPrompt.Targets([project], isBelow: true);
        var prompt = CommentsPrompt.Build(model, targets).ReplaceLineEndings("\n");

        Assert.Equal(["app.main*ts", "app.main*ts.Server"], targets.Select(n => n.Name));
        Assert.StartsWith("Please add or improve the code comments of the files and types listed below.", prompt);
        Assert.Contains("What the file or type is for", prompt);
        Assert.Contains("file's comment goes at the very top of the file", prompt);
        Assert.Contains("JSDoc comment", prompt);
        Assert.DoesNotContain("`namespace` line", prompt);
        Assert.Contains("### file main.ts\nDeclared in", prompt);
    }

    static Node AddScriptNode(IModel model, string name, Node parent, NodeType type, int line)
    {
        var node = AddNode(model, name, parent, type);
        node.Update(new ParsedNode(name, new() { FileSpan = new FileSpan("/repo/app/main.ts", line, line + 10) }));
        return node;
    }

    [Fact]
    public void Build_ShouldBeEmpty_WithNothingToComment()
    {
        using var model = modelMgr.UseModel();

        Assert.Equal("", CommentsPrompt.Build(model, []));
    }
}
