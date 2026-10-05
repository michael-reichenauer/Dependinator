# UX review: what was done, what was left

Branch `mr/user-exp`, 30 Sep – 5 Oct 2026. The review itself (eight problems, quick fixes,
missing features, six phases) lives in the plan file used during the work; this is the record of
what came out of it. Everything below is committed on the branch; nothing was pushed.

Test status at the end: build green, 462 UI unit tests, 56 chromium e2e tests (7 skipped sync
tests run only with `./scripts/e2e -s`). The full gate (`./scripts/e2e -a -s`) was not run.

## Done

### Phase 1 – consistency and quick wins (33d752ab)

- Keyboard shortcuts (`KeyboardService`, `jsInterop.js listenToKeyboard`): Ctrl+Z/Y, Escape
  deselects/cancels, +/- zoom, arrows pan, Home fits, Ctrl+F search, Alt+Up zoom out one level.
- Edit mode is persisted (`Config.IsEditingEnabled`), **off by default in every host**, and gates
  every model change consistently (menu, context menu, double-click, line points, toolbar).
- Undo/Redo toolbar buttons disable properly and stay visible.
- Checkbox-style menu toggles; theme submenu (System/Light/Dark) with dark palette, persisted,
  tile cache cleared on switch, body background set before Blazor boots, VS Code theme followed.
- Mode banner while placing a note/node, selecting an export area, dragging a link, arranging
  inside a node; Escape cancels.
- Contextual right-click menu (node / line / canvas).
- Node toolbar palette with separate **Icon** and **Container** colour rows; outlined
  Interface icon.
- Explorer: Close keeps the lines, "keep lines" pin, auto-minimise only below 960 px.
- "Show source" hidden outside VS Code; delete snackbars with Undo; help page rewritten.
- Pre-existing bug fixed: reselecting a node at the same spot did nothing.

### Phase 2 – orientation (ecee2f8c, dade02f0)

- View history separate from undo: Back/Forward toolbar buttons, Alt+←/→, Backspace /
  Shift+Backspace; only jumps create entries (search, explorer, breadcrumb, double-click, fit).
- Breadcrumb bar: the selected node's chain, or the container under the view centre.
- Alt+Up / context menu "Zoom Out One Level"; Fit button in the toolbar; floating toolbars
  clamped below the app chrome.

### Phase 3 – lines and analysis (76528919, 55eeee88, 3782cf8b, 87079b06)

- Dim unrelated lines while something is selected (View toggle, persisted; hover restores).
- Line filters (externals, inheritance, member lines, minimum link count), persisted.
- Legend (View › Show Legend) and plainer wording for the line-detail controls.
- Cycle detection (Tarjan per container) with a panel and red lines.

### Phase 4 – models, parsing and sync (652a34d3, 7f9eebaf, da7cce34)

- Cloud button is a status popover with explicit actions; background pull reports what changed;
  richer conflict dialog.
- Manage Models dialog with sections (workspace / recent / cloud / design models).
- Parse progress shows the file and elapsed time, "continue in background"; refresh summary
  ("Model refreshed: +3 types …"); error snackbars carry actions (Retry, Include test projects);
  stale design-model format is reported instead of silently recreated.

### Phase 5 – VS Code integration (04caf11d, 7ba25d4e)

- Commands: Reveal Current File in Diagram (Ctrl+Alt+D, editor context menu), Search Nodes,
  Re-parse Solution. `dependinator.followActiveEditor` setting (default on). Status bar item
  that spins while parsing. Title-bar button scoped to C#/solution files. Cloud-sync settings
  marked as developer settings. **Not tried in a real VS Code session** – verified by
  compilation and unit tests only.

### Phase 6 – bigger bets

- **Path finder** (3fa10b77): View › Find Path, two pickers, shortest chains listed, the chosen
  chain drawn cyan with everything else faded. `PathFinderService`, `PathPanel.razor`.
- **Explorer "include indirect"** (9c5a71b0): the `»` button lists nodes reached through other
  nodes, dimmed with hop counts, merged into the same containers; a chain button opens the path
  panel. This replaced the planned impact view. Same commit: a usage line and an inheritance
  line to the same container are now one explorer row (the duplicate "Dependinator.Core" rows).
- **First-run tour** (63042eaa): replaces the welcome dialog; four auto-advancing steps; shown
  once per browser/host (`Config.IsCoachSeen`); Help › Show Tips replays it; never auto-starts
  in test mode. **Existing users will see it once after this update** because the flag is new.
- **Share links** (8c3764af): `?m=<model key>&n=<node name>` or `&v=<x>,<y>,<zoom>`; Copy
  Link to Node (node menu, right-click), Link to Current View (Export menu, right-click on
  canvas). Opening a link loads the demo, a local model or a cloud model of the signed-in
  account. From VS Code the link points at dependinator.com and needs the model synced. Public
  links for other accounts need a share-token API (not done).
- **Architecture rules** (9a649d34): View › Show Architecture Rules; "X must not use Y" by
  picking two nodes; violations per class pair, drawn orange; rules saved in the model
  (`ModelDto.Rules`, defaulted so older files load) and undoable. Forbidden pairs only, no
  patterns or wildcards.
