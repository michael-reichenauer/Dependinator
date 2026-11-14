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

Lines are the aggregated funnel by default. Descent is opt-in through `Model.LineFocus` (the
explorer splits lines into its expanded rows, `Line.IsFocused`) and a selected stub
(`LineReveal`, below), both read by `RepLineService.Sync`. A per-node split depth
(`Node.LineSplitDepth`, the node menu's "Split Lines One Level Deeper" and "Merge Lines Back")
was removed on Nov 14 2026 at Michael's request: the stubs made it unneeded. Don't bring it back.
- **Stubs (Nov 13 2026, Michael's idea).** The lines between a container's edge and its children
  (`LineStub.IsEdgeLine`) are drawn as a short arrow with a ring at the child, not as the long
  fan to the middle of the container's edge. A stub is the same line drawn short (`LineSvg`
  swaps the points): same element id, hover card, selection and rule colour. Drawn in full
  still: an edge line on the dependency path being shown, one with a label, one the user routed
  with line points (adding a point to a stub brings the full line back), and one whose links all
  end at the container itself (a method using its own type: `LineStub.CrossesBorder`, nothing
  continues outside). Stubs are level (upright
  at an inheritance end) because stubs in the old line's direction looked like a hedgehog;
  `LineStub.GetDirection` turns one aside when a sibling line leaves the same anchor that way.
  The length is in the container's units (so the two stubs between neighbours stay apart at the
  layout's 60-unit gap) within pixel limits.
- **A selected stub's lines (Nov 13, Michael).** Selecting a stub draws that stub's lines end
  to end: what uses the node from outside its container, or what it uses outside, not the lines
  of the node's other stub. `RepLineService.Sync` takes a `LineReveal` (from
  `NodeTreeSvg.GetLineReveal`): `Stub` for a selected `LineStub.IsStub` line (an edge line drawn
  in full for its label, its line points or the shown path is selected for itself and gets no
  lines). The stub's own links (`stub.Contains(link)`) that cross the border are pinned at the
  child and descend on the far side as deep as the nodes are open, as cousin lines flagged
  `Line.IsRevealed`; the stub itself stays active as the selected line when the reveal took its
  links (`Sync` sets `IsActiveRep`; not when the explorer's focus took them), and
  `LineEmphasis.IsLineDimmed` keeps the `IsRevealed` lines undimmed beside a selected line. The
  reveal is part of the Sync memo, the lines it creates are inactive again after the selection
  (never pruned, never saved), and an export has no selection. It leaves out what View › Rows
  and "hide member lines" hide.
- **A selected node draws no such lines (Michael, Nov 13).** The first version (PR #220) drew a
  selected node's lines end to end too. Michael dropped it after trying it: a node is selected
  all the time (a search, a go to, moving it), so the lines came unasked and often (a hub such
  as Demo.Core.Utils lit 45), while clicking a stub does the job and feels natural. A selected
  node still lights its own lines and its stubs (`LineEmphasis.IsNodeLine`). Don't re-propose
  the node's lines, or lines on hover (dropped by him the same day: selecting is enough).
- Traps the reviews of the stub lines found:
  - Clicking one of a stub's lines moves the selection to that line, which would end the stub's
    reveal: `LineReveal.KeptLine` keeps a selected cousin line between its ends, or it vanishes
    under the click.
  - The click must also survive the repaint: `SelectionService` measures a clicked line
    (`GetBoundingRectangle`) before it clears the old selection and raises no event in
    between. Deselecting first worked in the dev host and failed in the browser app (Wasm
    repaints before the interop call returns, so the line was gone and the click only
    deselected; Michael found it on the PR's test site). The chromium e2e run on the dev host
    cannot see this kind of bug: check selection changes in a published `Dependinator.Wasm`
    too (see the Layout section for how to serve one).
  - One line of 7 links becomes 7 lines of one link, which a minimum link count would drop: the
    revealed lines (`Line.IsRevealed`) skip that filter.
  - No reveal while cycles are shown: the cycle lines are the sibling lines (`CycleService`
    ids), and a split line would lose its cycle colour.
  - `SvgService.CreateModelTile` syncs the lines before it reads the view flags: a Sync can
    create lines, and the rule and path line id sets must already hold them.
  - Usage and inheritance between the same two nodes are two lines (`RepPair.IsInheritance`,
    `LineService.AddDirectLine`), so a node can have two stubs on one side, and a clicked line
    can have a twin. Picking links by direction or by the ends alone drew both: go by the
    stub's own links, and by `IsInheritanceBetween` for the kept line.
  - Beside a selected line only its own lines and its explored stub's may be `IsRevealed` (that
    is what keeps the dim rule to one test): a reveal that flags more lights them.
  - Stubs are drawn after a container's other lines (`NodeLinesSvg.DrawnNodeLines` yields them
    last): a line back to a node on the left starts at its node's right side and runs level
    under the node and the stub into it, and took the stub's click (Demo.Core.CloudSync → Utils
    in the demo). Known and left: a stub therefore also lies over the handles of a selected
    hand-routed line whose point is right at it, and a selected stub's own lines end at its
    anchor and lie over it (a second click on the selected stub can select one of its lines).
