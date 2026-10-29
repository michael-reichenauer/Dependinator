using Dependinator.Core.ModelQueries;
using Dependinator.Core.Parsing;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using FileSpan = Dependinator.Core.Parsing.FileSpan;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;
using ParsedNode = Dependinator.Core.Parsing.Node;

namespace Dependinator.UI.Tests.Diagrams;

// The questions an AI agent asks about the open model: dependencies grouped where both sides
// meet, paths, cycles, rules and a rule checked before it is added, nodes found by name or title,
// and the process-wide service answering on the latest scope.
public class ModelQueriesTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly ModelQueryService service = new();
    readonly Mock<IWorkspaceFileService> workspace = new();
    readonly ModelQueries queries;

    // UI (assembly): UI.Views.View (with Load()) and UI.Models.Item; Data (assembly): Data.Repo
    // (with Get()). View uses Repo twice (one from Load() to Get()) and Item once.
    readonly Node ui;
    readonly Node views;
    readonly Node view;
    readonly Node models;
    readonly Node item;
    readonly Node data;
    readonly Node repo;

    public ModelQueriesTests()
    {
        queries = Create(modelMgr);
        using var model = modelMgr.UseModel();
        ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
        views = AddNode(model, "UI.Views", ui, NodeType.Namespace);
        view = AddNode(model, "UI.Views.View", views);
        var load = AddNode(model, "UI.Views.View.Load()", view, NodeType.MethodMember);
        AddNode(model, "UI.Views.View.Save()", view, NodeType.MethodMember);
        models = AddNode(model, "UI.Models", ui, NodeType.Namespace);
        item = AddNode(model, "UI.Models.Item", models);
        data = AddNode(model, "Data", model.Root, NodeType.Assembly);
        repo = AddNode(model, "Data.Repo", data);
        var get = AddNode(model, "Data.Repo.Get()", repo, NodeType.MethodMember);
        AddLink(model, load, get);
        AddLink(model, view, repo);
        AddLink(model, view, item);
        AddLink(model, load, view); // Inside the subject: never a dependency
    }

    ModelQueries Create(ModelMgr mgr) =>
        new(
            mgr,
            new NodeSearchService(mgr),
            new CycleService(mgr, Mock.Of<ICommandService>()),
            new RuleService(mgr, Mock.Of<ICommandService>()),
            workspace.Object,
            service
        );

    static Node AddNode(IModel model, string name, Node parent, NodeType type = NodeType.ClassType)
    {
        var node = new Node(name, parent) { Boundary = new Rect(0, 0, 100, 100), Type = type };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static Link AddLink(IModel model, Node source, Node target)
    {
        var link = new Link(source, target);
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        new LineService().AddLinesFromSourceToTarget(model, link);
        return link;
    }

    [Fact]
    public void GetDependencies_ShouldGroupOutgoingWhereTheSidesMeet()
    {
        var result = AssertOk(queries.GetDependencies(views.Name, isIncoming: false));

        Assert.Equal(2, result.GroupCount);
        Assert.Equal([data.Name, models.Name], result.Groups.Select(g => g.Node.Name));
        var toData = result.Groups[0];
        Assert.Equal(2, toData.LinkCount);
        Assert.Equal([new UnitDependency(view.Name, repo.Name, 2)], toData.Examples);
        Assert.Equal(1, result.Groups[1].LinkCount);
    }

    [Fact]
    public void GetDependencies_ShouldGroupIncomingWhereTheSidesMeet()
    {
        var result = AssertOk(queries.GetDependencies(repo.Name, isIncoming: true));

        var group = Assert.Single(result.Groups);
        Assert.Equal(ui.Name, group.Node.Name);
        Assert.Equal("Assembly", group.Node.Type);
        Assert.Equal(2, group.LinkCount);
    }

    [Fact]
    public void CheckRule_ShouldReportViolations_WithoutAddingTheRule()
    {
        var result = AssertOk(queries.CheckRule(ui.Name, data.Name));

        Assert.False(result.IsRule);
        Assert.True(result.IsResolved);
        Assert.Equal(1, result.ViolationCount);
        Assert.Equal(2, result.LinkCount);
        Assert.Equal([new UnitDependency(view.Name, repo.Name, 2)], result.Violations);
        Assert.Empty(modelMgr.WithModel(m => m.Rules));
        Assert.Equal(0, AssertOk(queries.CheckRule(data.Name, ui.Name)).ViolationCount);
    }

    [Fact]
    public void CheckRule_ShouldKnowAnExistingRule_AndItsAllowedDependencies()
    {
        modelMgr.WithModel(m =>
        {
            m.AddRule(new ArchitectureRule(ui.Name, data.Name));
            m.AddAllowedViolation(new AllowedViolation(ui.Name, data.Name, view.Name, repo.Name, "Legacy"));
        });

        var result = AssertOk(queries.CheckRule(ui.Name, data.Name));
        var listed = Assert.Single(AssertOk(queries.ListRules()));

        Assert.True(result.IsRule);
        Assert.Equal(0, result.ViolationCount);
        Assert.Equal([new AllowedDependency(view.Name, repo.Name, "Legacy", 2)], result.Allowed);
        Assert.Equal(result, listed with { Violations = result.Violations, Allowed = result.Allowed });
    }

    [Fact]
    public void CheckRule_ShouldRefuseNestedNodes()
    {
        AssertError(queries.CheckRule(ui.Name, view.Name));
        AssertError(queries.CheckRule(view.Name, view.Name));
        AssertError(queries.CheckRule(view.Name + ".Load()", view.Name + ".Save()")); // One type
    }

    [Fact]
    public void FindNode_ShouldAcceptATitle_AndSayWhenNoneMatches()
    {
        using var model = modelMgr.UseModel();

        Assert.Equal(view, AssertOk(ModelQueries.FindNode(model, view.LongName.ToUpperInvariant())));
        var error = AssertError(ModelQueries.FindNode(model, "Nothing.Like.This"));
        Assert.Contains("search", error.Message);
        view.SetAlias("Main Screen");
        Assert.Equal(view, AssertOk(ModelQueries.FindNode(model, "main screen")));
    }

    [Fact]
    public void FindPaths_ShouldListHopsByName()
    {
        var result = AssertOk(queries.FindPaths(ui.Name, data.Name));

        Assert.Equal("Found", result.Status);
        Assert.Equal([view.Name, repo.Name], Assert.Single(result.Paths));
        Assert.Equal("NotFound", AssertOk(queries.FindPaths(data.Name, ui.Name)).Status);
    }

    [Fact]
    public void ListCycles_ShouldListOpenAndAcceptedCycles()
    {
        modelMgr.WithModel(m =>
        {
            AddLink(m, item, view); // Views and Models in a cycle
            m.AddAcceptedCycle(new AcceptedCycle(ui.Name, [ui.Name, data.Name], "Gone"));
        });

        var list = AssertOk(queries.ListCycles());

        Assert.Equal(2, list.Count);
        var (open, accepted) = (list.Cycles[0], list.Cycles[1]);
        Assert.Equal(("Open", ui.Name, null, 2), (open.Status, open.Container, open.Reason, open.MemberCount));
        Assert.Equal([models.Name, views.Name], open.Members);
        Assert.Equal(("Accepted", false), (accepted.Status, accepted.StillOccurs));
    }

    [Fact]
    public void ListCycles_ShouldMarkADecisionOutgrown_WhenANodeJoinedItsCycle()
    {
        modelMgr.WithModel(m =>
        {
            var shared = AddNode(m, "UI.Shared", ui, NodeType.Namespace);
            var helper = AddNode(m, "UI.Shared.Helper", shared);
            AddLink(m, item, helper);
            AddLink(m, helper, view); // Views, Models and Shared in a cycle
            m.AddAcceptedCycle(new AcceptedCycle(ui.Name, [views.Name, models.Name], "Intended"));
        });

        var list = AssertOk(queries.ListCycles());

        Assert.Equal(["Open", "Accepted"], list.Cycles.Select(c => c.Status));
        Assert.Equal([false, true], list.Cycles.Select(c => c.IsOutgrown));
        Assert.Equal(3, list.Cycles[0].MemberCount);
    }

    [Fact]
    public void GetChildren_ShouldListTheTopLevel_ForAnEmptyName()
    {
        var result = AssertOk(queries.GetChildren(""));

        Assert.Equal("", result.Node.Name);
        Assert.Equal([data.Name, ui.Name], result.Children.Select(c => c.Name));
        Assert.Equal(2, AssertOk(queries.GetChildren(ui.Name)).ChildCount);
    }

    [Fact]
    public void GetModel_ShouldSummarize_AndFailWithoutAModel()
    {
        var summary = AssertOk(queries.GetModel());
        var empty = Create(new ModelMgr(new StateMgr()));

        Assert.Equal(10, summary.NodeCount);
        Assert.Equal(4, summary.LinkCount);
        Assert.Equal(2, summary.TopLevel.Count);
        AssertError(empty.GetModel());
    }

    [Fact]
    public async Task Service_ShouldAnswerOnTheLatestOpenScope()
    {
        var other = Create(new ModelMgr(new StateMgr()));

        AssertError(await service.GetModelAsync());
        queries.Activate();
        AssertOk(await service.GetModelAsync());
        other.Activate();
        AssertError(await service.GetModelAsync()); // The other scope has no model
        other.Dispose(); // Back to the one opened before
        AssertOk(await service.GetModelAsync());
        queries.Dispose();
        var error = AssertError(await service.GetModelAsync());

        Assert.Contains("No Dependinator diagram is open", error.Message);
    }

    [Fact]
    public async Task ReadSourceAsync_ShouldNumberTheNodesLines_WithThePathFromTheModelsFolder()
    {
        SetSource(repo, "/repo/src/Repo.cs", 2, 3);
        workspace.Setup(w => w.ReadSourceFileAsync("/repo/src/Repo.cs")).ReturnsAsync((Result<string>)"a\r\nb\nc\nd");

        var source = AssertOk(await queries.ReadSourceAsync("Data.Repo"));

        Assert.Equal("Data.Repo", source.Node.Name);
        Assert.Equal("src/Repo.cs", source.Path);
        Assert.Equal((2, 3, false), (source.StartLine, source.EndLine, source.IsCut));
        Assert.Equal("    2  b\n    3  c", source.Text);
    }

    [Fact]
    public async Task ReadSourceAsync_ShouldCutLongCode()
    {
        var lineCount = ModelQueries.MaxSourceLines + 50;
        SetSource(repo, "/repo/src/Repo.cs", 1, lineCount);
        var file = string.Join("\n", Enumerable.Range(1, lineCount).Select(n => $"line {n}"));
        workspace.Setup(w => w.ReadSourceFileAsync("/repo/src/Repo.cs")).ReturnsAsync((Result<string>)file);

        var source = AssertOk(await queries.ReadSourceAsync("Data.Repo"));

        Assert.True(source.IsCut);
        Assert.Equal(lineCount, source.EndLine);
        Assert.Equal(ModelQueries.MaxSourceLines, source.Text.Split('\n').Length);
    }

    [Fact]
    public async Task ReadSourceAsync_ShouldFail_ForANodeWithoutCodeOfItsOwn()
    {
        var error = AssertError(await queries.ReadSourceAsync("UI.Views"));

        Assert.Equal("UI.Views has no code of its own (a namespace): read the types in it.", error.Message);
        workspace.Verify(w => w.ReadSourceFileAsync(It.IsAny<string>()), Times.Never);
    }

    void SetSource(Node node, string path, int startLine, int endLine)
    {
        using var model = modelMgr.UseModel();
        model.Path = "/repo/Demo.sln";
        node.Update(new ParsedNode(node.Name, new() { FileSpan = new FileSpan(path, startLine, endLine) }));
    }
}