- **Minimap** (a437dc6a, de7c26f4, 378458f2, 9cceca2b): View › Show Minimap, persisted; bottom
  right; click/drag pans; hidden below 700 px width. Follows the zoom: whole model while the
  view is large, otherwise a window around the view with the diagram at most 8× more zoomed
  than the map, lazy scrolling (camera-like), boxes drawn as deep as they are visible.
- **Group selection** (ab8a697c): Shift/Ctrl+click adds nodes; "N selected" badge; drag moves
  all; hide, size, colour, delete apply to all as one undo step; dimming respects the group.
- **Rubber-band selection** (e9ba1248): Shift+drag draws the export's rectangle and selects the
  nodes fully inside it (open containers are looked into, closed ones taken whole).

Also: CLAUDE.md got a "Committing" section (a16d283a); analysis panels (cycles, rules, path)
share the top-right corner and close each other.

## Things likely to need adjustment

| Where | What | Why it may need a change |
| --- | --- | --- |
| `MinimapGeometry.cs` | `MaxZoomRatio = 8`, `FollowMargin = 0.15` | Tuned on the demo only; try on a large solution. 12 was tried and reverted. |
| `Minimap.razor` | `MaxBoxes = 600`, `MinOpenWidth/Height`, label threshold 56 px | Performance and clutter on big models. |
| `CoachService.cs` | Step texts, `ZoomStepFactor = 1.4`, `OwnModelsHint` for the web host | Wording; the web-host hint sends people to the VS Code extension first. |
| `ShareLinkService.cs` | `WebAppUrl`, `CloudReadyTimeout = 10 s`, link format | A dev/staging web URL is not supported; the format is public once links are shared. |
| `RuleService.cs` / `RulesPanel.razor` | Violations listed per class pair, short names | Big rules produce long lists (191 in the demo); no grouping by namespace yet. |
| `PathFinderService.cs` | `MaxPaths = 10` | Only equally short chains are listed. |
| `DColors.cs` | `PathLine` (cyan), `RuleLine` (orange), `CycleLine` (red) | Check on real dark/light themes. |
| `ViewOptions.cs` / `Config` | Dim unrelated lines **on** by default, edit mode **off** by default | Deliberate defaults from the review; easy to flip. |
| `DependenciesService.SetIncludeIndirect` | Rebuilds the tree, folding expanded rows | Could preserve expansion state. |
| VS Code `extension.ts` | 300 ms follow debounce, `ctrl+alt+d` binding | Not tried in VS Code; the keybinding may clash with user bindings. |
| Explorer indirect hop counts | Counted on the type-level graph (members fold into their type) | Namespace- or project-level hops are not shown. |

## Parked by you

- **Parse diff** (new/removed nodes and links after a refresh, snapshots) – value unclear yet.
- **Pinned containers** (keep a container open/closed regardless of zoom) – breaks the zoom-only
  rendering rule; highest risk of the list.

## From the plan but not done

Orientation and lines

- Sticky container labels (the "map" idiom: pin a large container's name to the visible area).
- Node toolbar regrouped into a primary row plus overflow with captions.
- Hover highlight of a line's whole path and both endpoints (only the dim/undim on hover exists).
- Fan-in / fan-out badges on nodes.
- Explorer as a docked side panel on desktop and bottom sheet on phones; "focus this node"
  (re-root) per row and a follow-selection pin.

Parsing and models

- Parse progress with phases and a Cancel button (needs a cancellation token through the RPC).
- Whole-diagram layout reset (View › Reset Layout) and per-node reset through the undo stack.
- `LayoutDensity` (Balanced/Compact/Spacious) exposed under Settings.
- Zoom-out cap derived from model bounds (`PanZoomService.MaxZoom` is still a fixed 10).
- Surfacing the remaining silent failures: duplicate note id, swallowed file-write errors,
  language server not started.

VS Code

- Hover / CodeLens "N references · M dependencies · Show in Dependinator" (needs an LSP query).
- `.csproj`, `.slnx` and folder workspaces; picking a solution when several exist
  (`WorkspaceFileService` takes the first).

Smaller items

- Distinct silhouettes for class / record / struct icons (only the interface icon changed).
- Extension CHANGELOG regenerated from the root changelog; wording aligned across READMEs.
- Public share links (share-token endpoint in the Functions API).
- Multi-select for lines; icon selection for a group (the dialog is bound to one node).
- Re-record `src/DependinatorVsCode/resources/demo.gif` (`./scripts/record-demo`): the hero flow
  now has the explorer's indirect rows and the path finder.

## Test notes

- New e2e suites: Theme, Keyboard, ViewHistory, Breadcrumb, LineDim, LineFilter, Legend, Cycles,
  ModelsDialog, PathFinder, Coach, ShareLink, Rules, Minimap, MultiSelect.
- Flakes seen and worked around (details in the memory notes of the Claude session): the
  Settings submenu closing before its assertion; the tour's last click swallowed by the search
  dialog's fading overlay; Escape swallowed by a closing node menu.
- Demo-model facts the tests rely on: `Demo.UI.Main` reaches `Demo.Core.Shared.ModelPaths` in
  three hops (Main → Canvas → AppBar → ModelPaths) and has no direct Core dependency;
  `Demo.Roslyn.Parsing.SourceParser` has 50 links into Core (the duplicate-row case); the
  "Demo.UI" search query lands on `Demo.UI.Main._isDarkMode`.
- `./scripts/e2e` refuses to run while `./scripts/watch` holds port 5000; the session stopped
  the watch several times for test runs.
