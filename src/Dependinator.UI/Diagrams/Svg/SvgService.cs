using System.Globalization;
using Dependinator.UI.Diagrams.Cycles;
using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Diagrams.Navigation;
using Dependinator.UI.Diagrams.Rules;
using Dependinator.UI.Diagrams.Tiles;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

// Generates the SVG rendering of the diagram from the model, producing the tiled SVG content
// drawn on the canvas. The drawing itself is NodeTreeSvg's; this service owns the tile cache and
// gathers the view state a render needs (ViewFlags).
namespace Dependinator.UI.Diagrams.Svg;

interface ISvgService
{
    Tile GetTile(Rect viewRect, double zoom);

    // Draws one tile the view is likely to need next and caches it, if it is not cached yet: the
    // tile one zoom level in, then one level out, around the view's center. Returns whether it
    // drew one, so the caller can ask again for the next. Leaves the view state as it was.
    bool PrefetchNext(Rect viewRect, double zoom);

    // Renders the diagram content covering canvasRect (canvas coordinates) at zoom (canvas units
    // per output pixel), in output-pixel coordinates with origin at the rect's top-left. Returns
    // the bare content (no <svg> wrapper, no defs) for embedding in an export document. Unlike
    // GetTile, this renders fresh (no tile cache) and does not touch the view state.
    string GetContentSvg(Rect canvasRect, double zoom);
}

[Transient]
class SvgService : ISvgService
{
    // Nodes smaller than this (in tile pixels) in both dimensions are skipped entirely,
    // including their whole subtree: they are invisible, but at far-out zoom containers can
    // hold hundreds of them, each otherwise emitting full icon markup. Lines to skipped nodes
    // still draw (see IsEndpointRendered), keeping the far-zoom line texture intact.
    readonly IModelMgr modelMgr;
    readonly ITilesMgr tilesMgr;
    readonly IViewOptions viewOptions;
    readonly ICycleService cycleService;
    readonly IPathFinderService pathFinderService;
    readonly IRuleService ruleService;
    readonly IProblemService problemService;
    readonly IHiddenNodeService hiddenNodeService;
    IReadOnlyDictionary<NodeId, NodeProblems>? tilesNodeProblems;
    IReadOnlyDictionary<NodeId, int>? tilesHiddenChildCounts;

    // The tile the lines were last synced to (SyncLines), Empty before any.
    TileKey syncedKey = TileKey.Empty;

    public SvgService(
        IModelMgr modelMgr,
        ITilesMgr tilesMgr,
        IViewOptions viewOptions,
        ICycleService cycleService,
        IPathFinderService pathFinderService,
        IRuleService ruleService,
        IProblemService problemService,
        IHiddenNodeService hiddenNodeService
    )
    {
        this.modelMgr = modelMgr;
        this.tilesMgr = tilesMgr;
        this.viewOptions = viewOptions;
        this.cycleService = cycleService;
        this.pathFinderService = pathFinderService;
        this.ruleService = ruleService;
        this.problemService = problemService;
        this.hiddenNodeService = hiddenNodeService;
    }

    // The user's view toggles that affect what a tile contains; captured per render so the
    // static render helpers need no service access.
    ViewFlags Flags =>
        new(viewOptions.IsEditingEnabled, viewOptions.RevealHiddenNodes, viewOptions.DimUnrelatedLines)
        {
            LinkCountWidths = viewOptions.IsLinkCountWidthsShown,
            Filter = viewOptions.LineFilter,
            Rows = viewOptions.RowFilter,
            CyclicLines = viewOptions.IsCyclesShown ? cycleService.GetCyclicLineIds() : null,
            SelectedCycleLines = SelectedCycleKey is { } cycleKey ? cycleService.GetCycleLineIds(cycleKey) : null,
            SelectedCycleMembers = SelectedCycleKey is { } key ? cycleService.GetCycleMemberIds(key) : null,
            PathLines = pathFinderService.GetPathLineIds(),
            ViolatingLines = viewOptions.IsRulesShown ? ruleService.GetViolatingLineIds() : null,
        };

    string? SelectedCycleKey => viewOptions.IsCyclesShown ? viewOptions.SelectedCycleKey : null;

