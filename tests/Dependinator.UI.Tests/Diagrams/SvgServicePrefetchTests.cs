using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Cycles;
using Dependinator.UI.Diagrams.Navigation;
using Dependinator.UI.Diagrams.Rules;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Diagrams.Tiles;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Rect = Dependinator.UI.Shared.Types.Rect;

namespace Dependinator.UI.Tests.Diagrams;

// Tiles drawn ahead while the view rests (SvgService.PrefetchNext): the next zoom level in, then
// out, around the view's center, cached without changing the shown view.
public class SvgServicePrefetchTests
{
    static readonly Rect ViewRect = new(0, 0, 1000, 600);
    const double Zoom = 1;

    static (SvgService Service, IModelMgr ModelMgr, ITilesMgr TilesMgr) Create()
    {
        var stateMgr = new StateMgr();
        IModelMgr modelMgr = new ModelMgr(stateMgr);
        using (var model = modelMgr.UseModel())
        {
            foreach (var (name, x) in new[] { ("A", 100.0), ("B", 400.0), ("C", 700.0) })
            {
                var node = new Node(name, model.Root) { Boundary = new Rect(x, 200, 100, 100) };
                model.Root.AddChild(node);
                model.TryAddNode(node);
            }
        }

        ITilesMgr tilesMgr = new TilesMgr(stateMgr, Mock.Of<IApplicationEvents>());
        var service = new SvgService(
            modelMgr,
            tilesMgr,
            new ViewOptions(),
            new CycleService(modelMgr, Mock.Of<Dependinator.UI.Modeling.Commands.ICommandService>()),
            Mock.Of<IPathFinderService>(),
            Mock.Of<IRuleService>(),
            Mock.Of<IProblemService>(),
            Mock.Of<IHiddenNodeService>()
        );
        return (service, modelMgr, tilesMgr);
    }

    // The tile at a level around the view's center. At Zoom 1, a level's own zoom, the levels
    // next to it must come out as 1 and -1 (the floor of the log would round 1 / 1.3 to 2).
    static TileKey CenteredKey(int level)
    {
        var zoom = TileKey.LevelZoom(level);
        return TileKey.AtLevel(
            new Rect(
                ViewRect.X + ViewRect.Width / 2 * (Zoom - zoom),
                ViewRect.Y + ViewRect.Height / 2 * (Zoom - zoom),
                ViewRect.Width,
                ViewRect.Height
            ),
            level
        );
    }

    [Fact]
    public void PrefetchNext_ShouldDrawTheNextLevelInThenOut_ThenNothing()
    {
        var (service, _, tilesMgr) = Create();
        var shown = service.GetTile(ViewRect, Zoom);
        Assert.Equal(0, shown.Key.Z);
        var zoomInKey = CenteredKey(1);
        var zoomOutKey = CenteredKey(-1);

        Assert.True(service.PrefetchNext(ViewRect, Zoom));
        using (var tiles = tilesMgr.UseTiles())
        {
            Assert.True(tiles.Contains(zoomInKey));
            Assert.False(tiles.Contains(zoomOutKey));
        }

        Assert.True(service.PrefetchNext(ViewRect, Zoom));
        Assert.False(service.PrefetchNext(ViewRect, Zoom));
        using (var tiles = tilesMgr.UseTiles())
        {
            Assert.True(tiles.Contains(zoomOutKey));
            Assert.NotEqual(shown.Key, zoomInKey);
            Assert.NotEqual(shown.Key, zoomOutKey);
            // The shown tile is still the last used one
            Assert.True(tiles.TryGetLastUsed(ViewRect, Zoom, out var lastUsed));
            Assert.Same(shown, lastUsed);
        }
    }

    [Fact]
    public void PrefetchNext_ShouldLeaveTheViewState()
    {
        var (service, modelMgr, _) = Create();
        service.GetTile(ViewRect, Zoom);

        service.PrefetchNext(ViewRect, Zoom);

        using var model = modelMgr.UseModel();
        Assert.Equal(ViewRect, model.ViewRect);
        Assert.Equal(Zoom, model.Zoom);
    }

    [Fact]
    public void GetTile_ShouldUseThePrefetchedTile_WhenZoomedInAroundTheCenter()
    {
        var (service, _, tilesMgr) = Create();
        service.GetTile(ViewRect, Zoom);
        service.PrefetchNext(ViewRect, Zoom);
        Tile prefetched;
        using (var tiles = tilesMgr.UseTiles())
            Assert.True(tiles.TryGetCached(CenteredKey(1), ViewRect, Zoom, out prefetched));

        // Two wheel ticks in (1.14 each), around the center: level 1
        var zoom = Zoom / 1.14 / 1.14;
        var zoomedIn = new Rect(
            ViewRect.Width / 2 * (Zoom - zoom),
            ViewRect.Height / 2 * (Zoom - zoom),
            ViewRect.Width,
            ViewRect.Height
        );
        var tile = service.GetTile(zoomedIn, zoom);

        Assert.Same(prefetched, tile);
    }

    [Fact]
    public void PrefetchNext_ShouldDoNothing_BeforeTheViewIsDrawn()
    {
        var (service, _, tilesMgr) = Create();
        service.GetTile(ViewRect, Zoom);
        using (var tiles = tilesMgr.UseTiles())
            tiles.ClearCache(); // A model change

        Assert.False(service.PrefetchNext(ViewRect, Zoom));
        using (var tiles = tilesMgr.UseTiles())
            Assert.False(tiles.Contains(CenteredKey(1)));
    }

    [Fact]
    public void PrefetchNext_ShouldDoNothing_ForAnEmptyModel()
    {
        var stateMgr = new StateMgr();
        IModelMgr modelMgr = new ModelMgr(stateMgr);
        var service = new SvgService(
            modelMgr,
            new TilesMgr(stateMgr, Mock.Of<IApplicationEvents>()),
            new ViewOptions(),
            new CycleService(modelMgr, Mock.Of<Dependinator.UI.Modeling.Commands.ICommandService>()),
            Mock.Of<IPathFinderService>(),
            Mock.Of<IRuleService>(),
            Mock.Of<IProblemService>(),
            Mock.Of<IHiddenNodeService>()
        );

        Assert.False(service.PrefetchNext(ViewRect, Zoom));
    }
}
