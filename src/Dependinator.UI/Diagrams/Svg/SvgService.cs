using System.Globalization;
using Dependinator.UI.Diagrams.Cycles;
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

        // The badges are baked into the tiles, and the problems can change without a model
        // change: the counts of a parse are only taken once its progress ends. Read before the
        // tiles lock, since it takes the model lock.
        var nodeProblems = viewOptions.IsProblemBadgesShown ? problemService.GetNodeProblems() : null;
        // While hidden nodes are revealed they are drawn, so nothing needs a badge.
        var hiddenChildCounts =
            viewOptions.IsHiddenBadgesShown && !viewOptions.RevealHiddenNodes
                ? hiddenNodeService.GetHiddenChildCounts()
                : null;

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
                return tile;
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

    public string GetContentSvg(Rect canvasRect, double zoom)
    {
        using var model = modelMgr.UseModel();
        if (model.Root.Children.Count == 0 || canvasRect.Width <= 0 || canvasRect.Height <= 0 || zoom <= 0)
            return "";

        var offset = new Pos(-canvasRect.X / zoom, -canvasRect.Y / zoom);
        var bounds = new Rect(0, 0, canvasRect.Width / zoom, canvasRect.Height / zoom);
        // Exports never show edit chrome (handles), selection or cycle-pick dimming or problem badges, whatever
        // the current state.
        var flags = Flags with
        {
            IsEditing = false,
            DimUnrelated = false,
            SelectedCycleLines = null,
        };
        var context = new RenderContext(offset, 1 / zoom, bounds, Pos.None, flags);
        return NodeTreeSvg.RenderRootContent(model, zoom, context);
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

        // The problems GetTile checked the cache against, so the tile matches them.
        var viewFlags = Flags;
        var selection = LineEmphasis.FindSelection(model);
        var flags = viewFlags with
        {
            Selection = selection,
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
