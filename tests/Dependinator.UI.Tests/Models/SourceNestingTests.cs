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

    // A project holds a source in its own folder (a package.json beside the .csproj), and goes
    // before a root of the same folder
    [Fact]
    public void Parents_ShouldNestASource_InAProjectOfTheSameFolder()
    {
        AddRoot("App*sln");
        AddProject("Web", "/repo/src/web", "App*sln");
        AddRoot("web");
        AddRoot("client");

        var parents = Parents(
            ("src/web/Web.sln", "App*sln"),
            ("src/web/package.json", "web"),
            ("src/web/client/package.json", "client")
        );

        Assert.Equal("Web", parents["web"]);
        Assert.Equal("Web", parents["client"]);
    }

    // A root holds only the folders below its own: two sources in one folder stay side by side
    [Fact]
    public void Parents_ShouldKeepTwoSourcesOfOneFolder_SideBySide()
    {
        AddRoot("App*sln");
        AddRoot("tools");

        var parents = Parents(("App.sln", "App*sln"), ("package.json", "tools"));

        Assert.Equal("", parents["App*sln"]);
        Assert.Equal("", parents["tools"]);
    }

    // Two solutions in one folder with a project there (they can share it) stay side by side:
    // a project holds its own folder only for a source of another kind
    [Fact]
    public void Parents_ShouldKeepTwoSolutionsOfOneFolder_SideBySide_WithAProjectThere()
    {
        AddRoot("App*sln");
        AddProject("App", "/repo", "App*sln");
        AddRoot("Full*sln");
        AddProject("Full", "/repo", "Full*sln");

        var parents = Parents(("App.sln", "App*sln"), ("Full.sln", "Full*sln"));

        Assert.Equal("", parents["App*sln"]);
        Assert.Equal("", parents["Full*sln"]);
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

    // The user's choice goes before the folders: the top, or a node of another source
    [Fact]
    public void Parents_ShouldFollowTheUsersChoice()
    {
        AddRoot("App*sln");
        AddProject("Lib", "/repo/src/lib", "App*sln");
        AddProject("Web", "/repo/src/web", "App*sln");
        AddRoot("ui");
        AddRoot("infra");

        var parents = Parents(
            ("App.sln", "App*sln", null),
            ("src/lib/ui/package.json", "ui", "Web"),
            ("infra/main.bicep", "infra", "")
        );

        Assert.Equal("Web", parents["ui"]);
        Assert.Equal("", parents["infra"]);
    }

    // A choice of a node that is not in the model, or of the source's own, is Automatic
    [Theory]
    [InlineData("Gone")]
    [InlineData("ui")]
    [InlineData("ui.main*ts")]
    public void Parents_ShouldBeAutomatic_ForAChoiceThatCannotBe(string shownIn)
    {
        AddRoot("App*sln");
        AddRoot("ui");
        AddNode("ui.main*ts", "ui");

        var parents = Parents(("App.sln", "App*sln", null), ("src/ui/package.json", "ui", shownIn));

        Assert.Equal("App*sln", parents["ui"]);
    }

    // The solution is chosen to be inside the web project, which is in the solution's folder:
    // the choice wins, and the web project goes to the top
    [Fact]
    public void Parents_ShouldKeepTheUsersChoice_WhenTheSourcesWouldBeInEachOther()
    {
        AddRoot("App*sln");
        AddRoot("web");
        AddNode("web.main*ts", "web");

        var parents = Parents(("App.sln", "App*sln", "web"), ("src/web/package.json", "web", null));

        Assert.Equal("web", parents["App*sln"]);
        Assert.Equal("", parents["web"]);
    }

    // A project the parse no longer has (or of a source left out) is no place to be shown in
    [Fact]
    public void Parents_ShouldBeAutomatic_ForAChoiceOfAProjectThatIsGone()
    {
        AddRoot("App*sln");
        AddProject("Old", "/repo/src/old", "App*sln");
        AddRoot("ui");
        modelMgr.WithModel(m => m.UpdateStamp = m.UpdateStamp.AddSeconds(1));
        modelMgr.WithModel(m => m.Nodes[NodeId.FromName("App*sln")].UpdateStamp = m.UpdateStamp);

        var parents = Parents(("App.sln", "App*sln", null), ("src/ui/package.json", "ui", "Old"));

        Assert.Equal("App*sln", parents["ui"]);
    }

    // The web project's choice cannot be followed (its node is gone), so it is placed in the
    // solution's folder; the solution's own choice of the web project still wins
    [Fact]
    public void Parents_ShouldKeepAChoiceThatIsFollowed_OverOneThatIsNot()
    {
        AddRoot("App*sln");
        AddRoot("web");

        var parents = Parents(("App.sln", "App*sln", "web"), ("src/web/package.json", "web", "Gone"));

        Assert.Equal("web", parents["App*sln"]);
        Assert.Equal("", parents["web"]);
    }

    [Fact]
    public void Places_ShouldBeTheOtherSources_AndTheirProjects()
    {
        AddRoot("App*sln");
        AddProject("Lib", "/repo/src/lib", "App*sln");
        AddRoot("ui");
        AddRoot("infra");
        SourceNesting.Source[] sources =
        [
            new("App.sln", "App*sln"),
            new("src/lib/ui/package.json", "ui"),
            new("infra/main.bicep", "infra"),
        ];

        var places = modelMgr.WithModel(m => SourceNesting.Places(m, sources, "ui"));

        Assert.Equal(["App*sln", "Lib", "infra"], places.Select(p => p.NodeName));
        Assert.True(places[1].IsProject);
        Assert.StartsWith("Lib", places[1].Label);
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

    // The lines inside a moved root keep their route and description; the route's points are in
    // a node that moves along
    [Fact]
    public void Apply_ShouldKeepTheLinesInsideTheRoot()
    {
        AddRoot("App*sln");
        AddRoot("web");
        AddNode("web.a*ts", "web");
        AddNode("web.b*ts", "web");
        Pos[] points = [new(20, 30), new(40, 30)];
        using (var model = modelMgr.UseModel())
        {
            structure.AddOrUpdateLink(model, new Dependinator.Core.Parsing.Link("web.a*ts", "web.b*ts", new()));
            var line = model.Lines[LineId.From("web.a*ts", "web.b*ts")];
            line.SetSegmentPoints(points);
            line.IsSegmentsUserSet = true;
            line.SetDescription("calls", model.UpdateStamp);
        }

        using (var model = modelMgr.UseModel())
            SourceNesting.Apply(model, structure, Folder, [new("App.sln", "App*sln"), new("src/web/x.json", "web")]);

        modelMgr.WithModel(m =>
        {
            var line = m.Lines[LineId.From("web.a*ts", "web.b*ts")];
            Assert.Equal(points, line.SegmentPoints);
            Assert.True(line.IsSegmentsUserSet);
            Assert.Equal("calls", line.Description);
        });
    }

    [Fact]
    public void Apply_ShouldLeaveTheStructureVersion_WhenNothingMoves()
    {
        AddRoot("App*sln");
        AddRoot("web");
        var version = modelMgr.WithModel(m => m.StructureVersion);

        using (var model = modelMgr.UseModel())
            SourceNesting.Apply(model, structure, Folder, [new("App.sln", "App*sln"), new("/other/x.json", "web")]);

        Assert.Equal(version, modelMgr.WithModel(m => m.StructureVersion));
    }

    // A moved root can be saved before its new parent (a node made after it): it loads into
    // that parent all the same, with no second copy of the parent
    [Fact]
    public void Apply_ShouldLoadTheMovedRoot_IntoItsNewParent()
    {
        AddRoot("web");
        AddRoot("App*sln");
        AddProject("Lib", "/repo/src/lib", "App*sln");

        using (var model = modelMgr.UseModel())
            SourceNesting.Apply(
                model,
                structure,
                Folder,
                [new("App.sln", "App*sln"), new("src/lib/package.json", "web")]
            );

        var dto = modelMgr.WithModel(m => m.SerializeToDto());
        var names = dto.Nodes.Select(n => n.Name).ToList();
        Assert.True(names.IndexOf("web") < names.IndexOf("Lib"));

        var loaded = new ModelMgr(new StateMgr());
        using (var model = loaded.UseModel())
        {
            model.Path = "/repo/.workspace";
            foreach (var node in dto.Nodes)
                structure.SetNodeDto(model, node);
        }
        loaded.WithModel(m =>
        {
            var lib = m.Nodes[NodeId.FromName("Lib")];
            Assert.Same(lib, m.Nodes[NodeId.FromName("web")].Parent);
            Assert.Equal("App*sln", lib.Parent.Name);
            Assert.Same(lib, Assert.Single(m.Nodes[NodeId.FromName("App*sln")].Children));
            Assert.Equal(NodeType.Assembly, lib.Type);
            // Only the Externals, where the stand-in for the project was made, is added
            Assert.Equal(names.Append("$Externals").Order(), m.Nodes.Values.Select(n => n.Name).Order());
        });
    }

    // Saved relative in a workspace model, which nests by it, and nowhere else
    [Theory]
    [InlineData("/repo/.workspace", "src/lib")]
    [InlineData("/repo/App.sln", null)]
    public void SerializeToDto_ShouldSaveAProjectsFolder_OnlyInAWorkspaceModel(string path, string? saved)
    {
        AddRoot("App*sln");
        AddProject("Lib", "/repo/src/lib", "App*sln");
        modelMgr.WithModel(m => m.Path = path);

        var dto = modelMgr.WithModel(m => m.SerializeToDto());

        Assert.Equal(saved, dto.Nodes.Single(n => n.Name == "Lib").Properties.ProjectFolder);
    }

    IReadOnlyDictionary<string, string> Parents(params (string Key, string RootName)[] sources) =>
        Parents([.. sources.Select(s => (s.Key, s.RootName, (string?)null))]);

    IReadOnlyDictionary<string, string> Parents(params (string Key, string RootName, string? ShownIn)[] sources) =>
        modelMgr.WithModel(m =>
            SourceNesting.Parents(
                m,
                Folder,
                [.. sources.Select(s => new SourceNesting.Source(s.Key, s.RootName, s.ShownIn))]
            )
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
