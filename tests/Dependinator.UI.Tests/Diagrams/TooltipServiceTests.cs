using System.Net;
using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Cycles;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Rules;
using Dependinator.UI.Diagrams.Tooltips;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// The hover card of a node or line, built on demand from the model: a node's kind and container,
// description, what it uses and is used by outside itself (a member counting as its type), its
// members and its problems; a line's ends, link count, first links and whether it is in a cycle.
public class TooltipServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());

    TooltipService NewService()
    {
        var commands = Mock.Of<ICommandService>();
        var cycles = new CycleService(modelMgr, commands);
        var rules = new RuleService(modelMgr, commands);
        return new TooltipService(modelMgr, cycles, rules);
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

    static string NodeElement(Node node) => PointerId.FromNode(node.Id).ElementId;

    // The card's text as the browser shows it (HtmlEncode writes "·" as an entity).
    static string? Html(TooltipService service, string elementId) =>
        service.GetHtml(elementId) is string html ? WebUtility.HtmlDecode(html) : null;

    [Fact]
    public void GetHtml_ShouldDescribeANode_WithItsDependenciesCountedPerType()
    {
        Node service;
        using (var model = modelMgr.UseModel())
        {
            var ns = AddNode(model, "App", model.Root, NodeType.Namespace);
            service = AddNode(model, "App.Service", ns);
            service.SetDescription("Runs <the> app.");
            var run = AddNode(model, "App.Service.Run", service, NodeType.MethodMember);
            var stop = AddNode(model, "App.Service.Stop", service, NodeType.MethodMember);
            var repo = AddNode(model, "App.Repo", ns);
            var save = AddNode(model, "App.Repo.Save", repo, NodeType.MethodMember);
            var load = AddNode(model, "App.Repo.Load", repo, NodeType.MethodMember);
            var main = AddNode(model, "App.Main", ns);
            AddLink(model, run, save);
            AddLink(model, stop, load); // The same type again: still one use
            AddLink(model, run, stop); // Inside the service: not a use
            AddLink(model, main, run);
        }

        var html = Html(NewService(), NodeElement(service));

        Assert.NotNull(html);
        Assert.Contains(">Service</div>", html);
        Assert.Contains("Class · App", html);
        Assert.Contains("Runs <the> app.", html);
        Assert.DoesNotContain("<the>", NewService().GetHtml(NodeElement(service))); // Encoded
        Assert.Contains("uses <b>1</b>", html);
        Assert.Contains("used by <b>1</b>", html);
        Assert.Contains("<b>2</b> members", html);
        Assert.DoesNotContain("cycle", html);
    }

    [Fact]
    public void GetHtml_ShouldNotCountAMembersOwnType_AsAUse()
    {
        Node run;
        using (var model = modelMgr.UseModel())
        {
            var ns = AddNode(model, "App", model.Root, NodeType.Namespace);
            var service = AddNode(model, "App.Service", ns);
            run = AddNode(model, "App.Service.Run", service, NodeType.MethodMember);
            var stop = AddNode(model, "App.Service.Stop", service, NodeType.MethodMember);
            var repo = AddNode(model, "App.Repo", ns);
            AddLink(model, run, stop);
            AddLink(model, stop, run);
            AddLink(model, run, AddNode(model, "App.Repo.Save", repo, NodeType.MethodMember));
        }

        var html = Html(NewService(), NodeElement(run));

        Assert.Contains("Method · Service", html);
        Assert.Contains("uses <b>1</b>", html); // Repo, not Service
        Assert.DoesNotContain("used by", html);
    }

    [Fact]
    public void GetHtml_ShouldShowCycles_OnTheMembersAndTheirContainer()
    {
        Node container,
            a;
        using (var model = modelMgr.UseModel())
        {
            container = AddNode(model, "C", model.Root, NodeType.Assembly);
            a = AddNode(model, "C.A", container);
            var b = AddNode(model, "C.B", container);
            AddLink(model, a, b);
            AddLink(model, b, a);
        }
        var service = NewService();

        Assert.Contains("in 1 cycle", Html(service, NodeElement(a)));
        var containerHtml = Html(service, NodeElement(container));
        Assert.Contains("1 cycle inside", containerHtml);
        Assert.Contains("contains <b>2</b>", containerHtml);
    }

    [Fact]
    public void GetHtml_ShouldDescribeALine_WithItsFirstLinksAndCycle()
    {
        Line line;
        using (var model = modelMgr.UseModel())
        {
            var ns = AddNode(model, "N", model.Root, NodeType.Namespace);
            var a = AddNode(model, "N.A", ns);
            var b = AddNode(model, "N.B", ns);
            foreach (var name in new[] { "Delta", "Alpha", "Charlie", "Bravo", "Echo" })
                AddLink(model, AddNode(model, $"N.A.{name}", a, NodeType.MethodMember), b);
            AddLink(model, b, a);
            line = model.Lines.Values.Single(l => l.Source == a && l.Target == b);
        }

        var html = Html(NewService(), PointerId.FromLine(line.Id).ElementId);

        Assert.NotNull(html);
        Assert.Contains("A<span class=\"dep-tt-arrow\">→</span>B", html);
        Assert.Contains("N · 5 links", html);
        Assert.Contains("<code>A.Alpha</code>", html);
        Assert.Contains("<code>A.Charlie</code>", html);
        Assert.DoesNotContain("A.Delta", html); // Sorted, three shown
        Assert.Contains("+ 2 more", html);
        Assert.Contains("part of a cycle", html);
    }

    [Fact]
    public void GetHtml_ShouldBeNull_ForAnUnknownOrOtherElement()
    {
        var service = NewService();

        Assert.Null(service.GetHtml("missing.n"));
        Assert.Null(service.GetHtml("missing.l"));
        Assert.Null(service.GetHtml("svgcanvas"));
    }
}
