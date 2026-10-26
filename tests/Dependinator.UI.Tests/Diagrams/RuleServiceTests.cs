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

// Architecture rules: a link from inside the rule's "from" node to inside its "to" node is a
// violation, listed per type pair with link counts and with its lines for highlighting; a rule
// whose node is not in the model is kept and reported as unresolved.
public class RuleServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());

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
    public void Compute_ShouldListViolationsPerTypePair_AndTheirLines()
    {
        using var model = modelMgr.UseModel();
        var ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
        var data = AddNode(model, "Data", model.Root, NodeType.Assembly);
        var view = AddNode(model, "UI.View", ui);
        var repo = AddNode(model, "Data.Repo", data);
        var viewMethod = AddNode(model, "UI.View.Load()", view, NodeType.MethodMember);
        var repoMethod = AddNode(model, "Data.Repo.Get()", repo, NodeType.MethodMember);
        AddLink(model, viewMethod, repoMethod);
        AddLink(model, view, repo);
        AddLink(model, repo, view); // The other direction is allowed
        model.AddRule(new ArchitectureRule(ui.Name, data.Name));

        var (reports, lineIds) = RuleService.Compute(model);

        var report = Assert.Single(reports);
        Assert.True(report.IsResolved);
        var violation = Assert.Single(report.Violations);
        Assert.Equal(view.Id, violation.SourceId);
        Assert.Equal(repo.Id, violation.TargetId);
        Assert.Equal(2, violation.LinkCount);
        Assert.Contains(LineId.From(ui.Name, data.Name), lineIds);
        Assert.DoesNotContain(LineId.From(data.Name, ui.Name), lineIds);
    }

    [Fact]
    public void Compute_ShouldReportAKeptRule_AndAnUnresolvedOne()
    {
        using var model = modelMgr.UseModel();
        var ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
        var data = AddNode(model, "Data", model.Root, NodeType.Assembly);
        AddLink(model, AddNode(model, "UI.View", ui), AddNode(model, "Data.Repo", data));
        model.AddRule(new ArchitectureRule(data.Name, ui.Name));
        model.AddRule(new ArchitectureRule("Gone", ui.Name));

        var (reports, lineIds) = RuleService.Compute(model);

        Assert.Equal(2, reports.Count);
        Assert.True(reports[0].IsResolved);
        Assert.Empty(reports[0].Violations);
        Assert.False(reports[1].IsResolved);
        Assert.Equal("Gone", reports[1].FromDisplay);
        Assert.Empty(lineIds);
    }

    [Fact]
    public void AddRule_ShouldGoThroughTheCommandStack_AndIgnoreDuplicatesAndSelfRules()
    {
        Node ui,
            data;
        using (var model = modelMgr.UseModel())
        {
            ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
            data = AddNode(model, "Data", model.Root, NodeType.Assembly);
        }
        var commands = new Mock<ICommandService>();
        commands
            .Setup(c => c.Do(It.IsAny<Command>(), true, true))
            .Callback<Command, bool, bool>((command, _, _) => modelMgr.WithModel(m => command.Execute(m)));
        var service = new RuleService(modelMgr, commands.Object);

        AssertOk(service.AddRule(ui.Id, data.Id));
        AssertError(service.AddRule(ui.Id, data.Id)); // Same rule again is rejected, and says so
        AssertError(service.AddRule(ui.Id, ui.Id)); // A node cannot be forbidden to use itself

        var rule = Assert.Single(modelMgr.WithModel(m => m.Rules));
        Assert.Equal(new ArchitectureRule(ui.Name, data.Name), rule);
        commands.Verify(c => c.Do(It.IsAny<Command>(), true, true), Times.Once);

        service.RemoveRule(rule);
        Assert.Empty(modelMgr.WithModel(m => m.Rules));
    }

    [Fact]
    public void GetViolatingLineIds_ShouldIncludeLinesCreatedLater_ForViolatingLinks()
    {
        Node ui,
            data;
        Link link;
        using (var model = modelMgr.UseModel())
        {
            ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
            data = AddNode(model, "Data", model.Root, NodeType.Assembly);
            link = AddLink(model, ui, data);
            model.AddRule(new ArchitectureRule(ui.Name, data.Name));
        }

        var service = new RuleService(modelMgr, Moq.Mock.Of<ICommandService>());
        Assert.Contains(LineId.From(ui.Name, data.Name), service.GetViolatingLineIds());

        // A cousin line materialized at another zoom carries the same link without any structure change
        var cousinId = LineId.From("Cousin", data.Name);
        using (var model = modelMgr.UseModel())
        {
            var cousin = new Line(ui, data, id: cousinId);
            cousin.Add(link);
            link.AddLine(cousin);
            model.TryAddLine(cousin);
        }

        Assert.Contains(cousinId, service.GetViolatingLineIds());
    }

    [Fact]
    public void Model_ShouldRoundTripRulesThroughTheDto_AndDropThemOnClear()
    {
        using var model = modelMgr.UseModel();
        model.AddRule(new ArchitectureRule("A", "B"));
        var dto = model.SerializeToDto();

        var rule = Assert.Single(dto.Rules);
        Assert.Equal(("A", "B"), (rule.From, rule.To));

        model.Clear();
        Assert.Empty(model.Rules);
        model.SetFromDto("path", dto);
        Assert.Equal([new ArchitectureRule("A", "B")], model.Rules);
    }

    [Fact]
    public void Compute_ShouldLeaveOutAnAllowedDependency_AndListItUnderTheRuleWithItsLinkCount()
    {
        using var model = modelMgr.UseModel();
        var ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
        var data = AddNode(model, "Data", model.Root, NodeType.Assembly);
        var view = AddNode(model, "UI.View", ui);
        var page = AddNode(model, "UI.Page", ui);
        var repo = AddNode(model, "Data.Repo", data);
        var viewMethod = AddNode(model, "UI.View.Load()", view, NodeType.MethodMember);
        AddLink(model, viewMethod, repo);
        AddLink(model, view, repo);
        AddLink(model, page, repo);
        var rule = new ArchitectureRule(ui.Name, data.Name);
        model.AddRule(rule);
        model.AddAllowedViolation(
            new AllowedViolation(ui.Name, data.Name, view.Name, repo.Name, "Reads settings only")
        );
        model.AddAllowedViolation(new AllowedViolation(ui.Name, data.Name, "UI.Gone", repo.Name, ""));

        var (reports, lineIds) = RuleService.Compute(model);

        var report = Assert.Single(reports);
        var violation = Assert.Single(report.Violations);
        Assert.Equal(page.Id, violation.SourceId);
        Assert.Equal((page.Name, repo.Name), (violation.SourceNodeName, violation.TargetNodeName));
        Assert.Equal(2, report.Allowed.Count);
        var allowed = report.Allowed.Single(a => a.Allowed.SourceName == view.Name);
        Assert.Equal(2, allowed.LinkCount);
        Assert.Equal(view.Id, allowed.SourceId);
        Assert.Equal("Reads settings only", allowed.Allowed.Reason);
        var gone = report.Allowed.Single(a => a.Allowed.SourceName == "UI.Gone");
        Assert.True(gone.IsGone);
        Assert.Null(gone.SourceId);
        Assert.NotEmpty(lineIds);

        // With every dependency allowed, nothing is drawn as breaking the rule.
        model.AddAllowedViolation(new AllowedViolation(ui.Name, data.Name, page.Name, repo.Name, ""));
        var (allAllowed, noLineIds) = RuleService.Compute(model);
        Assert.Empty(Assert.Single(allAllowed).Violations);
        Assert.Empty(noLineIds);
    }

    [Fact]
    public async Task AllowAndDisallow_ShouldGoThroughTheCommandStack_AndRemovingTheRuleTakesItsAllowedAlong()
    {
        Node ui,
            data,
            view,
            repo;
        using (var model = modelMgr.UseModel())
        {
            ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
            data = AddNode(model, "Data", model.Root, NodeType.Assembly);
            view = AddNode(model, "UI.View", ui);
            repo = AddNode(model, "Data.Repo", data);
            AddLink(model, view, repo);
            model.AddRule(new ArchitectureRule(ui.Name, data.Name));
        }
        var commands = new CommandService(Mock.Of<IApplicationEvents>(), modelMgr);
        var service = new RuleService(modelMgr, commands);
        var report = Assert.Single(service.GetReports());

        service.Allow(report.Rule, Assert.Single(report.Violations), " Reads settings only ");

        report = Assert.Single(service.GetReports());
        Assert.Empty(report.Violations);
        var allowed = Assert.Single(report.Allowed).Allowed;
        Assert.Equal(new AllowedViolation(ui.Name, data.Name, view.Name, repo.Name, "Reads settings only"), allowed);
        Assert.Equal(0, service.ViolationCount);

        service.SetAllowedReason(allowed, "Settings only");
        Assert.Equal("Settings only", Assert.Single(Assert.Single(service.GetReports()).Allowed).Allowed.Reason);

        service.RemoveRule(report.Rule);
        Assert.Empty(modelMgr.WithModel(m => m.AllowedViolations));
        await commands.Undo();
        Assert.Equal("Settings only", Assert.Single(modelMgr.WithModel(m => m.AllowedViolations)).Reason);

        service.Disallow(Assert.Single(modelMgr.WithModel(m => m.AllowedViolations)));
        Assert.Single(Assert.Single(service.GetReports()).Violations);
    }

    [Fact]
    public void Model_ShouldRoundTripAllowedViolationsThroughTheDto_AndDropThemOnClear()
    {
        using var model = modelMgr.UseModel();
        var allowed = new AllowedViolation("A", "B", "A.X", "B.Y", "Because");
        model.AddAllowedViolation(allowed);
        var dto = model.SerializeToDto();

        var saved = Assert.Single(dto.AllowedViolations);
        Assert.Equal(
            ("A", "B", "A.X", "B.Y", "Because"),
            (saved.From, saved.To, saved.Source, saved.Target, saved.Reason)
        );

        model.Clear();
        Assert.Empty(model.AllowedViolations);
        model.SetFromDto("path", dto);
        Assert.Equal([allowed], model.AllowedViolations);
    }
}
