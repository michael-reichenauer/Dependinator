using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Diagrams.Navigation;
using Dependinator.UI.Diagrams.Tours;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// The codebase tour: the prompt outlines the model, an answer is read from structured output or a
// pasted chat answer, the stops that are no nodes are left out, and the tour plays stop by stop.
public class CodebaseTourTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly Mock<INavigationService> navigation = new();
    readonly TourService service;

    // UI (assembly) with UI.Views.View, Data (assembly) with Data.Repo; View uses Repo.
    public CodebaseTourTests()
    {
        service = new TourService(
            modelMgr,
            Mock.Of<IModelService>(),
            Mock.Of<IAiService>(),
            new ViewOptions(),
            Mock.Of<IPathFinderService>(),
            navigation.Object,
            Mock.Of<IApplicationEvents>()
        );

        using var model = modelMgr.UseModel();
        model.Path = "/demo/Demo.sln";
        var ui = AddNode(model, "UI", model.Root, NodeType.Assembly);
        var views = AddNode(model, "UI.Views", ui, NodeType.Namespace);
        var view = AddNode(model, "UI.Views.View", views);
        var data = AddNode(model, "Data", model.Root, NodeType.Assembly);
        var repo = AddNode(model, "Data.Repo", data);
        AddLink(model, view, repo);
    }

    [Fact]
    public void GetPrompt_ShouldOutlineTheModel_AndAskForStops()
    {
        var prompt = service.GetPrompt();

        Assert.Contains("# A guided tour of a codebase", prompt);
        Assert.Contains("- `UI` (project, 1 type)", prompt);
        Assert.Contains("- `UI.Views` → `Data`: 1", prompt);
        Assert.Contains("Stop: <name>", prompt);
    }

    [Fact]
    public void Parse_ShouldReadTheStops_FromStructuredOutput()
    {
        var json = """
            {"intro": "A small app.", "stops": [
              {"node": "`UI`", "title": "The UI", "text": "Where it starts."},
              {"node": " ", "title": "Nothing", "text": "No node."},
              {"node": "Data.Repo", "title": "", "text": "Storage."}
            ]}
            """;

        var tour = AssertOk(CodebaseTour.Parse(json, AiSource.ClaudeCode));

        Assert.Equal("A small app.", tour.Intro);
        Assert.Equal(AiSource.ClaudeCode, tour.Source);
        Assert.Equal(["UI", "Data.Repo"], tour.Stops.Select(s => s.Name));
        Assert.Equal("The UI", tour.Stops[0].Title);
    }

    [Fact]
    public void Parse_ShouldFail_WithoutStops()
    {
        AssertError(CodebaseTour.Parse("""{"intro": "x", "stops": []}"""));
        AssertError(CodebaseTour.Parse("not json"));
    }

    [Fact]
    public void ParseText_ShouldReadAChatAnswer()
    {
        var text = """
            Here is a tour.

            **Intro:** A small app
            with two parts.

            1. **Stop:** `UI.Views` – the views
            **Title:** The views
            **Text:** What the user sees.
            It uses `Data.Repo`.

            Stop: Data.Repo
            Title: Storage
            Text: Keeps the data.
            """;

        var tour = AssertOk(CodebaseTour.ParseText(text));

        Assert.Equal(AiSource.Chat, tour.Source);
        Assert.Equal("A small app with two parts.", tour.Intro);
        Assert.Equal(["UI.Views", "Data.Repo"], tour.Stops.Select(s => s.Name));
        Assert.Equal("The views", tour.Stops[0].Title);
        Assert.Equal("What the user sees. It uses `Data.Repo`.", tour.Stops[0].Text);
        Assert.Equal("Keeps the data.", tour.Stops[1].Text);
    }

    [Fact]
    public void ParseText_ShouldFail_WithoutStopLines()
    {
        AssertError(CodebaseTour.ParseText("I cannot see your code."));
    }

    [Fact]
    public void ParsePasted_ShouldLeaveOutStopsThatAreNoNodes_AndRepeatedNodes()
    {
        var text = """
            Stop: `UI`
            Title: The UI
            Text: Starts here.

            Stop: No.Such.Node
            Text: Made up.

            Stop: Data.Repo
            Text: Storage.

            Stop: UI
            Text: Again.
            """;

        var tour = AssertOk(service.ParsePasted(text));

        Assert.Equal(["UI", "Data.Repo"], tour.Stops.Select(s => s.Name));
        Assert.Equal(2, tour.LeftOutCount);
        Assert.Equal(NodeId.FromName("Data.Repo"), tour.Stops[1].Id);
        // A stop without a title gets the node's short name.
        Assert.Equal("Repo", tour.Stops[1].Title);
    }

    [Fact]
    public void ParsePasted_ShouldFail_WhenNoStopIsANode()
    {
        AssertError(service.ParsePasted("Stop: Nowhere\nText: Nothing here."));
    }

    [Fact]
    public async Task StartAsync_ShouldShowEachStop_WithNextAndBack()
    {
        var tour = AssertOk(service.ParsePasted("Stop: UI\nText: a\n\nStop: Data.Repo\nText: b"));
        var changes = 0;
        service.Changed += () => changes++;

        await service.StartAsync(tour);
        Assert.Same(tour, service.Playing);
        Assert.Equal(0, service.StopIndex);
        navigation.Verify(n => n.ShowNodeAsync(NodeId.FromName("UI")), Times.Once);

        await service.NextAsync();
        Assert.Equal(1, service.StopIndex);
        navigation.Verify(n => n.ShowNodeAsync(NodeId.FromName("Data.Repo")), Times.Once);

        await service.NextAsync(); // past the last stop: stays
        Assert.Equal(1, service.StopIndex);

        await service.BackAsync();
        Assert.Equal(0, service.StopIndex);

        service.End();
        Assert.Null(service.Playing);
        Assert.Equal(4, changes);
    }

    [Fact]
    public async Task StartAsync_ShouldEndTheTour_WhenAnotherModelIsOpen()
    {
        var tour = AssertOk(service.ParsePasted("Stop: UI\nText: a")) with { ModelPath = "/other/model.sln" };

        await service.StartAsync(tour);

        Assert.Null(service.Playing);
        navigation.Verify(n => n.ShowNodeAsync(It.IsAny<NodeId>()), Times.Never);
    }

    [Fact]
    public async Task OnModelChanged_ShouldEndTheTour_WhenAnotherModelOpens()
    {
        var tour = AssertOk(service.ParsePasted("Stop: UI\nText: a"));
        await service.StartAsync(tour);

        using (var model = modelMgr.UseModel())
            model.Path = "/other/model.sln";
        service.OnModelChanged();

        Assert.Null(service.Playing);
    }

    [Fact]
    public async Task OnModelChanged_ShouldLeaveOutStopsWhoseNodeIsGone_AndStayOnTheShownStop()
    {
        var tour = AssertOk(
            service.ParsePasted("Stop: UI\nText: a\n\nStop: Data.Repo\nText: b\n\nStop: Data\nText: c")
        );
        await service.StartAsync(tour, 2);

        using (var model = modelMgr.UseModel())
        {
            model.RemoveNode(model.Nodes[NodeId.FromName("Data.Repo")]);
        }
        service.OnModelChanged();

        Assert.Equal(["UI", "Data"], service.Playing!.Stops.Select(s => s.Name));
        Assert.Equal(1, service.StopIndex);
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
}
