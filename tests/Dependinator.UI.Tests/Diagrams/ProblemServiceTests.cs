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

// The problem indicator's summary: open cycles and cycles to fix later are warnings, accepted
// ones are not problems; architecture rules count once per broken rule, with their dependencies
// in the text; a model without problems has an empty summary. It follows model changes.
public class ProblemServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());

    ProblemService NewService()
    {
        var commands = new Mock<ICommandService>();
        commands
            .Setup(c => c.Do(It.IsAny<Command>(), true, true))
            .Callback<Command, bool, bool>((command, _, _) => modelMgr.WithModel(m => command.Execute(m)));
        return new ProblemService(
            modelMgr,
            new CycleService(modelMgr, commands.Object),
            new RuleService(modelMgr, commands.Object)
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
    public void GetSummary_ShouldCountCyclesToFixLater_ButNotAcceptedOnes()
    {
        AddTwoCycles();
        modelMgr.WithModel(m =>
        {
            m.AddAcceptedCycle(new AcceptedCycle("C", ["C.A", "C.B"], "Intended", IsToFix: false));
            m.AddAcceptedCycle(new AcceptedCycle("Z", ["Z.X", "Z.Y"], "Split Y", IsToFix: true));
        });

        var group = Assert.Single(NewService().GetSummary().Groups);

        Assert.Equal(1, group.Count);
        Assert.Equal("1 cycle to fix later", group.Text);
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
}