- **Exploring a stub's lines (Nov 13, Michael).** Clicking one of a selected stub's lines keeps
  the stub's other lines, so the user goes from line to line and pans and zooms around:
  `SelectionService` marks the stub `Line.IsExplored` when the clicked line is one of its lines
  (a cousin line with only the stub's links; not `IsRevealed`, which holds only the newest
  tile's lines) and keeps it while the next clicked line is one of its lines too;
  `LineEmphasis.FindSelection` hands it on as `RenderSelection.ExploredStub`, and
  `GetLineReveal` asks for `KeptLine` and `Stub` together (the kept line's links go first, so a
  clicked line to a whole container stays whole while the others split). The stub is
  `IsRevealed` too, lit with its lines. Escape first selects the stub again
  (`TrySelectExploredStub`), then clears; any other selection ends the exploring.
- **One line per far container (Nov 13, Michael).** A selected stub's far side opens no container
  that lies outside the tile (`LineReveal.Area`, the tile's canvas area with its margin, set in
  `SvgService.CreateModelTile`) or whose default-size child would be below
  `RepLineService.MinFarEndSize` (20 px) on screen (`IsWorthOpening`): its lines end at that
  container, one line for all of them. So the lines depend on the tile, not just the zoom: the Area
  is part of the reveal. With a stub selected, a new tile, zoom or layout re-walks only the stub's
  own links (`IsEveryStubFarEndUnchanged` against the memo's `StubFarEnds`) and syncs everything
  only when one of their far ends changed: a full sync each time took 40 to 70 ms per tile on the
  dev host for the demo's Utils stub (the browser app is about 10x slower), against 2 to 5 ms. The Area is
  where the tile draws nodes (`RenderContext.IsInTile`), so a container the tile draws has its lines
  split unless its children are tiny, and one up to about a screen outside the view may be split
  too, depending on the tile. The lines' `IsActiveRep` hold the newest tile's state, also while an
  older cached tile is shown (as for tiles at another zoom).
  Still open: a wider touch area for a stub if it is fiddly on the iPad.
- Every change to the focus calls `model.BumpStructureVersion()` (the Sync memo keys on
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

## Go to marks, it does not select (Nov 13 2026, Michael)

- `NavigationService.ShowNodeAsync`/`ShowNodesAsync` (every go to: search, explorer, breadcrumb,
  links, lists, tour, VS Code following the editor) leave the selection alone and mark the nodes
  with `IHighlightService`: `Node.IsHighlighted`, amber corner brackets drawn with the selection
  overlay (`NodeSvg.HighlightSvg`, baked into the tiles, so set and clear raise `ModelChanged`),
  a CSS pulse while `IsPulsing` (the canvas's `dep-highlight-pulsing` class), a single marked
  node's lines lit (`RenderSelection.Highlighted`, beside any selection; several marked nodes,
  such as a picked cycle, only get brackets, or every line of the members would light), and the
  breadcrumb chain (it goes before the selection's; the ids outlive a model load, so it checks
  the node's flag). Selecting on a go to dropped the user's selection and showed handles and a
  toolbar unasked; don't bring it back.
- Cleared on the release of any canvas press (`InteractionService.OnMouseUp`), never on the
  press: an inner member line can be hit only while lit (`LineSvg`), so a repaint between press
  and release (always in Wasm) took a click on the marked node's inner line. Also cleared by any
  key, view history Back/Forward, the next go to and an export. A wheel zoom keeps it. A single
  go to's node that is selected gets no brackets; a group marks every member, selected or not.
  Michael picked the brackets over a glowing halo and a spotlight.
- The motion (Nov 14 2026, Michael): `PanZoomService.PanZoomToAsync` flies along `ZoomPath`
  (van Wijk and Nuij, d3's interpolateZoom) instead of the old zoom out, pan, zoom in steps. The
  path's width is the zoom times `FlightFocusShare` of the screen's shorter side, so the top of
  a long flight shows both ends; `FlightDwell` slows the top, a sine ease softens both ends, and
  the duration is clock-timed from the path length (`FlightBaseSeconds`, `FlightSecondsPerLength`,
  `FlightMaxSeconds`). Michael said to keep the first values and tune them later. Each frame
  awaits at least 1 ms (`ShowFrameAsync`): the Wasm app paints and takes the cancelling wheel
  only then. `ZoomPath` uses `-asinh(b)` for the paper's `ln(sqrt(b²+1)-b)`, which cancels to
  `log(0)` on a big zoom with a small pan.

## Floating toolbars (Nov 13 2026, Michael)

- The node, note and line toolbars are a plain `div.dep-float-toolbar` (not a `MudButtonGroup`,
  whose purple divider between every button Michael found rough): grey icons, purple on hover,
  28 px buttons. Groups are split by `span.dep-toolbar-sep`; a divider with no button before or
  after it (a run of dividers included) is hidden by CSS (`:has`), so a conditional group needs
  no `@if` around its divider. The 36 px height is assumed by `NodeToolbar.ToolbarOffset` and
  `LineToolbar`'s top: change them with the CSS.
- A `MudTooltip` around a `MudMenu` stays over the open menu (the button keeps the pointer and
  the focus): give it `Disabled` while the menu is open (`OpenChanged`) and `ShowOnFocus="false"`,
  and reset the flag when the toolbar is gone (a menu removed while open never reports its close).
- The node menu is the last button ("more", ⋮), References and Dependencies come first.
- Line bends are edited on the line (Nov 13, Michael's pick over the old +/− buttons): edit-mode
  segment handles (`PointerId.FromLineMidPoint`, sub-id `lm{segment}_{percent}`; the percent is
  50, or 25/75 when the label's box covers the middle, since the label layer would take the
  press) become a bend on a drag past the 4 px threshold (`LineEditService.InsertSegmentPoint`,
  one undo step with the drag); a bend released within 6 screen px of straight, or
  double-clicked, is removed; "Straighten line" clears all.
- Line-end tags (`LineEndTags.razor`, Nov 13) replace the go to source/target buttons:
  `SelectionService` places a non-stub line's toolbar and tags from the model geometry
  (`LineOnScreen`: the line in client pixels, clipped to the canvas below `ChromeBottom`), not
  the line's measured box; stubs keep the measured box. A tag goes with `ShowNodeAsync` and
  re-places the selection after. An explorer line to a far end inside another container is not
  drawn once there (its source is not shown at that zoom), so its toolbar goes; known, accepted.

## Rendering performance

- Keep `WheelZoomSpeed` below `TileKey.ZoomFactor` (1.3), or every wheel tick rebuilds all tiles.
- **The view moves by a CSS transform (Nov 14 2026).** Measured: a `viewBox` change repaints the
  whole svg every frame (WebKit: 33-62 ms panning, 73-151 ms zooming at 700-1200 elements), a
  transform on the `<svg>` element 16 ms; a transform on an inner `<g>` is as slow as the viewBox.
  So while `PanZoomService.IsViewMoving` (a step until 150 ms of rest, or a flight's landing)
  `CanvasService.GetView` keeps the drawn svg and moves it (`GestureTransform`), and draws the
  view for real on rest, past the tile's area, past a 2x scale or after a `ModelChanged`. View
  steps raise only `ViewChanged` (canvas, minimap); `UIStateChanged` goes out on the first step
  and on rest: re-rendering the whole UI per step was 7 of the step's 11 ms in Wasm. Traps:
  - A transformed element paints above `position: fixed` elements without a z-index that come
    before it in the page: every fixed overlay needs one (`.dep-float-toolbar` 5, the app bar 1100).
  - The moved svg's own rect and its events' `offsetX/Y` move with it: JS measures the
    `#canvasframe` div instead (`getBoundingRectangle("svgcanvas")` and `mouseEventData`), and
    the e2e `App.Canvas` locator is that frame.
  - Headless Playwright WebKit sends `movementX` 0, so a mouse drag does not pan there: measure
    WebKit panning with the arrow keys. Headless Chromium keeps 60 fps either way and hides the
    repaint cost; compare in WebKit.
- A pinch zooms by the fingers' distance and keeps the point between them under them
  (`PointerEvent.PinchScale`, `PinchFromX/Y`); its moves are batched per frame like the mouse.
- A trackpad pinch is wheel events with ctrlKey, many per gesture: `PointerEvent.WheelZoomFactor`
  zooms it by its deltaY (about 100 per e-fold in Chromium), capped at the notches so Ctrl + a
  mouse wheel is unchanged. A plain two-finger trackpad scroll still zooms a notch per event
  (left as it is: some browsers give a mouse notch a small pixel delta too, which would slow
  those wheels down).
- **A wheel notch glides (Nov 14 2026).** `PanZoomService.Zoom` eases a mouse wheel notch to
  its zoom over about 130 ms (`GlideAsync`: each frame covers `1 - e^(-dt/35 ms)` of the log
  zoom left, around the pointer, landing within 0.5%); a notch during a glide adds to its
  target, and a notch after the landing starts a new glide (the landing frame clears
  `wheelGlideId` before its wait, or a notch in that wait was lost). It is a flight-like loop on
  `goToRequestId`, so any other pan, zoom, go to or wheel event ends it (a sideways scroll too).
  A trackpad pinch (ctrl) zooms at once, from the view as it is. The edit-mode zoom of a selected
  node's content still steps. Unit tests that wheel-zoom must wait for the landing.
- **Tiles drawn ahead (Nov 14 2026).** 300 ms after the canvas last rendered, `Canvas` asks
  `ICanvasService.PrefetchNext` (on the renderer, `InvokeAsync`: the Blazor Server host would
  otherwise sync the lines on a timer thread while components read them) for the tiles one zoom
  level in, then out, around the view's center, one per timer slot (a tile cannot be stopped
  once it is being drawn, 6 to 116 ms in the browser app). `SvgService.PrefetchNext` takes the
  levels from the view's key (`TileKey.AtLevel`), since `From(zoom / 1.3)` rounds a level off
  at a level's own zoom; it draws only ahead of a cached view, holds the state lock across
  drawing and caching, never touches `model.ViewRect`/`Zoom`, caches with
  `TileCache.AddPrefetched` (not the last used tile) and syncs the lines back to the view's
  tile. Drawing a tile syncs the lines to its zoom and area, so `GetTile` also syncs them on a
  cache hit of another tile than the one they follow (`syncedKey`).
- The cloud sync checks the sync state every 5 s while the UI is active (`AppCloudSyncService`,
  `ActiveRefreshInterval`) and compares the model's content hash, signed in or not: hashing the
  demo took 550-700 ms in the browser app, a stop every 5 s while panning (Michael, Nov 14).
  The hash is kept while `IModelService.ContentStamp` is unchanged: save requests and model
  loads (bumped after the change), `StructureVersion` and the path. Like the local save it
  leaves out a lazy layout while rendering (the next save shows it); with the layout generation
  in the stamp, zooming into new containers hashed again at each check. A push caches the hash
  of what it pushed, or the check compared an older hash with the new baseline and pushed again
  and again (found in review).
- Pausing every few seconds while panning is not the view save: it writes only the small view
  record (`TriggerViewSave`, at most every 10 s).
- `CreateModelTile` logs `Tile ((x,y,z)): N chars in Mms` to the app log (`~/Dependinator.log`):
  use it for before/after comparisons. Deferred until measured: a selection overlay layer,
  WebKit-gating `refreshSvgPaintServers`, a pan/zoom save debounce, description-wrap caching.
- WebKit loses `<use>` gradient fills when Blazor replaces the diagram subtree; `refreshSvgPaintServers`
  (jsInterop.js, WebKit only) re-appends `<defs>` when the canvas content changed by reference. No e2e
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
  - Drawn smaller than node texts (`LineSvg.LabelFontSize`, `LabelMaxTextZoom`), the label cut
    to one row and the description wrapped to three (`SvgText.GetUserTextLines` keeps the user's
    line breaks); the hover card shows them in full (Michael, Nov 13).
  - `Line.LabelPosition` is a fraction of the line's length (not a point), so the label stays
    on its line when nodes move or the line is rerouted; saved only when moved
    (`LineDto.LabelPosition`, also in `PendingLineLabel`). The label's transparent hit rect
    (`LineSvg.LabelHitSvg`) has the element id `PointerId.LineLabelElementId`, which `Parse`
    reads back as the line with `IsLineLabel` (equal to the line's id, so selecting, menus
    and hover cards treat it as the line); only the edit-mode drag (`InteractionService`,
    `LineEditService.MoveLabel`, `LineLabelPositionCommand`) looks at `IsLineLabel`. Its group
    carries the line's element id (for the hover card) but no `data-name`, so the e2e
    `App.Line` locator still matches one element. Labels lie above the nodes, a selected line's
    bend-point handles with the line below them: a handle under a label gets a transparent copy
    of its hit circle above the rect (`LineSvg.LabelHandleClass`, same id). Letting presses
    through the whole rect instead broke the double-click on a bent line's label: the first
    click selects the line, so the second missed it (seen only in the published Wasm app).
- Alias edits bump `StructureVersion`, because panels cache their rows by it. Tooltips stay
  `"{LongName} ({Alias})"`, because `App.Node(label)` in the e2e tests matches the long-name prefix.
- `HideNodesCommand` needs its own `MergeKey`: same-type commands within 500 ms merge, which turned
  hide+show into a no-op undo. The line rule lives in `LineVisibility.Update`.
- View › Rows is a view option (`RowFilter`), not a model edit: rows are skipped in
  `NodeTreeSvg.IsRendered` and in the minimap, and `IsLineFilteredOut` drops lines to a hidden row.

## Workspace models (Nov 2026)

- A host with a workspace (`IModelListService.HasWorkspace`: VS Code, the dev host and so the e2e
  test host) keeps its solutions and Bicep files apart from every other model. The Models menu
  (`App/ModelMenuItems.razor`) lists only `GetWorkspaceModels()`, the Models dialog ("Other
  Models …") opens the rest, and an open model that is none of the workspace's is a visit
  (`GetVisitedModel`): a "Viewing" row in the menu, and the breadcrumb bar stays up with the
  model's kind and a button back to `HomePath`. The browser app has no workspace and keeps the
  one flat list. Decided with Michael (Nov 12): keep the submenu, don't replace it by the dialog.
- `HomePath` is what this window opened last, kept in memory: the recent paths in the config are
  shared by every window (and by parallel e2e tests), so they only decide after a restart.
- In test mode the embedded demo stands in for the working solution as the first workspace
  model (`workspacePaths`), but `LocalPaths` is unchanged: `ModelService.RefreshAsync` only
  re-parses a local path, and the demo must stay out of that.
- The menu's sections (decided with Michael, Nov 13): "Whole workspace" with the workspace
  model and "Choose Sources …" right below it (only while that model is open), then "Single
  sources" with the model of each source. Without a workspace model (fewer than two sources)
  there is the one list "This workspace". A submenu for the single models was the alternative.
- A component with no parameters inside the app bar's menu (`ModelMenuItems`) is not re-rendered
  with its parent: it subscribes to `UIStateChanged` and the cloud sync's `Changed` itself.
- The model of the whole workspace (`WorkspaceModelPath`, `docs/notes/WORKSPACE-MODEL.md`) is
  first in `GetWorkspaceModels()` but last in `LocalPaths`. VS Code starts on the model of the
  workspace used last (`WorkspaceStartupPath`: the first recent path of this workspace, since
  the recent paths are shared by all workspaces), else on the whole workspace (decided with
  Michael, Nov 13, phase 4), so VS Code does not seed the recent paths with the first source.
  `WorkspaceModelPath` is its identity (non-null with a workspace folder, compare with `ModelListService.IsSamePath`, never `==`); it is
  listed only with two or more sources, but while there is a source `IsWorkspacePath` and
  `IsLocalPath` hold for it either way, so a model that is open keeps refreshing when a source
  is removed. With no source left it is a visit. A model path is not re-spelled on the way in: saved models and the cloud pull go
  by the exact path (tried, and it made a pulled copy unreadable).
- A menu item that opens a model must not await the load (`ModelMenuItems.OpenAsync` runs it
  in the background): MudBlazor keeps the menu open until the click handler returns, so the
  menu stayed up behind the sources dialog and through the whole parse.
- In a cloud session the dev host lists `Dependinator.sln` twice among the workspace's sources
  (the `/workspaces/Dependinator` symlink and the real path): an artifact of the session, not
  a bug in the list.

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