    public Tile GetTile(Rect viewRect, double zoom)
    {
        using (var model = modelMgr.UseModel())
        {
            if (model.Root.Children.Count == 0)
                return Tile.Empty;
            if (viewRect.Width == 0 || viewRect.Height == 0)
                return Tile.Empty;

            model.ViewRect = viewRect;
            model.Zoom = zoom;
        }

        // Read before the tiles lock, since it takes the model lock.
        var (nodeProblems, hiddenChildCounts) = GetBadges();

        TileKey tileKey;
        Tile tile;
        using (var tiles = tilesMgr.UseTiles())
        {
            if (
                !ReferenceEquals(nodeProblems, tilesNodeProblems)
                || !ReferenceEquals(hiddenChildCounts, tilesHiddenChildCounts)
            )
            {
                tiles.ClearCache();
                tilesNodeProblems = nodeProblems;
                tilesHiddenChildCounts = hiddenChildCounts;
            }

            if (tiles.TryGetLastUsed(viewRect, zoom, out tile))
                return tile; // Same tile as last call

            tileKey = TileKey.From(viewRect, zoom);
            if (tiles.TryGetCached(tileKey, viewRect, zoom, out tile))
            {
                // The lines follow the tile that is shown (a tile drawn ahead, or one from an
                // earlier visit, was drawn while they were synced to another one).
                if (tile.Key != syncedKey)
                {
                    using var model = modelMgr.UseModel();
                    SyncLines(model, tile.Key, LineEmphasis.FindSelection(model));
                }
                return tile;
            }
        }

        // Create a new tile and cache it
        using (var model = modelMgr.UseModel())
        {
            tile = CreateModelTile(model, tileKey, nodeProblems, hiddenChildCounts);
        }
        using (var tiles = tilesMgr.UseTiles())
        {
            tiles.SetCached(tile, viewRect, zoom);
        }

        return tile;
    }

    // The badges are baked into the tiles, and the problems can change without a model change:
    // the counts of a parse are only taken once its progress ends.
    (IReadOnlyDictionary<NodeId, NodeProblems>?, IReadOnlyDictionary<NodeId, int>?) GetBadges()
    {
        var nodeProblems = viewOptions.IsProblemBadgesShown ? problemService.GetNodeProblems() : null;
        // While hidden nodes are revealed they are drawn, so nothing needs a badge.
        var hiddenChildCounts =
            viewOptions.IsHiddenBadgesShown && !viewOptions.RevealHiddenNodes
                ? hiddenNodeService.GetHiddenChildCounts()
                : null;
        return (nodeProblems, hiddenChildCounts);
    }

    public bool PrefetchNext(Rect viewRect, double zoom)
    {
        if (viewRect.Width == 0 || viewRect.Height == 0 || zoom <= 0)
            return false;

        // The model lock is the tiles lock (one state lock), so nothing changes the model between
        // drawing the tile and caching it.
        using var model = modelMgr.UseModel();
        if (model.Root.Children.Count == 0)
            return false;
        var (nodeProblems, hiddenChildCounts) = GetBadges();

        using var tiles = tilesMgr.UseTiles();
        // Only ahead of a view that is drawn and cached with the current badges and screen size:
        // otherwise the next GetTile clears the cache, or the cache was cleared (a model change)
        // and the view's own tile comes first.
        var viewKey = TileKey.From(viewRect, zoom);
        if (
            !ReferenceEquals(nodeProblems, tilesNodeProblems)
            || !ReferenceEquals(hiddenChildCounts, tilesHiddenChildCounts)
            || !tiles.Contains(viewKey)
        )
            return false;
        if (GetPrefetchKeys(viewKey, viewRect, zoom).FirstOrDefault(key => !tiles.Contains(key)) is not { } missing)
            return false;

        try
        {
            return tiles.AddPrefetched(CreateModelTile(model, missing, nodeProblems, hiddenChildCounts));
        }
        finally
        {
            // Drawing a tile syncs the lines to its zoom and area: sync them back to the view's
            // tile, which hit testing, the hover cards and the toolbars read (also when the
            // drawing failed half way).
            SyncLines(model, viewKey, LineEmphasis.FindSelection(model));
        }
    }

    // The tiles of the next zoom level in and out, around the view's center. In first: zooming in
    // draws the denser tiles. The levels come from the view's key, not from its zoom times
    // ZoomFactor: at a level's own zoom (1 after a fit) the floor of the log rounds a level off.
    static IEnumerable<TileKey> GetPrefetchKeys(TileKey viewKey, Rect viewRect, double zoom)
    {
        var centerX = viewRect.X + viewRect.Width / 2 * zoom;
        var centerY = viewRect.Y + viewRect.Height / 2 * zoom;
        foreach (var level in new[] { viewKey.Z + 1, viewKey.Z - 1 })
        {
            var levelZoom = zoom * TileKey.LevelZoom(level) / viewKey.GetTileZoom();
            var rect = new Rect(
                centerX - viewRect.Width / 2 * levelZoom,
                centerY - viewRect.Height / 2 * levelZoom,
                viewRect.Width,
                viewRect.Height
            );
            yield return TileKey.AtLevel(rect, level);
        }
    }

    public string GetContentSvg(Rect canvasRect, double zoom)
    {
        using var model = modelMgr.UseModel();
        if (model.Root.Children.Count == 0 || canvasRect.Width <= 0 || canvasRect.Height <= 0 || zoom <= 0)
            return "";

        var offset = new Pos(-canvasRect.X / zoom, -canvasRect.Y / zoom);
        var bounds = new Rect(0, 0, canvasRect.Width / zoom, canvasRect.Height / zoom);
        // Exports never show edit chrome (handles), selection or cycle-pick dimming or problem badges, whatever
        // the current state.
        var flags = Flags with { IsEditing = false, DimUnrelated = false, SelectedCycleLines = null };
        var context = new RenderContext(offset, 1 / zoom, bounds, Pos.None, flags);
        return NodeTreeSvg.RenderRootContent(model, zoom, context);
    }

