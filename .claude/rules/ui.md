---
paths:
  - "src/Dependinator.UI/**"
---

# Dependinator.UI: traps and invariants

## Repainting and Blazor

- `applicationEvents.TriggerModelChanged()` only clears the tile cache and marks icon defs stale;
  nothing repaints. The canvas and the toolbars re-render on `UIStateChanged`. A change to what
  the diagram shows must raise both, or it stays invisible until the next unrelated UI event,
  while the unit tests pass.
- MudBlazor 9 `MudTreeViewItem` writes the new `Expanded` into the item before it raises
  `ExpandedChanged`, so a handler that returns early on `item.Expanded == newValue` does nothing.
  Compare derived state instead (`DependenciesService.UpdateFocus`, `LineFocus.IsSameAs`).
- A `MudDialog`'s `TitleContent` re-renders only through `IMudDialogInstance.StateHasChanged()`.
- Blazor Server shares statics across circuits (users, and parallel e2e tests), so per-user state
  goes in scoped services (`IViewOptions`), never in statics.

## Lines

Lines are the aggregated funnel by default. Descent is opt-in through `Node.LineSplitDepth` (node
toolbar +/−, both directions, pass-through nodes don't count as a level) and `Model.LineFocus`
(the explorer splits lines into its expanded rows, `Line.IsFocused`), both read by
`RepLineService.Sync`.
- Every change to split depth or focus calls `model.BumpStructureVersion()` (the Sync memo keys on
  it) and raises both `TriggerModelChanged` and `TriggerUIStateChanged`.
- Keep the perf defenses:
  - endpoint-visibility culling in `SvgService` (`IsEitherEndpointRendered`);
  - no eager pruning of inactive lines (it caused GC stalls of up to 9 s);
  - the `Sync` skip via `StructureVersion` plus the expansion memo, so zooming skips Sync in the
    default view.
- Lines render on top of child nodes, with anchors at centered edge midpoints.
- Hover CSS must never change a hit target's geometry: WebKit hit-tests a press that follows
  the hover restyle against the restyled shape. The `.hoverable:hover` rule once shrank a line's
  wide hit polyline to 2 px, and WebKit clicks landed on the canvas. It now skips `.line-hit`,
  and a separate `pointer-events="none"` polyline draws the hover highlight (`LineSvg`).
- `NodeViewPolicy.IsRenderedFlat` (zoom > 1000) prevents the blank deep-zoom canvas caused by
  single-precision nested `<svg>` offsets (`SvgServiceDeepZoomTests`).
- Perf regressions first show up as flaky menu timeouts in unrelated e2e tests.

## Rendering performance

- Keep `WheelZoomSpeed` below `TileKey.ZoomFactor` (1.3), or every wheel tick rebuilds all tiles.
- `CreateModelTile` logs `Tile ((x,y,z)): N chars in Mms` to the app log (`~/Dependinator.log`):
  use it for before/after comparisons. Deferred until measured: a selection overlay layer,
  WebKit-gating `refreshSvgPaintServers`, a pan/zoom save debounce, description-wrap caching.
- WebKit loses `<use>` gradient fills when Blazor replaces the diagram subtree; `refreshSvgPaintServers`
  (jsInterop.js, WebKit only) re-appends `<defs>` when `SvgContent` changed by reference. No e2e
  test can see it (only pixels differ). To reproduce, run playwright-cli with `--browser webkit`,
  double-click the canvas and pick an icon.

## Layout (`Modeling/Layout/`)

- Node sizes are the steps {60, 100, 140, 180}, so centers and line anchors stay on the 20-grid.
  Vertical snapping rounds half up (`LayeredEngine.SnapHalfUp`). "Show node" frames at most
  `NodeViewPolicy.MaxFramedNodeSize`, because tiles render up to 1.3x the view zoom.
- `NodeViewPolicy.EffectiveZoom` treats `Rect.None` as size 100 (many tests build nodes without a
  boundary).
- `NodeLayout.Generation` is bumped by layouts, fits and geometry commands.
  `RepLineService.Sync` skips the same-zoom sync only at an unchanged generation, and
  `SvgService.RenderRootContent` re-renders at most 3 rounds after a lazy layout.
- `NodeDto.IsChildrenLayoutRequired` is persisted. A `ModelDto.LayoutVersion` below
  `CurrentLayoutVersion` re-flags only containers the user has not customized.
- Work budgets, measured in WASM Release (~0.4 ms per 1k units, ~10x slower than Blazor Server):
  inline 250k, reset 2M, refine 10M in 20k slices.
- `LayoutRefineService` never refines a container the view is zoomed into: that once blanked the
  canvas.
- `LayeredLayoutGoldenTests` pin the fast pass. To exercise refine on the demo, lower
  `NodeLayout.InlineThoroughWorkBudget` temporarily (only Demo.UI.Diagrams exceeds 250k).
- For WASM timings, publish `src/Dependinator.Wasm` in Release and serve its `wwwroot` with
  `python3 -I -m http.server 5000`.

## Design layer: zones, notes, line labels, aliases, hide, rows

- A zone is a manual node with `IsZone`. Its label is the `Alias`, and its name is a generated
  identity ("Zone 2", via `DiagramPlacement.UniqueShortName`), because names split on ".", "/"
  and "(". Editing a zone is a `NodeEditCommand` (Alias/Description), never a rename.
- `NodeViewPolicy.IsAnnotation` keeps zones and notes out of container semantics. Zones draw
  before sibling lines and the selection overlay after the siblings. `NodeLayout.IsArranged`
  keeps zones and notes in place in forced layouts, and `SelectionService` exempts zones from the
  viewport-cover rule. Mirror these guards for any new annotation kind.
- Line labels:
  - `Line.Label` is separate from the parsed `Description`, which `ClearNotUpdated` wipes.
  - `Model.PendingLineLabels` keeps a label by line id while its line is recreated or not yet
    rendered.
  - `LineDto` carries the Source and Target names with a label, because line ids are hashes.
  - Labels render in their own layer after the child nodes.
  - `StructureService.RenameNode` re-parks labels under the new line ids.
- Alias edits bump `StructureVersion`, because panels cache their rows by it. Tooltips stay
  `"{LongName} ({Alias})"`, because `App.Node(label)` in the e2e tests matches the long-name prefix.
- `HideNodesCommand` needs its own `MergeKey`: same-type commands within 500 ms merge, which turned
  hide+show into a no-op undo. The line rule lives in `LineVisibility.Update`.
- View › Rows is a view option (`RowFilter`), not a model edit: rows are skipped in
  `SvgService.IsRendered` and in the minimap, and `IsLineFilteredOut` drops lines to a hidden row.
