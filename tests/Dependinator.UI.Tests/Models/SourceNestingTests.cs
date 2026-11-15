using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using ParsedNode = Dependinator.Core.Parsing.Node;

namespace Dependinator.UI.Tests.Models;

// Where a workspace model shows each source: inside the deepest node of another source whose
// folder holds the source's folder.
public class SourceNestingTests
{
    const string Folder = "/repo";

    readonly IModelMgr modelMgr = new ModelMgr(new StateMgr());
    readonly StructureService structure = new(new LineService());

    [Fact]
    public void Parents_ShouldBeTheDeepestHolder_OfAnotherSource()
    {
        AddRoot("App*sln");
        AddProject("Lib", "/repo/src/lib", "App*sln");
        AddProject("Lib.Inner", "/repo/src/lib/inner", "App*sln");
        AddRoot("ui");
        AddRoot("infra");

        var parents = Parents(
            ("App.sln", "App*sln"),
            ("src/lib/inner/ui/package.json", "ui"),
            ("infra/main.bicep", "infra")
        );

        Assert.Equal("", parents["App*sln"]);
        Assert.Equal("Lib.Inner", parents["ui"]);
        Assert.Equal("App*sln", parents["infra"]);
    }

    // A project's folder holds only the folders below it, like a root's
    [Fact]
    public void Parents_ShouldNotNestASource_InAProjectOfTheSameFolder()
    {
        AddRoot("App*sln");
        AddProject("Web", "/repo/src/web", "App*sln");
        AddRoot("web");

        var parents = Parents(("src/app/App.sln", "App*sln"), ("src/web/package.json", "web"));

        Assert.Equal("", parents["web"]);
    }

    // A source outside the workspace folder (a second workspace folder) has its whole path as key
    [Fact]
    public void Parents_ShouldKeepASourceOutsideTheWorkspace_AtTheTop()
    {
        AddRoot("App*sln");
        AddRoot("tools");

        var parents = Parents(("App.sln", "App*sln"), ("/other/tools/package.json", "tools"));

        Assert.Equal("", parents["tools"]);
    }

    // A project the parse no longer has is removed after the parse: it holds nothing
    [Fact]
    public void Parents_ShouldLeaveOutProjects_ThatTheParseNoLongerHas()
    {
        AddRoot("App*sln");
        AddProject("Old", "/repo/src", "App*sln");
        AddRoot("web");
        modelMgr.WithModel(m => m.UpdateStamp = m.UpdateStamp.AddSeconds(1));
        modelMgr.WithModel(m => m.Nodes[NodeId.FromName("App*sln")].UpdateStamp = m.UpdateStamp);

        var parents = Parents(("App.sln", "App*sln"), ("src/web/package.json", "web"));

        Assert.Equal("App*sln", parents["web"]);
    }

    // A solution in src/app with a project outside its folder (src/lib) that holds a TypeScript
    // project's folder, while that project's folder holds the solution: only one can be inside
    // the other, so the first stays at the top.
    [Fact]
    public void Parents_ShouldNotNestTwoSourcesInEachOther()
    {
        AddRoot("App*sln");
        AddProject("Lib", "/repo/src/lib", "App*sln");
        AddRoot("lib");
        AddProject("lib.Part", "/repo/src", "lib");

        var parents = Parents(("src/app/App.sln", "App*sln"), ("src/lib/x/package.json", "lib"));

        Assert.Equal("", parents["App*sln"]);
        Assert.Equal("Lib", parents["lib"]);
    }

    [Fact]
    public void Apply_ShouldMoveTheRoot_AndPlaceItAnew()
    {
        AddRoot("App*sln");
        AddRoot("web");
        AddNode("web.main*ts", "web");
        modelMgr.WithModel(m =>
        {
            m.Nodes[NodeId.FromName("web")].Boundary = new Rect(0, 0, 100, 100);
            m.Nodes[NodeId.FromName("web.main*ts")].Boundary = new Rect(10, 10, 60, 60);
        });

        using (var model = modelMgr.UseModel())
            SourceNesting.Apply(model, structure, Folder, [new("App.sln", "App*sln"), new("src/web/x.json", "web")]);

        modelMgr.WithModel(m =>
        {
            var web = m.Nodes[NodeId.FromName("web")];
            Assert.Equal("App*sln", web.Parent.Name);
            Assert.Equal(Rect.None, web.Boundary);
            Assert.True(web.Parent.IsChildrenLayoutRequired);
            // What is inside keeps its place
            Assert.Equal(new Rect(10, 10, 60, 60), m.Nodes[NodeId.FromName("web.main*ts")].Boundary);
        });
    }

    IReadOnlyDictionary<string, string> Parents(params (string Key, string RootName)[] sources) =>
        modelMgr.WithModel(m =>
            SourceNesting.Parents(m, Folder, [.. sources.Select(s => new SourceNesting.Source(s.Key, s.RootName))])
        );

    void AddRoot(string name) => Add(new ParsedNode(name, new() { Type = NodeType.Solution }));

    void AddProject(string name, string folder, string parent) =>
        Add(
            new ParsedNode(
                name,
                new()
                {
                    Type = NodeType.Assembly,
                    Parent = parent,
                    ProjectFolder = folder,
                }
            )
        );

    void AddNode(string name, string parent) =>
        Add(new ParsedNode(name, new() { Type = NodeType.SourceFile, Parent = parent }));

    void Add(ParsedNode node)
    {
        using var model = modelMgr.UseModel();
        structure.AddOrUpdateNode(model, node);
    }
}
