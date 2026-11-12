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
- `UseJsonRpcClasses` serves every concrete class in the assembly that implements an `[Rpc]`
  interface and resolves it from DI, so a second, non-DI implementation (a helper or a wrapper)
  makes the host fail at startup. Share the code through a base class that does not declare the
  interface (`ModelQueryAnswers`).
- `ModelQueryService` answers on the most recently opened scope, so in the web host with parallel
  e2e tests a query can land on another test's circuit; test-mode code asks its own scope
  (`ScopeModelQueries`).

## Lines

Lines are the aggregated funnel by default. Descent is opt-in through `Node.LineSplitDepth` (node
toolbar +/−, both directions, pass-through nodes don't count as a level) and `Model.LineFocus`
(the explorer splits lines into its expanded rows, `Line.IsFocused`), both read by
`RepLineService.Sync`.
- Every change to split depth or focus calls `model.BumpStructureVersion()` (the Sync memo keys on
  it) and raises both `TriggerModelChanged` and `TriggerUIStateChanged`.
- Keep the perf defenses:
  - endpoint-visibility culling in `NodeLinesSvg` (`IsEitherEndpointRendered`);
  - no eager pruning of inactive lines (it caused GC stalls of up to 9 s);
  - the `Sync` skip via `StructureVersion` plus the expansion memo, so zooming skips Sync in the
    default view.
- Lines render on top of child nodes, with anchors at centered edge midpoints.
- Hover CSS must never shrink a hit target: WebKit hit-tests a press that follows the hover
  restyle against the restyled shape. The `.hoverable:hover` rule once shrank a line's wide hit
  polyline to 2 px, and WebKit clicks landed on the canvas. It now skips `LineSvg.HitClass`,
  and a `pointer-events="none"` polyline (`LineSvg.HoverClass`) draws the highlight
  (`LineSvgHitTargetTests`). Node hover rects only grow under the rule, and a zone's filled
  rect loses at most a sub-pixel band of its border: both harmless.
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
- Siblings open as containers at the same zoom (`NodeViewPolicy.OpenSize`: a node stops being an
  icon at `MinContainerZoom`, whatever its size, so the layout's sizes open at 120 to 360 px on
  screen). Only a node resized by hand outside 50 to 200 units opens by its own size on screen
  (at 100 or 400 px), so a resize can still open a node at an unchanged zoom. The node's own
  chrome still follows its size on screen (zoomed past, and the background fade, which starts a
  doubling of the zoom after the node opened). "Show node" frames a node so that every ancestor
  is open (`OpenAncestorsFactor`): a node that is large in its parent is framed closer than as
  an icon, since being visible goes first.
- `NodeLayout.Generation` is bumped by layouts, fits and geometry commands.
  `RepLineService.Sync` skips the same-zoom sync only at an unchanged generation, and
  `NodeTreeSvg.RenderRootContent` re-renders at most 3 rounds after a lazy layout.
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

## Member cards

- Member rows are laid out by `MemberCard` (sections, columns, `RowPitch` 16, off the 20-grid on
  purpose); headings and the card are derived from row positions when rendering, nothing stored.
  Rows never move or resize by hand (`NodeEditService` skips members), and a customized container
  still re-arranges its rows (`NodeLayout.ArrangeCustomizedTypeChildren`).
- A node of only members is a square like every sized node, and its card flows its columns to
  the square (`MemberCard.Arrange` with the container's aspect), `MemberCardCoverage` 0.9;
  `ApplyContainerTransform` caps the zoom at 1, so a tiny card covers less. Shaping the node like
  its card (#178, removed Nov 7 2026) made long-signature types up to 1.8x wider than tall: the
  icon stays square in the box, so lines (anchored at the box edges) ended far from the icon.
- The summary card is part of `IsShowIcon` (lines attach to the type, a click selects it); it is
  decided by `zoom * ContainerZoom`, so `NodeTreeSvg.RenderNode` lays such nodes out before
  deciding. Its rows use class `summaryMember`, never `memberName`, which the e2e node locators match.
- "Show node" on a member frames it closer (`NodeViewPolicy.FramingZoomFactor`): rows readable and
  the parent open, also 1.3x apart in a tile. A small type's card is at natural scale, so at 1 px
  per unit its type would still be an icon (the e2e `WaitForContainerNodeAsync("Main")` failed).
- A row's signature and description are `<tspan>`s inside its `text.memberName`, so the name
  text has `data-name` (the e2e point finder reads `dataset.name ?? textContent`); `ColumnWidth`
  counts the signature (up to `MaxColumnWidth`).
- Text with `<tspan>` children must not rely on `dominant-baseline` (middle, hanging): WebKit
  (iPad Safari) and Firefox do not apply it to the tspans, so they sit off the first run. Use the
  alphabetic baseline and shift `y` (`MemberRowSvg.MemberBaselineOffsetFactor`, `SvgText`'s
  `DescriptionFirstLineOffsetFactor`).
- "+ N private" (`MemberCard.HasPrivateToggle`, `Node.IsPrivateMembersShown`, saved): folded
  private rows get the toggle row's rect (their lines end there) and are skipped in
  `NodeTreeSvg.IsRendered`. Rendering asks the fold as of the last layout (`Node.LaidOutToggle`,
  `IsFoldedAway`, `LaidOutToggleRect`; runtime only, `MemberCard.RestoreLayoutState` on load),
  never the live member counts: a re-parse can cross `FoldMinRows` or change a member's access
  without a layout, and `MemberCard.IsLayoutStale` (checked in `NodeTreeSvg.RenderNode`) asks for
  one. Going to a folded member (`NavigationService.EnsureLayoutForPath`) opens the private rows. The toggle (`PointerId` sub-id "pt") is drawn after the lines, or a
  line polyline takes its click. Toggling re-lays out the card at once and the view follows
  (`CardToggleMove`, `PanZoomService.FollowPoint`): more rows in the same box means a smaller
  `ContainerZoom`, which otherwise drops the card back to its summary.
- An open member card node (`NodeViewPolicy.IsMemberCardNode`) is drawn by `MemberCardSvg.GetCardNodeSvg`
  (the shared `CardFrameSvg` header, no container label, no inner card rect) and laid out by
  `NodeLayout.ArrangeCardNode`: children from child (0,0) at the box's top left
  (`ApplyCardTransform`, zoom ≤ 1), rows from `MemberCard.CardRowsTop`, nested types below.
- Card colors are fixed (`DColors.CardHeader`/`CardFrame`, `MemberCardSvg.CardColors`), not the type's
  random color; only a user-set custom color or edit mode uses `NodeColors`. A private row is
  muted and gets `BadgeSvg.Private`, a padlock on its icon's lower right corner (Michael
  picked it over a faded icon, a UML "-" and the earlier "private" text on the right, Nov 6).
- Lines between members of one type carry `line-inner` (LineSvg) and are hidden unless lit or
  hover-previewed (Canvas LineCss, jsInterop.js `initLinePreview`).

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
  `NodeTreeSvg.IsRendered` and in the minimap, and `IsLineFilteredOut` drops lines to a hidden row.

## iPad home-screen web app (Nov 2026)

- iPadOS 26 draws a foggy "scroll edge" blur over the top ~100px of a home-screen web app (the
  toolbar too) unless an opaque, fixed element covers the top edge and takes pointer events
  (WebKit hit-tests ~4px inside the edge, then colors the status bar with it): that is
  `.dep-top-edge` (5px, canvas color). Tried on the device and not enough on their own: a
  `pointer-events: none` strip or header, `viewport-fit=cover` with safe-area insets, a page
  that cannot scroll. The meta tags in `index.html`/`_Host.cshtml` (status bar `default`, never
  `black-translucent`) matter too, and iOS reads them only when the icon is added: re-add it
  to test. A Playwright browser cannot reproduce any of this; the user tests on the iPad.
- The canvas fits the window exactly (`ScreenService.CheckResizeAsync`, svg `display: block`);
  the page itself never scrolls.
