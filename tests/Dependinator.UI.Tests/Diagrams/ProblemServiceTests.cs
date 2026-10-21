using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// The problem indicator's summary: the last parse's problems (compile errors) are errors, one
// group per project, counted per error; open cycles are warnings, as many as the Cycles panel lists
// (accepted ones and ones to fix later are decided on); architecture rules count once per broken
// rule, with their dependencies in the text; a model without problems has an empty summary. It
// follows model changes, but not while a parse or load is in progress.
public class ProblemServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());

    readonly Mock<IProgressService> progress = new();

    // The rule and cycle services only use their command service for changes, which these tests
    // make on the model directly.
    ProblemService NewService()
    {
        var commands = Mock.Of<ICommandService>();
        return new ProblemService(
            modelMgr,
            new CycleService(modelMgr, commands),
            new RuleService(modelMgr, commands),
            progress.Object
        );
    }

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

    // Two cycles: A <-> B in C, and X <-> Y in Z.
    void AddTwoCycles()
    {
        using var model = modelMgr.UseModel();
        foreach (var (container, first, second) in new[] { ("C", "A", "B"), ("Z", "X", "Y") })
        {
            var parent = AddNode(model, container, model.Root, NodeType.Assembly);
            var a = AddNode(model, $"{container}.{first}", parent);
            var b = AddNode(model, $"{container}.{second}", parent);
            AddLink(model, a, b);
            AddLink(model, b, a);
        }
    }

    [Fact]
    public void GetSummary_ShouldBeEmpty_WhenTheModelHasNoProblems()
    {
        using (var model = modelMgr.UseModel())
        {
            var parent = AddNode(model, "C", model.Root, NodeType.Assembly);
            AddLink(model, AddNode(model, "C.A", parent), AddNode(model, "C.B", parent));
        }

        var summary = NewService().GetSummary();

        Assert.True(summary.IsEmpty);
        Assert.Equal(0, summary.WarningCount);
        Assert.Equal(0, summary.ErrorCount);
    }

    [Fact]
    public void GetSummary_ShouldCountOpenCyclesAsWarnings()
    {
        AddTwoCycles();

        var summary = NewService().GetSummary();

        var group = Assert.Single(summary.Groups);
        Assert.Equal(ProblemKind.Cycles, group.Kind);
        Assert.Equal(ProblemSeverity.Warning, group.Severity);
        Assert.Equal("2 cycles", group.Text);
        Assert.Equal(2, summary.WarningCount);
        Assert.Equal(0, summary.ErrorCount);
    }

    [Fact]
    public void GetSummary_ShouldNotCountAcceptedCycles_OrOnesToFixLater()
    {
        AddTwoCycles();
        modelMgr.WithModel(m =>
        {
            m.AddAcceptedCycle(new AcceptedCycle("C", ["C.A", "C.B"], "Intended", IsToFix: false));
            m.AddAcceptedCycle(new AcceptedCycle("Z", ["Z.X", "Z.Y"], "Split Y", IsToFix: true));
        });

        Assert.True(NewService().GetSummary().IsEmpty);
    }

    [Fact]
    public void GetSummary_ShouldCountBrokenRules_WithTheirDependencies_AndSkipKeptOnes()
    {
        using (var model = modelMgr.UseModel())
        {
            var ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
            var data = AddNode(model, "Data", model.Root, NodeType.Assembly);
            var view = AddNode(model, "UI.View", ui);
            var form = AddNode(model, "UI.Form", ui);
            var repo = AddNode(model, "Data.Repo", data);
            AddLink(model, view, repo);
            AddLink(model, form, repo);
            model.AddRule(new ArchitectureRule(ui.Name, data.Name)); // Broken by two dependencies
            model.AddRule(new ArchitectureRule(data.Name, ui.Name)); // Kept
            model.AddRule(new ArchitectureRule("Gone", ui.Name)); // Unresolved, not counted
        }

        var group = Assert.Single(NewService().GetSummary().Groups);

        Assert.Equal(ProblemKind.RuleViolations, group.Kind);
        Assert.Equal(1, group.Count);
        Assert.Equal("1 broken architecture rule (2 dependencies)", group.Text);
    }

    [Fact]
    public void GetSummary_ShouldFollowModelChanges()
    {
        var service = NewService();
        Assert.True(service.GetSummary().IsEmpty);

        AddTwoCycles();
        Assert.Equal(2, service.GetSummary().WarningCount);

        modelMgr.WithModel(m => m.AddAcceptedCycle(new AcceptedCycle("C", ["C.A", "C.B"], "Intended", IsToFix: false)));
        Assert.Equal("1 cycle", Assert.Single(service.GetSummary().Groups).Text);
    }

    [Fact]
    public void GetSummary_ShouldKeepTheLastSummary_WhileAParseIsInProgress()
    {
        var service = NewService();
        Assert.True(service.GetSummary().IsEmpty);

        progress.Setup(p => p.IsDiscreetActive).Returns(true);
        AddTwoCycles();
        Assert.True(service.GetSummary().IsEmpty);

        progress.Setup(p => p.IsDiscreetActive).Returns(false);
        Assert.Equal(2, service.GetSummary().WarningCount);
    }

    [Fact]
    public void GetSummary_ShouldListParseProblemsAsErrors_OneGroupPerProject_BeforeTheCycles()
    {
        AddTwoCycles();
        modelMgr.WithModel(m =>
        {
            AddNode(m, "Api", m.Root, NodeType.Assembly);
            m.SetParseProblems([new("49 compile errors in Api", 49, "Api"), new("1 compile error in UI", 1, "UI")]);
        });

        var summary = NewService().GetSummary();

        Assert.Equal(50, summary.ErrorCount);
        Assert.Equal(2, summary.WarningCount);
        Assert.Equal(
            [ProblemKind.ParseErrors, ProblemKind.ParseErrors, ProblemKind.Cycles],
            summary.Groups.Select(g => g.Kind)
        );
        var api = summary.Groups[0];
        Assert.Equal(ProblemSeverity.Error, api.Severity);
        Assert.Equal("49 compile errors in Api", api.Text);
        Assert.Equal(NodeId.FromName("Api"), api.NodeId);
        Assert.Null(summary.Groups[1].NodeId); // UI is not in the model
    }

    [Fact]
    public void GetSummary_ShouldFollowANewParse()
    {
        var service = NewService();
        modelMgr.WithModel(m => m.SetParseProblems([new("1 compile error in UI", 1, "UI")]));
        Assert.Equal(1, service.GetSummary().ErrorCount);

        modelMgr.WithModel(m => m.SetParseProblems([]));
        Assert.True(service.GetSummary().IsEmpty);
    }

    // A badge leads from the top down: each problem counts at its node and every ancestor.
    [Fact]
    public void GetNodeProblems_ShouldCountEachProblemAtItsNode_AndAtEveryAncestor()
    {
        Node api;
        using (var model = modelMgr.UseModel())
        {
            var solution = AddNode(model, "Sln", model.Root, NodeType.Solution);
            var core = AddNode(model, "Sln.Core", solution, NodeType.Assembly);
            var a = AddNode(model, "Sln.Core.A", core);
            var b = AddNode(model, "Sln.Core.B", core);
            AddLink(model, a, b);
            AddLink(model, b, a); // A cycle in Core
            api = AddNode(model, "Sln.Api", solution, NodeType.Assembly);
            var handler = AddNode(model, "Sln.Api.Handler", api);
            AddLink(model, handler, a);
            model.AddRule(new ArchitectureRule(api.Name, core.Name)); // Broken by Handler
            model.SetParseProblems([new("3 compile errors in Api", 3, api.Name)]);
        }

        var problems = NewService().GetNodeProblems();

        Assert.Equal(new NodeProblems(0, 1), problems[NodeId.FromName("Sln.Core")]);
        Assert.Equal(new NodeProblems(0, 1), problems[NodeId.FromName("Sln.Api.Handler")]);
        Assert.Equal(new NodeProblems(3, 1), problems[api.Id]);
        Assert.Equal(new NodeProblems(3, 2), problems[NodeId.FromName("Sln")]);
        Assert.False(problems.ContainsKey(NodeId.FromName("Sln.Core.A")));
        Assert.False(problems.ContainsKey(NodeIdRoot()));
    }

    // With no container to put it on, a cycle among the top-level nodes marks each of them.
    [Fact]
    public void GetNodeProblems_ShouldMarkTheMembers_OfACycleAtTheTopLevel()
    {
        using (var model = modelMgr.UseModel())
        {
            var a = AddNode(model, "A", model.Root, NodeType.Assembly);
            var b = AddNode(model, "B", model.Root, NodeType.Assembly);
            AddLink(model, a, b);
            AddLink(model, b, a);
        }

        var problems = NewService().GetNodeProblems();

        Assert.Equal(new NodeProblems(0, 1), problems[NodeId.FromName("A")]);
        Assert.Equal(new NodeProblems(0, 1), problems[NodeId.FromName("B")]);
    }

    NodeId NodeIdRoot() => modelMgr.WithModel(m => m.Root.Id);
}
