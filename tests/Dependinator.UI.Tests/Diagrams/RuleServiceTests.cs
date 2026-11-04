using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Rules;
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

    [Fact]
    public void Model_ShouldRoundTripRuleReasonsAndDismissedRulesThroughTheDto()
    {
        using var model = modelMgr.UseModel();
        model.AddRule(new ArchitectureRule("A", "B", "Keeps A free of B."));
        model.AddDismissedRule(new DismissedRule("B", "A", "Not intended."));
        var dto = model.SerializeToDto();

        model.Clear();
        Assert.Empty(model.DismissedRules);
        model.SetFromDto("path", dto);

        Assert.Equal("Keeps A free of B.", Assert.Single(model.Rules).Reason);
        Assert.Equal(new DismissedRule("B", "A", "Not intended."), Assert.Single(model.DismissedRules));
        Assert.Equal("Not intended.", model.DismissedRules[0].Reason);
    }

    [Fact]
    public void RemoveRuleCommand_ShouldDismissTheRule_AndUndoShouldBringItBack()
    {
        using var model = modelMgr.UseModel();
        var rule = new ArchitectureRule("A", "B", "Why.");
        model.AddRule(rule);
        var command = new RemoveRuleCommand(new ArchitectureRule("A", "B"));

        command.Execute(model);
        Assert.Empty(model.Rules);
        Assert.Equal("Removed from the rules. Why.", Assert.Single(model.DismissedRules).Reason);

        command.Revert(model);
        Assert.Equal("Why.", Assert.Single(model.Rules).Reason);
        Assert.Empty(model.DismissedRules);
    }

    [Fact]
    public void AddRuleCommand_ShouldTakeBackADismissal_AndUndoShouldRestoreIt()
    {
        using var model = modelMgr.UseModel();
        model.AddDismissedRule(new DismissedRule("A", "B", "Said no."));
        var command = new AddRuleCommand(new ArchitectureRule("A", "B"));

        command.Execute(model);
        Assert.Empty(model.DismissedRules);

        command.Revert(model);
        Assert.Empty(model.Rules);
        Assert.Equal("Said no.", Assert.Single(model.DismissedRules).Reason);
    }

    [Fact]
    public void SetRuleReasonCommand_ShouldReplaceTheReason_InPlace()
    {
        using var model = modelMgr.UseModel();
        model.AddRule(new ArchitectureRule("A", "B"));
        model.AddRule(new ArchitectureRule("C", "D"));
        var before = model.Rules[0];
        var command = new SetRuleReasonCommand(before, before with { Reason = "New." });

        command.Execute(model);
        Assert.Equal(["New.", ""], model.Rules.Select(r => r.Reason));

        command.Revert(model);
        Assert.Equal(["", ""], model.Rules.Select(r => r.Reason));
    }

    [Fact]
    public void DismissRuleCommand_ShouldBringBackOnlyTheStoredDismissal_OnUndo()
    {
        using var model = modelMgr.UseModel();
        model.AddDismissedRule(new DismissedRule("A", "B", "Kept reason."));
        var command = new DismissRuleCommand([], [new DismissedRule("A", "B"), new DismissedRule("C", "D")]);

        command.Execute(model);
        Assert.Empty(model.DismissedRules);

        command.Revert(model);
        var restored = Assert.Single(model.DismissedRules);
        Assert.Equal("Kept reason.", restored.Reason);
    }

    [Fact]
    public async Task FixLater_ShouldListTheDependencyUnderToFix_StillDrawnButNotAViolation()
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
            model.AddAllowedViolation(new AllowedViolation(ui.Name, data.Name, "UI.Gone", repo.Name, "Done?", true));
        }
        var commands = new CommandService(Mock.Of<IApplicationEvents>(), modelMgr);
        var service = new RuleService(modelMgr, commands);
        var report = Assert.Single(service.GetReports());

        service.FixLater(report.Rule, Assert.Single(report.Violations), " Use an interface ");

        report = Assert.Single(service.GetReports());
        Assert.Empty(report.Violations);
        Assert.Empty(report.Allowed);
        Assert.Equal(0, service.ViolationCount);
        Assert.NotEmpty(service.GetViolatingLineIds());
        var toFix = report.ToFix.Single(t => t.Planned.SourceName == view.Name);
        Assert.Equal("Use an interface", toFix.Planned.Reason);
        Assert.True(toFix.Planned.IsToFix);
        Assert.Equal(view.Id, toFix.Violation!.SourceId);
        Assert.True(report.ToFix.Single(t => t.Planned.SourceName == "UI.Gone").IsGone);

        await commands.Undo();
        Assert.Single(Assert.Single(service.GetReports()).Violations);
    }

    [Fact]
    public void Model_ShouldRoundTripADependencyToFixThroughTheDto()
    {
        using var model = modelMgr.UseModel();
        var toFix = new AllowedViolation("A", "B", "A.X", "B.Y", "Plan", IsToFix: true);
        model.AddAllowedViolation(toFix);
        var dto = model.SerializeToDto();

        model.Clear();
        model.SetFromDto("path", dto);

        Assert.Equal([toFix], model.AllowedViolations);
    }

    [Fact]
    public void Compute_OnlyMayUse_ShouldReportUsesFromOutsideBothNodes()
    {
        using var model = modelMgr.UseModel();
        var data = AddNode(model, "Data", model.Root, NodeType.Assembly);
        var db = AddNode(model, "Db", model.Root, NodeType.Assembly);
        var ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
        var repo = AddNode(model, "Data.Repo", data);
        var client = AddNode(model, "Db.Client", db);
        var pool = AddNode(model, "Db.Pool", db);
        var view = AddNode(model, "UI.View", ui);
        AddLink(model, repo, client); // Data may use Db
        AddLink(model, client, pool); // Inside Db is fine
        AddLink(model, view, client); // UI may not
        AddLink(model, view, repo); // Not about Db
        model.AddRule(new ArchitectureRule(data.Name, db.Name, Kind: RuleKind.OnlyMayUse));

        var (reports, lineIds) = RuleService.Compute(model);

        var report = Assert.Single(reports);
        var violation = Assert.Single(report.Violations);
        Assert.Equal((view.Id, client.Id), (violation.SourceId, violation.TargetId));
        Assert.Equal("only Data (dll) may use Db (dll)", report.Text);
        Assert.Contains(LineId.From(ui.Name, db.Name), lineIds);
        Assert.DoesNotContain(LineId.From(data.Name, db.Name), lineIds);
    }

    [Fact]
    public void Compute_Layers_ShouldReportUsesOfALayerAbove_AndListTheLayers()
    {
        using var model = modelMgr.UseModel();
        var ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
        var domain = AddNode(model, "Domain", model.Root, NodeType.Assembly);
        var data = AddNode(model, "Data", model.Root, NodeType.Assembly);
        var tools = AddNode(model, "Tools", model.Root, NodeType.Assembly);
        var view = AddNode(model, "UI.View", ui);
        var order = AddNode(model, "Domain.Order", domain);
        var repo = AddNode(model, "Data.Repo", data);
        var log = AddNode(model, "Tools.Log", tools);
        AddLink(model, view, repo); // Down two layers is fine
        AddLink(model, order, repo); // Down one is fine
        AddLink(model, repo, order); // Up is not
        AddLink(model, repo, view); // Neither
        AddLink(model, log, view); // Tools is no layer
        model.AddRule(ArchitectureRule.ForLayers([ui.Name, domain.Name, data.Name]));

        var (reports, _) = RuleService.Compute(model);

        var report = Assert.Single(reports);
        Assert.True(report.IsResolved);
        Assert.Equal(
            [(repo.Id, order.Id), (repo.Id, view.Id)],
            report.Violations.Select(v => (v.SourceId, v.TargetId)).ToList()
        );
        Assert.Equal(["UI", "Domain", "Data"], report.Nodes.Select(n => n.Name));
        Assert.Equal((ui.Id, data.Id), (report.FromId, report.ToId));
    }

    [Fact]
    public void Compute_Layers_ShouldBeUnresolved_WhenAMiddleLayerIsGone()
    {
        using var model = modelMgr.UseModel();
        var ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
        var data = AddNode(model, "Data", model.Root, NodeType.Assembly);
        AddLink(model, AddNode(model, "Data.Repo", data), AddNode(model, "UI.View", ui));
        model.AddRule(ArchitectureRule.ForLayers([ui.Name, "Gone", data.Name]));

        var (reports, lineIds) = RuleService.Compute(model);

        var report = Assert.Single(reports);
        Assert.False(report.IsResolved);
        Assert.Empty(report.Violations);
        Assert.Empty(lineIds);
    }

    [Fact]
    public void AddRule_ShouldAddLayersAndOnlyMayUse_AndRejectNestedOrTooFewNodes()
    {
        Node ui,
            view,
            domain,
            data;
        using (var model = modelMgr.UseModel())
        {
            ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
            view = AddNode(model, "UI.View", ui);
            domain = AddNode(model, "Domain", model.Root, NodeType.Assembly);
            data = AddNode(model, "Data", model.Root, NodeType.Assembly);
        }
        var commands = new Mock<ICommandService>();
        commands
            .Setup(c => c.Do(It.IsAny<Command>(), true, true))
            .Callback<Command, bool, bool>((command, _, _) => modelMgr.WithModel(m => command.Execute(m)));
        var service = new RuleService(modelMgr, commands.Object);

        AssertOk(service.AddRule(RuleKind.Layers, [ui.Id, domain.Id, data.Id]));
        AssertError(service.AddRule(RuleKind.Layers, [ui.Id, domain.Id, data.Id])); // Exists
        AssertError(service.AddRule(RuleKind.Layers, [ui.Id])); // Too few
        AssertError(service.AddRule(RuleKind.Layers, [ui.Id, view.Id, data.Id])); // View is inside UI
        AssertError(service.AddRule(RuleKind.Layers, [ui.Id, data.Id, ui.Id])); // UI twice
        AssertOk(service.AddRule(RuleKind.OnlyMayUse, [domain.Id, data.Id]));
        AssertError(service.AddRule(RuleKind.OnlyMayUse, [ui.Id, view.Id]));
        AssertOk(service.AddRule(ui.Id, data.Id)); // Must not use is a different rule on the same nodes

        Assert.Equal(
            [
                ArchitectureRule.ForLayers([ui.Name, domain.Name, data.Name]),
                new ArchitectureRule(domain.Name, data.Name, Kind: RuleKind.OnlyMayUse),
                new ArchitectureRule(ui.Name, data.Name),
            ],
            modelMgr.WithModel(m => m.Rules)
        );
    }

    [Fact]
    public void ArchitectureRule_ShouldDifferByKindAndLayers()
    {
        Assert.NotEqual(new ArchitectureRule("A", "C"), new ArchitectureRule("A", "C", Kind: RuleKind.OnlyMayUse));
        Assert.NotEqual(ArchitectureRule.ForLayers(["A", "B", "C"]), ArchitectureRule.ForLayers(["A", "C"]));
        Assert.Equal(ArchitectureRule.ForLayers(["A", "B", "C"], "why"), ArchitectureRule.ForLayers(["A", "B", "C"]));
    }

    [Fact]
    public void Model_ShouldRoundTripRuleKindsThroughTheDto_InListsOfTheirOwn_AndSkipAnUnknownKind()
    {
        using var model = modelMgr.UseModel();
        var layers = ArchitectureRule.ForLayers(["A", "B", "C"], "Clean");
        var only = new ArchitectureRule("A", "B", Kind: RuleKind.OnlyMayUse);
        var allowed = new AllowedViolation(
            "A",
            "C",
            "C.X",
            "A.Y",
            "Ok",
            RuleKind: RuleKind.Layers,
            LayersKey: layers.LayersKey
        );
        model.AddRule(layers);
        model.AddRule(only);
        model.AddAllowedViolation(allowed);
        var dto = model.SerializeToDto();

        // A version that knows only "must not use" sees none of them.
        Assert.Empty(dto.Rules);
        Assert.Empty(dto.AllowedViolations);
        Assert.Equal(2, dto.KindRules.Count);

        model.Clear();
        model.SetFromDto("path", dto);
        Assert.Equal([layers, only], model.Rules);
        Assert.Equal(["A", "B", "C"], model.Rules[0].NodeNames);
        Assert.Equal([allowed], model.AllowedViolations);
        Assert.True(model.AllowedViolations[0].IsFor(layers));
        Assert.False(model.AllowedViolations[0].IsFor(ArchitectureRule.ForLayers(["A", "D", "C"])));

        model.Clear();
        model.SetFromDto(
            "path",
            dto with
            {
                KindRules =
                [
                    dto.KindRules[0] with
                    {
                        Kind = 99,
                    },
                    dto.KindRules[1] with
                    {
                        Layers = null,
                        Kind = (int)RuleKind.Layers,
                    },
                ],
            }
        );
        Assert.Equal([ArchitectureRule.ForLayers(["A", "B"])], model.Rules);
    }

    [Fact]
    public void Compute_Independent_ShouldReportUsesOfTheSolutionOutsideIt_ButNotOfExternals()
    {
        using var model = modelMgr.UseModel();
        var shared = AddNode(model, "Shared", model.Root, NodeType.Assembly);
        var core = AddNode(model, "Core", model.Root, NodeType.Assembly);
        var externals = AddNode(model, "$Externals", model.Root, NodeType.Externals);
        var dto = AddNode(model, "Shared.Dto", shared);
        var util = AddNode(model, "Shared.Util", shared);
        var service = AddNode(model, "Core.Service", core);
        var json = AddNode(model, "$Externals.Json", externals);
        AddLink(model, dto, util); // Inside is fine
        AddLink(model, dto, json); // Externals are fine
        AddLink(model, dto, service); // The rest of the solution is not
        AddLink(model, service, dto); // Others may use it
        model.AddRule(ArchitectureRule.ForNode(RuleKind.Independent, shared.Name));

        var (reports, _) = RuleService.Compute(model);

        var report = Assert.Single(reports);
        Assert.True(report.IsResolved);
        var violation = Assert.Single(report.Violations);
        Assert.Equal((dto.Id, service.Id), (violation.SourceId, violation.TargetId));
        Assert.Equal(["Shared"], report.Nodes.Select(n => n.Name));
    }

    [Fact]
    public void Compute_NoCycles_ShouldReportTheLinksOfCyclesInsideTheNode_ButNotAcceptedOnes()
    {
        using var model = modelMgr.UseModel();
        var core = AddNode(model, "Core", model.Root, NodeType.Assembly);
        var ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
        var a = AddNode(model, "Core.A", core);
        var b = AddNode(model, "Core.B", core);
        var c = AddNode(model, "Core.C", core);
        var x = AddNode(model, "UI.X", ui);
        var y = AddNode(model, "UI.Y", ui);
        AddLink(model, a, b);
        AddLink(model, b, a); // A cycle inside Core
        AddLink(model, a, c); // Not part of it
        AddLink(model, x, y);
        AddLink(model, y, x); // A cycle outside Core
        AddLink(model, c, x);
        AddLink(model, x, c); // Core and UI in a cycle: around Core, not inside it
        var rule = ArchitectureRule.ForNode(RuleKind.NoCycles, core.Name);
        model.AddRule(rule);

        var (reports, _) = RuleService.Compute(model);

        Assert.Equal(
            [(a.Id, b.Id), (b.Id, a.Id)],
            Assert.Single(reports).Violations.Select(v => (v.SourceId, v.TargetId)).ToList()
        );
        Assert.Equal("no cycles inside Core (dll)", reports[0].Text);

        // A cycle accepted in the Cycles panel is a decision both share.
        model.AddAcceptedCycle(new AcceptedCycle(core.Name, [a.Name, b.Name], "Intended"));
        Assert.Empty(Assert.Single(RuleService.Compute(model).Reports).Violations);
    }

    [Fact]
    public void AddRule_ShouldAddOneNodeKinds_AndRoundTripThemThroughTheDto()
    {
        Node core;
        using (var model = modelMgr.UseModel())
            core = AddNode(model, "Core", model.Root, NodeType.Assembly);
        var commands = new Mock<ICommandService>();
        commands
            .Setup(c => c.Do(It.IsAny<Command>(), true, true))
            .Callback<Command, bool, bool>((command, _, _) => modelMgr.WithModel(m => command.Execute(m)));
        var service = new RuleService(modelMgr, commands.Object);

        AssertOk(service.AddRule(RuleKind.Independent, [core.Id]));
        AssertOk(service.AddRule(RuleKind.NoCycles, [core.Id]));
        AssertError(service.AddRule(RuleKind.NoCycles, [core.Id])); // Exists
        AssertError(service.AddRule(RuleKind.Independent, [core.Id, core.Id])); // One node only

        using var m = modelMgr.UseModel();
        var rules = m.Rules.ToList();
        var dto = m.SerializeToDto();
        m.Clear();
        m.SetFromDto("path", dto);
        Assert.Equal(
            [
                ArchitectureRule.ForNode(RuleKind.Independent, core.Name),
                ArchitectureRule.ForNode(RuleKind.NoCycles, core.Name),
            ],
            m.Rules
        );
        Assert.Equal(rules, m.Rules);
    }
}