    // The canvas area (canvas units) a tile renders: its rect with the margin, in level
    // coordinates, which are canvas coordinates over the level zoom.
    internal static Rect GetCanvasArea(Rect tileRect, Rect tileWithMargin, double tileZoom) =>
        new(
            (tileRect.X + tileWithMargin.X) * tileZoom,
            (tileRect.Y + tileWithMargin.Y) * tileZoom,
            tileWithMargin.Width * tileZoom,
            tileWithMargin.Height * tileZoom
        );

    // Syncs the lines to a tile's zoom and area, as the tile draws them. A selected stub's lines
    // are drawn end to end, except while cycles are shown: the cycle lines are the lines between
    // siblings, which must then stay as they are.
    LineReveal SyncLines(IModel model, TileKey tileKey, RenderSelection selection)
    {
        var tileZoom = tileKey.GetTileZoom();
        var reveal = viewOptions.IsCyclesShown
            ? LineReveal.None
            : NodeTreeSvg.GetLineReveal(
                selection,
                viewOptions.RowFilter,
                viewOptions.LineFilter,
                // Only a selected or explored edge line asks, and it exists before the Sync.
                selection.ExploredStub
                    is not null
                || selection.Line is { } selectedLine && LineStub.IsEdgeLine(selectedLine)
                    ? pathFinderService.GetPathLineIds()
                    : null
            );
        // A selected stub opens no far container outside the tile with its margin, the area the
        // tile draws nodes in: its lines end at that container.
        if (reveal.Stub is not null)
            reveal = reveal with
            {
                Area = GetCanvasArea(tileKey.GetTileRect(), tileKey.GetTileRectWithMargin(), tileZoom),
            };
        RepLineService.Sync(model, tileZoom, reveal);
        syncedKey = tileKey;
        return reveal;
    }

    Tile CreateModelTile(
        IModel model,
        TileKey tileKey,
        IReadOnlyDictionary<NodeId, NodeProblems>? nodeProblems,
        IReadOnlyDictionary<NodeId, int>? hiddenChildCounts
    )
    {
        var timing = Timing.Start();
        var tileRect = tileKey.GetTileRect();
        var tileWithMargin = tileKey.GetTileRectWithMargin();
        var tileZoom = tileKey.GetTileZoom();
        var tileOffset = new Pos(-tileRect.X, -tileRect.Y);

        // The lines are synced before the flags are read, since a selection can create lines and
        // the rule and path line sets below must hold them.
        var selection = LineEmphasis.FindSelection(model);
        var reveal = SyncLines(model, tileKey, selection);

        // The problems GetTile checked the cache against, so the tile matches them.
        var viewFlags = Flags;
        var flags = viewFlags with
        {
            Selection = selection,
            Reveal = reveal,
            DimSelection = LineEmphasis.FindDimSelection(model, selection, viewFlags.SelectedCycleMembers),
            NodeProblems = nodeProblems,
            HiddenChildCounts = hiddenChildCounts,
        };
        var rootContext = new RenderContext(tileOffset, 1 / tileZoom, tileWithMargin, Pos.None, flags);
        var rootContentSvg = NodeTreeSvg.RenderRootContent(model, tileZoom, rootContext);

        // Enable this if need to show tile border and/or tile with margin border
        // var tileBorderSvg = $"""<rect x="{0}" y="{0}" width="{tileRect.Width:0.##}" height="{tileRect.Height:0.##}" stroke-width="{3}" rx="5" fill="none" stroke="red"/>""";
        // var tileMarginBorderSvg = $"""<rect x="{tileWithMargin.X}" y="{tileWithMargin.Y}" width="{tileWithMargin.Width:0.##}" height="{tileWithMargin.Height:0.##}" stroke-width="{3}" rx="5" fill="none" stroke="green"/>""";

        var (x, y, w, h) = tileKey.GetViewRect();
        var tileViewBox = string.Create(CultureInfo.InvariantCulture, $"{x:0.##} {y:0.##} {w:0.##} {h:0.##}");
        var tileSvg = string.Create(
            CultureInfo.InvariantCulture,
            $"""
            <svg width="{w:0.##}" height="{h:0.##}" viewBox="{tileViewBox}" xmlns="http://www.w3.org/2000/svg">
              {rootContentSvg}
            </svg>
            """
        );

        Log.Info($"Tile {tileKey}: {tileSvg.Length} chars in {timing.ElapsedMs}ms");
        return new Tile(tileKey, tileSvg, tileZoom, tileOffset);
    }
}
