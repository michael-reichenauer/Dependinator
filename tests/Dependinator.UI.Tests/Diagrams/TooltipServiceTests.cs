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
        Assert.Contains("Class in App", html);
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

        Assert.Contains("Method in Service", html);
        Assert.Contains("uses <b>1</b>", html); // Repo, not Service
        Assert.DoesNotContain("used by", html);
    }

    // A member reads as it is declared in code, not as its row shows it (UML style).
    [Theory]
    [InlineData(
        nameof(NodeType.MethodMember),
        "(Purpose purpose): Task<Rect?>",
        "Task<Rect?> <b>Run</b>(Purpose purpose)"
    )]
    [InlineData(nameof(NodeType.MethodMember), "(int count)", "void <b>Run</b>(int count)")]
    [InlineData(
        nameof(NodeType.MethodMember),
        "((int X, int Y) pos, int[] ids): (int, int)",
        "(int, int) <b>Run</b>((int X, int Y) pos, int[] ids)"
    )]
    [InlineData(nameof(NodeType.ConstructorMember), "(int count)", "<b>Run</b>(int count)")]
    [InlineData(nameof(NodeType.PropertyMember), ": int", "int <b>Run</b>")]
    [InlineData(nameof(NodeType.PropertyMember), "[int index]: string", "string this[int index]")]
    [InlineData(nameof(NodeType.FieldMember), ": List<string>", "List<string> <b>Run</b>")]
    [InlineData(nameof(NodeType.EventMember), ": Action?", "event Action? <b>Run</b>")]
    [InlineData(nameof(NodeType.ParameterMember), ": string", "param <b>Run</b> string")]
    [InlineData(nameof(NodeType.OutputMember), ": string", "output <b>Run</b> string")]
    [InlineData(nameof(NodeType.MethodMember), "(int count", "(int count")]
    public void DeclarationHtml_ShouldReadLikeCode(string type, string signature, string expected)
    {
        Node member;
        using (var model = modelMgr.UseModel())
        {
            var service = AddNode(model, "App.Service", model.Root);
            member = AddNode(model, "App.Service.Run", service, Enum.Parse<NodeType>(type));
            member.Signature = signature;
        }

        // Without where it may wrap: after "(", not at the non-breaking spaces
        var html = TooltipService.DeclarationHtml(member).Replace("<wbr>", "").Replace("&nbsp;", " ");
        Assert.Equal(expected, WebUtility.HtmlDecode(html));
    }

    [Fact]
    public void DeclarationHtml_ShouldWrapOnlyBetweenParameters()
    {
        Node member;
        using (var model = modelMgr.UseModel())
        {
            var service = AddNode(model, "App.Service", model.Root);
            member = AddNode(model, "App.Service.Run", service, NodeType.MethodMember);
            member.Signature = "(Dictionary<string, int> map, (int X, int Y) pos): Task<int>";
        }

        Assert.Equal(
            "Task&lt;int&gt;&nbsp;<b>Run</b>(<wbr>Dictionary&lt;string,&nbsp;int&gt;&nbsp;map, (int&nbsp;X,&nbsp;int&nbsp;Y)&nbsp;pos)",
            TooltipService.DeclarationHtml(member)
        );
    }

    [Fact]
    public void DeclarationHtml_ShouldNameAnEnumMember_AndGiveAFinalizerNoReturnType()
    {
        Node value,
            finalizer;
        using (var model = modelMgr.UseModel())
        {
            var kind = AddNode(model, "App.Kind", model.Root, NodeType.EnumType);
            value = AddNode(model, "App.Kind.First", kind, NodeType.FieldMember);
            value.Signature = ": Kind";
            var service = AddNode(model, "App.Service", model.Root);
            finalizer = AddNode(model, "App.Service.~Service", service, NodeType.MethodMember);
            finalizer.Signature = "()";
        }

        Assert.Equal("<b>First</b>", TooltipService.DeclarationHtml(value));
        Assert.Equal("<b>~Service</b>()", TooltipService.DeclarationHtml(finalizer));
    }

    [Fact]
    public void Flow_ShouldKeepListItemsOnTheirOwnLines()
    {
        Assert.Equal(
            "Steps:\n- parse the\u0020files\n- build",
            TooltipService.Flow("Steps:\n- parse the\n  files\n- build")
        );
    }

    [Fact]
    public void GetHtml_ShouldFlowADescriptionsSourceLines_AndKeepItsParagraphs()
    {
        Node node;
        using (var model = modelMgr.UseModel())
        {
            node = AddNode(model, "App", model.Root, NodeType.Namespace);
            node.SetDescription("First line\n  second line.\n\nNext paragraph.");
        }

        Assert.Contains("First line second line.\n\nNext paragraph.", Html(NewService(), NodeElement(node)));
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
        Assert.Contains("A<span class=\"dep-tt-arrow\">→</span><wbr>B", html);
        Assert.Contains("5 links in N", html);
        Assert.Contains("<code>A.Alpha</code>", html);
        Assert.Contains("<code>A.Charlie</code>", html);
        Assert.DoesNotContain("A.Delta", html); // Sorted, three shown
        Assert.Contains("+ 2 more", html);
        Assert.Contains("part of a cycle", html);
    }

    [Fact]
    public void GetHtml_ShouldShowAZonesLabel_WithoutItsGeneratedName()
    {
        Node zone;
        using (var model = modelMgr.UseModel())
        {
            zone = new Node("Zone 2", model.Root) { Boundary = new Rect(0, 0, 100, 100), IsZone = true };
            model.Root.AddChild(zone);
            model.TryAddNode(zone);
            zone.SetAlias("Edge");
            zone.SetDescription("Front Door\nand API Management");
        }

        var html = Html(NewService(), NodeElement(zone));

        Assert.Contains(">Edge</div>", html);
        Assert.Contains(">Zone</div>", html);
        Assert.DoesNotContain("Zone 2", html);
        Assert.Contains("Front Door\nand API Management", html);
    }

    [Fact]
    public void GetHtml_ShouldCallAManualLine_DrawnByHand()
    {
        Line line;
        using (var model = modelMgr.UseModel())
        {
            var a = AddNode(model, "A", model.Root);
            var b = AddNode(model, "B", model.Root);
            var link = new Link(a, b) { IsManual = true };
            model.TryAddLink(link);
            a.AddSourceLink(link);
            b.AddTargetLink(link);
            new LineService().AddLinesFromSourceToTarget(model, link);
            line = model.Lines.Values.Single();
        }

        var html = Html(NewService(), PointerId.FromLine(line.Id).ElementId);

        Assert.Contains("drawn by hand", html);
        Assert.DoesNotContain("1 link", html);
    }

    [Fact]
    public void GetHtml_ShouldRecount_WhenTheModelChanges()
    {
        Node a,
            b;
        using (var model = modelMgr.UseModel())
        {
            a = AddNode(model, "A", model.Root);
            b = AddNode(model, "B", model.Root);
            AddLink(model, a, b);
        }
        var service = NewService();
        Assert.Contains("uses <b>1</b>", Html(service, NodeElement(a)));

        using (var model = modelMgr.UseModel())
            AddLink(model, a, AddNode(model, "C", model.Root));

        Assert.Contains("uses <b>2</b>", Html(service, NodeElement(a)));
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
