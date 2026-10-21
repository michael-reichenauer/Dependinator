using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using ModelNode = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Models;

public class LayoutRefineServiceTests
{
    [Fact]
    public void SelectNext_ShouldPreferVisibleContainers()
    {
        using var model = NewModel();
        var visible = AddContainer(model, "Visible", new Rect(0, 0, 1000, 1000));
        var offView = AddContainer(model, "OffView", new Rect(5000, 5000, 2000, 2000));
        visible.IsChildrenLayoutRefinePending = true;
        offView.IsChildrenLayoutRefinePending = true;
        model.Zoom = 2; // The view shows canvas (-100, -100) to (1500, 1100), Visible entirely
        model.ViewRect = new Rect(-100, -100, 800, 600);

        Assert.Equal(visible, LayoutRefineService.SelectNext(model));

        visible.IsChildrenLayoutRefinePending = false;
        Assert.Equal(offView, LayoutRefineService.SelectNext(model));
    }

    [Fact]
    public void RefineAContainer_ShouldWait_WhileTheViewIsZoomedIntoIt()
    {
        // Rearranging it would move what the user looks at out of sight
        using var model = NewModel();
        var (container, children) = AddTangledContainer(model);
        var before = children.Select(child => child.Boundary).ToList();
        var job = Begin(model, container);
        model.Zoom = 0.1;
        model.ViewRect = new Rect(400, 400, 800, 600); // 80 x 60 canvas units inside the container

        Assert.Null(LayoutRefineService.SelectNext(model));
        Assert.False(LayoutRefineService.TryApply(model, job, []));
        Assert.True(container.IsChildrenLayoutRefinePending);
        Assert.Equal(before, children.Select(child => child.Boundary).ToList());

        model.Zoom = 2; // Zoomed out: the whole container is in view
        model.ViewRect = new Rect(-100, -100, 800, 600);
        Assert.Equal(container, LayoutRefineService.SelectNext(model));
        Assert.True(LayoutRefineService.TryApply(model, job, []));
    }

    [Fact]
    public void TryApply_ShouldApplyAClearlyBetterLayout()
    {
        using var model = NewModel();
        var (container, children) = AddTangledContainer(model);
        var before = children.Select(child => child.Boundary).ToList();
        var generation = NodeLayout.Generation;

        var applied = Refine(model, container, []);

        Assert.True(applied);
        Assert.False(container.IsChildrenLayoutRefinePending);
        Assert.NotEqual(before, children.Select(child => child.Boundary).ToList());
        Assert.True(NodeLayout.Generation > generation);
        Assert.All(
            children,
            child => Assert.Equal(new Size(100, 100), new Size(child.Boundary.Width, child.Boundary.Height))
        );
    }

    [Fact]
    public void TryApply_ShouldKeepALayoutThatIsAlreadyGood()
    {
        using var model = NewModel();
        var (container, children) = AddTangledContainer(model);
        Refine(model, container, []);
        var refined = children.Select(child => child.Boundary).ToList();
        container.IsChildrenLayoutRefinePending = true;

        var applied = Refine(model, container, []);

        Assert.False(applied);
        Assert.False(container.IsChildrenLayoutRefinePending);
        Assert.Equal(refined, children.Select(child => child.Boundary).ToList());
    }

    [Fact]
    public void TryApply_ShouldSkipAContainerTheUserArranged()
    {
        using var model = NewModel();
        var (container, children) = AddTangledContainer(model);
        var before = children.Select(child => child.Boundary).ToList();
        var job = Begin(model, container);
        container.IsChildrenLayoutCustomized = true;

        var applied = LayoutRefineService.TryApply(model, job, []);

        Assert.False(applied);
        Assert.False(container.IsChildrenLayoutRefinePending);
        Assert.Equal(before, children.Select(child => child.Boundary).ToList());
    }

    [Fact]
    public void TryApply_ContainerChangedMeanwhile_ShouldRetryAFewTimesThenGiveUp()
    {
        using var model = NewModel();
        var (container, children) = AddTangledContainer(model);
        var attempts = new Dictionary<NodeId, int>();

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var job = Begin(model, container);
            children[0].Boundary = children[0].Boundary with { X = children[0].Boundary.X + 20 };

            Assert.False(LayoutRefineService.TryApply(model, job, attempts));
            Assert.Equal(attempt < 3, container.IsChildrenLayoutRefinePending);
        }
    }

    static bool Refine(IModel model, ModelNode container, Dictionary<NodeId, int> attempts) =>
        LayoutRefineService.TryApply(model, Begin(model, container), attempts);

    static LayoutRefineService.RefineJob Begin(IModel model, ModelNode container)
    {
        var job = LayoutRefineService.Begin(model, container)!;
        job.EvaluateCurrent();
        job.Run.RunToCompletion();
        return job;
    }

    // A container whose dependency chain A -> B -> C -> D is laid out backward and stacked, so
    // its lines cross and run through nodes.
    static (ModelNode Container, List<ModelNode> Children) AddTangledContainer(IModel model)
    {
        var container = AddContainer(model, "Container", new Rect(0, 0, 1000, 1000));
        var children = new List<ModelNode>();
        foreach (var (name, index) in new[] { "A", "B", "C", "D", "E" }.Select((name, index) => (name, index)))
        {
            var child = new ModelNode($"Container.{name}", container)
            {
                Type = NodeType.ClassType,
                Boundary = new Rect(index % 2 * 300, index * 140, 100, 100),
            };
            container.AddChild(child);
            model.TryAddNode(child);
            children.Add(child);
        }
        AddLink(model, children[4], children[0]);
        AddLink(model, children[0], children[3]);
        AddLink(model, children[3], children[1]);
        AddLink(model, children[1], children[2]);
        AddLink(model, children[4], children[2]);
        container.IsChildrenLayoutRequired = false;
        container.IsChildrenLayoutRefinePending = true;
        return (container, children);
    }

    static IModel NewModel() => new ModelMgr(new StateMgr()).UseModel();

    static ModelNode AddContainer(IModel model, string name, Rect boundary)
    {
        var node = new ModelNode(name, model.Root) { Type = NodeType.Namespace, Boundary = boundary };
        model.Root.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static void AddLink(IModel model, ModelNode source, ModelNode target) =>
        new LineService().AddLinesFromSourceToTarget(model, new Link(source, target));
}
