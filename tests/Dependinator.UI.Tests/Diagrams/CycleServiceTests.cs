using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// Cycle detection: per container, children that depend on each other in a circle form a cycle,
// links deep inside the children count for their top-level siblings, and one-way chains do not.
// An accepted cycle is reported apart and not highlighted until a node joins it; an acceptance
// whose cycle is gone is kept and marked.
public class CycleServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly List<Command> done = [];

    // A service whose commands run against the model and are kept, so a test can revert them.
    CycleService NewService()
    {
        var commands = new Mock<ICommandService>();
        commands
            .Setup(c => c.Do(It.IsAny<Command>(), true, true))
            .Callback<Command, bool, bool>(
                (command, _, _) =>
                {
                    modelMgr.WithModel(m => command.Execute(m));
                    done.Add(command);
                }
            );
        return new CycleService(modelMgr, commands.Object);
    }

    static Node AddNode(IModel model, string name, Node parent)
    {
        var node = new Node(name, parent) { Boundary = new Rect(0, 0, 100, 100) };
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

    // A container C with children A and B that use each other, and D used by B.
    (Node Container, Node A, Node B, Node D) AddCycle()
    {
        using var model = modelMgr.UseModel();
        var container = AddNode(model, "C", model.Root);
        var a = AddNode(model, "C.A", container);
        var b = AddNode(model, "C.B", container);
        var d = AddNode(model, "C.D", container);
        AddLink(model, a, b);
        AddLink(model, b, a);
        AddLink(model, b, d);
        return (container, a, b, d);
    }

    [Fact]
    public void GetCycles_ShouldFindMutuallyDependentSiblings_AndTheirLines()
    {
        Node a,
            b,
            d,
            container;
        using (var model = modelMgr.UseModel())
        {
            container = AddNode(model, "C", model.Root);
            a = AddNode(model, "C.A", container);
            b = AddNode(model, "C.B", container);
            d = AddNode(model, "C.D", container);
            AddLink(model, a, b);
            AddLink(model, b, a);
            AddLink(model, b, d); // D is a leaf of the graph: not part of the cycle
        }

        var service = NewService();
        var cycles = service.GetCycles();

        var cycle = Assert.Single(cycles);
        Assert.Equal(container.Id, cycle.ContainerId);
        Assert.Equal(["A", "B"], cycle.Members.Select(m => m.Name));

        var lineIds = service.GetCyclicLineIds();
        Assert.Contains(LineId.From(a.Name, b.Name), lineIds);
        Assert.Contains(LineId.From(b.Name, a.Name), lineIds);
        Assert.DoesNotContain(LineId.From(b.Name, d.Name), lineIds);
    }

    // The panel picks one cycle by its key: its lines stay highlighted, the others are dimmed.
    [Fact]
    public void GetCycleLineIds_ShouldReturnTheLinesOfThatCycleOnly()
    {
        Node a,
            b,
            x,
            y;
        using (var model = modelMgr.UseModel())
        {
            var first = AddNode(model, "C", model.Root);
            a = AddNode(model, "C.A", first);
            b = AddNode(model, "C.B", first);
            var second = AddNode(model, "E", model.Root);
            x = AddNode(model, "E.X", second);
            y = AddNode(model, "E.Y", second);
            AddLink(model, a, b);
            AddLink(model, b, a);
            AddLink(model, x, y);
            AddLink(model, y, x);
        }

        var service = NewService();
        var cycle = service.GetCycles().Single(c => c.ContainerNodeName == "C");

        var lineIds = service.GetCycleLineIds(cycle.Key);
        Assert.NotNull(lineIds);
        Assert.Contains(LineId.From(a.Name, b.Name), lineIds);
        Assert.Contains(LineId.From(b.Name, a.Name), lineIds);
        Assert.DoesNotContain(LineId.From(x.Name, y.Name), lineIds);
        Assert.Contains(LineId.From(x.Name, y.Name), service.GetCyclicLineIds());
        Assert.Null(service.GetCycleLineIds("no such cycle"));
    }

    [Fact]
    public void GetCycles_ShouldCountLinksDeepInsideChildren_ForTheirTopLevelSiblings()
    {
        using (var model = modelMgr.UseModel())
        {
            var a = AddNode(model, "A", model.Root);
            var b = AddNode(model, "B", model.Root);
            var aInner = AddNode(model, "A.X", a);
            var bInner = AddNode(model, "B.Y", b);
            AddLink(model, aInner, bInner);
            AddLink(model, bInner, aInner);
        }

        var cycle = Assert.Single(NewService().GetCycles());
        Assert.Equal(["A", "B"], cycle.Members.Select(m => m.Name));
    }

    [Fact]
    public void GetCycles_ShouldIgnoreOneWayChains_AndParentChildLinks()
    {
        using (var model = modelMgr.UseModel())
        {
            var a = AddNode(model, "A", model.Root);
            var b = AddNode(model, "B", model.Root);
            var c = AddNode(model, "C", model.Root);
            var aInner = AddNode(model, "A.X", a);
            AddLink(model, a, b);
            AddLink(model, b, c);
            AddLink(model, aInner, a); // Inside → its own container: no sibling pair
        }

        Assert.Empty(NewService().GetCycles());
    }

    [Fact]
    public void GetCycles_ShouldShowTheDisplayName_AlsoAfterAnAliasEdit()
    {
        Node a;
        using (var model = modelMgr.UseModel())
        {
            var container = AddNode(model, "C", model.Root);
            a = AddNode(model, "C.A", container);
            var b = AddNode(model, "C.B", container);
            AddLink(model, a, b);
            AddLink(model, b, a);
        }
        var service = NewService();
        Assert.Equal(["A", "B"], Assert.Single(service.GetCycles()).Members.Select(m => m.Name));

        // The alias edit bumps the structure version, so the cached rows are rebuilt.
        modelMgr.WithModel(m =>
            new Dependinator.UI.Modeling.Commands.NodeEditCommand(a.Id) { Alias = "Alpha" }.Execute(m)
        );

        Assert.Equal(["Alpha", "B"], Assert.Single(service.GetCycles()).Members.Select(m => m.Name));
    }

    [Fact]
    public void Accept_ShouldMoveTheCycleToAccepted_AndStopHighlightingItsLines()
    {
        var (container, a, b, _) = AddCycle();
        var service = NewService();

        service.Accept(Assert.Single(service.GetCycles()), "  Parent and child know each other  ");

        Assert.Empty(service.GetCycles());
        Assert.Empty(service.GetCyclicLineIds());
        var report = Assert.Single(service.GetAcceptedCycles());
        Assert.True(report.StillOccurs);
        Assert.Equal(container.Id, report.ContainerId);
        Assert.Equal(["A", "B"], report.MemberNames);
        Assert.Equal("Parent and child know each other", report.Accepted.Reason);
        Assert.Equal(
            new AcceptedCycle(container.Name, [b.Name, a.Name], "Parent and child know each other"),
            report.Accepted
        );

        // Undo brings the cycle and its red lines back.
        modelMgr.WithModel(m => Assert.Single(done).Revert(m));
        Assert.Single(service.GetCycles());
        Assert.Contains(LineId.From(a.Name, b.Name), service.GetCyclicLineIds());
        Assert.Empty(service.GetAcceptedCycles());
    }

    [Fact]
    public void GetCycles_ShouldReportAnAcceptedCycleAgain_WhenANodeJoinsIt()
    {
        var (container, _, _, d) = AddCycle();
        var service = NewService();
        service.Accept(Assert.Single(service.GetCycles()), "Intended");

        modelMgr.WithModel(m => AddLink(m, d, m.Nodes[NodeId.FromName("C.B")])); // D now uses B back

        var cycle = Assert.Single(service.GetCycles());
        Assert.Equal(["A", "B", "D"], cycle.Members.Select(m => m.Name));
        Assert.Equal("Intended", cycle.AcceptedBefore?.Reason);
        var outgrown = Assert.Single(service.GetAcceptedCycles());
        Assert.False(outgrown.StillOccurs);
        Assert.False(outgrown.IsGone);
        Assert.Equal(["D"], outgrown.Joined);

        // Accepting the grown cycle replaces the earlier acceptance it covers.
        service.Accept(cycle, "Still intended");
        var report = Assert.Single(service.GetAcceptedCycles());
        Assert.Equal(["A", "B", "D"], report.MemberNames);
        Assert.Equal("Still intended", report.Accepted.Reason);
        Assert.Empty(service.GetCycles());
        Assert.Equal(container.Name, report.Accepted.ContainerName);
    }

    [Fact]
    public void GetCycles_ShouldKeepACycleThatShrank_Accepted()
    {
        Node a,
            b,
            d;
        Link dToA;
        using (var model = modelMgr.UseModel())
        {
            var container = AddNode(model, "C", model.Root);
            a = AddNode(model, "C.A", container);
            b = AddNode(model, "C.B", container);
            d = AddNode(model, "C.D", container);
            AddLink(model, a, b);
            AddLink(model, b, a);
            AddLink(model, b, d);
            dToA = AddLink(model, d, a);
        }
        var service = NewService();
        service.Accept(Assert.Single(service.GetCycles()), "A, B and D belong together");

        modelMgr.WithModel(m => m.RemoveLink(dToA)); // Only A ↔ B is left

        Assert.Empty(service.GetCycles());
        Assert.True(Assert.Single(service.GetAcceptedCycles()).StillOccurs);
    }

    [Fact]
    public void GetAcceptedCycles_ShouldKeepAnAcceptanceWhoseCycleIsGone_WithItsStoredNames()
    {
        Link bToA;
        using (var model = modelMgr.UseModel())
        {
            var container = AddNode(model, "C", model.Root);
            var a = AddNode(model, "C.A", container);
            var b = AddNode(model, "C.B", container);
            AddLink(model, a, b);
            bToA = AddLink(model, b, a);
            model.AddAcceptedCycle(new AcceptedCycle("Gone", ["Gone.X", "Gone.Y"], ""));
        }
        var service = NewService();
        service.Accept(Assert.Single(service.GetCycles()), "Intended");

        modelMgr.WithModel(m => m.RemoveLink(bToA)); // The refactor broke the cycle

        Assert.Empty(service.GetCycles());
        var reports = service.GetAcceptedCycles();
        Assert.Equal(2, reports.Count);
        Assert.All(reports, r => Assert.False(r.StillOccurs));
        var gone = reports.Single(r => r.Accepted.ContainerName == "Gone");
        Assert.Null(gone.ContainerId);
        Assert.Equal("Gone", gone.ContainerName);
        Assert.Equal(["X", "Y"], gone.MemberNames);

        // Removing them is up to the user.
        service.Unaccept([.. reports.Select(r => r.Accepted)]);
        Assert.Empty(service.GetAcceptedCycles());
    }

    [Fact]
    public void SetReason_ShouldReplaceTheReason_AsOneUndoableStep()
    {
        AddCycle();
        var service = NewService();
        service.Accept(Assert.Single(service.GetCycles()), "First");
        var accepted = Assert.Single(service.GetAcceptedCycles()).Accepted;

        service.SetReason(accepted, "Second");
        service.SetReason(Assert.Single(service.GetAcceptedCycles()).Accepted, " Second "); // Same: no step

        Assert.Equal("Second", Assert.Single(service.GetAcceptedCycles()).Accepted.Reason);
        Assert.Equal(2, done.Count);
        modelMgr.WithModel(m => done[^1].Revert(m));
        Assert.Equal("First", Assert.Single(service.GetAcceptedCycles()).Accepted.Reason);
    }

    [Fact]
    public void FixLater_ShouldListTheCycleToFix_AndKeepHighlightingItsLines()
    {
        var (_, a, b, _) = AddCycle();
        var service = NewService();

        service.FixLater(Assert.Single(service.GetCycles()), " Move B's use of A into A ");

        Assert.Empty(service.GetCycles());
        Assert.Empty(service.GetAcceptedCycles());
        var report = Assert.Single(service.GetCyclesToFix());
        Assert.True(report.StillOccurs);
        Assert.True(report.Accepted.IsToFix);
        Assert.Equal("Move B's use of A into A", report.Accepted.Reason);
        Assert.Equal(["A", "B"], report.Cycle?.Members.Select(m => m.Name));
        Assert.Contains(LineId.From(a.Name, b.Name), service.GetCyclicLineIds());

        // Undo reports it as an open cycle again.
        modelMgr.WithModel(m => Assert.Single(done).Revert(m));
        Assert.Single(service.GetCycles());
        Assert.Empty(service.GetCyclesToFix());
    }

    [Fact]
    public void GetCyclesToFix_ShouldKeepOneWhoseCycleIsGone_ForTheUserToRemove()
    {
        Link bToA;
        using (var model = modelMgr.UseModel())
        {
            var container = AddNode(model, "C", model.Root);
            var a = AddNode(model, "C.A", container);
            var b = AddNode(model, "C.B", container);
            AddLink(model, a, b);
            bToA = AddLink(model, b, a);
        }
        var service = NewService();
        service.FixLater(Assert.Single(service.GetCycles()), "Remove B's use of A");

        modelMgr.WithModel(m => m.RemoveLink(bToA)); // Fixed

        var report = Assert.Single(service.GetCyclesToFix());
        Assert.False(report.StillOccurs);
        Assert.Null(report.Cycle);
        Assert.Empty(service.GetCyclicLineIds());
        service.Unaccept([report.Accepted]);
        Assert.Empty(service.GetCyclesToFix());
    }

    [Fact]
    public void GetCyclesToFix_ShouldSayTheCycleChanged_WhenANodeJoinsIt_NotThatItIsFixed()
    {
        var (_, _, _, d) = AddCycle();
        var service = NewService();
        service.FixLater(Assert.Single(service.GetCycles()), "Move A's use of B");

        modelMgr.WithModel(m => AddLink(m, d, m.Nodes[NodeId.FromName("C.B")])); // D now uses B back

        var report = Assert.Single(service.GetCyclesToFix());
        Assert.True(report.HasChanged);
        Assert.False(report.IsGone);
        Assert.Equal(["D"], report.Joined);
        var cycle = Assert.Single(service.GetCycles());
        Assert.True(cycle.AcceptedBefore?.IsToFix);
    }

    [Fact]
    public void GetAcceptedCycles_ShouldListTheOnesNoLongerOccurringFirst()
    {
        using (var model = modelMgr.UseModel())
        {
            var container = AddNode(model, "A", model.Root);
            var a = AddNode(model, "A.X", container);
            var b = AddNode(model, "A.Y", container);
            AddLink(model, a, b);
            AddLink(model, b, a);
            model.AddAcceptedCycle(new AcceptedCycle("Z", ["Z.X", "Z.Y"], "Gone"));
        }
        var service = NewService();
        service.Accept(Assert.Single(service.GetCycles()), "Occurs");

        Assert.Equal(["Gone", "Occurs"], service.GetAcceptedCycles().Select(r => r.Accepted.Reason));
    }

    [Fact]
    public void GetCycles_ShouldLabelATopLevelCycle()
    {
        using (var model = modelMgr.UseModel())
        {
            var a = AddNode(model, "A", model.Root);
            var b = AddNode(model, "B", model.Root);
            AddLink(model, a, b);
            AddLink(model, b, a);
        }
        var service = NewService();

        var cycle = Assert.Single(service.GetCycles());
        Assert.Equal("Top level", cycle.ContainerName);
        service.Accept(cycle, "");
        Assert.Equal("Top level", Assert.Single(service.GetAcceptedCycles()).ContainerName);
    }

    [Fact]
    public void Model_ShouldRoundTripAcceptedCyclesThroughTheDto_AndDropThemOnClear()
    {
        using var model = modelMgr.UseModel();
        model.AddAcceptedCycle(new AcceptedCycle("C", ["C.B", "C.A"], "Why"));
        var dto = model.SerializeToDto();

        var acceptedDto = Assert.Single(dto.AcceptedCycles);
        Assert.Equal(("C", "Why"), (acceptedDto.Container, acceptedDto.Reason));
        Assert.Equal(["C.A", "C.B"], acceptedDto.Members);

        model.Clear();
        Assert.Empty(model.AcceptedCycles);
        model.SetFromDto("path", dto);
        Assert.Equal([new AcceptedCycle("C", ["C.A", "C.B"], "Why")], model.AcceptedCycles);

        // One to fix later keeps that, and its planned change.
        model.AddAcceptedCycle(new AcceptedCycle("D", ["D.A", "D.B"], "Move it", IsToFix: true));
        var toFixDto = model.SerializeToDto().AcceptedCycles.Single(a => a.Container == "D");
        Assert.True(toFixDto.IsToFix);
        model.SetFromDto("path", model.SerializeToDto());
        Assert.Contains(new AcceptedCycle("D", ["D.A", "D.B"], "Move it", IsToFix: true), model.AcceptedCycles);
    }
}
