# What changed in 2026

New features and larger changes from January to mid-October 2026. The list is built from the git
history and `CHANGELOG.md`, which only starts at 0.8.9 in July. Dates are when the work landed on
`dev`. Bug fixes, package bumps and e2e flake fixes are left out unless they changed how the
project works.

## The year in short

- **Jan**: the VS Code extension takes shape. The Wasm UI runs in a webview and talks JSON-RPC to
  a new LSP server that does the parsing. Parsing moves out of the UI into a new Core library.
  First VSIX release workflow.
- **Feb**: a Roslyn source parser replaces the reflection parser. VS Code can jump between diagram
  and code. Snap to grid and line segments arrive, and so does the cloud sync API.
- **Mar**: cloud sync gets conflicts and auto sync and switches to Clerk sign-in. Project renames
  (`Dependinator.UI`, Example → Demo).
- **Apr**: edit mode, Razor/source-generated types, parallel parsing.
- **May**: package upgrades (MudBlazor 9.5).
- **Jun**: search dialog, Playwright e2e tests and CI, `src/` + `tests/` layout, architecture
  tests, a lighter look with new icons.
- **Jul**: design features (manual nodes, links, notes, icons, colours, cloud icons, empty models,
  image export), descriptions, inheritance lines, line aggregation, a source watcher, Marketplace
  publishing with auto-release on push to `main`, rendering optimisation.
- **Aug**: the extension opens the solution, and the About box shows the extension version.
- **Sep**: `Result`/`Result<T>` as C# 15 unions on the .NET 11 SDK, funnel line aggregation,
  optional test projects. Then the UX review: shortcuts, dark theme, history, breadcrumb, line
  filters, legend, cycles, Manage Models, VS Code commands.
- **Oct**: the rest of the UX review (path finder, tour, share links, architecture rules, minimap,
  group and rubber-band selection), new line and container visuals, a release flow through `dev`,
  a README and community-files rework, the Bicep parser, and zones, line labels and aliases.

Extension versions: 0.5.x (Jan–Mar), 0.8.x (Jul–Oct 6), then one minor bump per release: 0.9.0,
0.10.0, 0.80.0, 0.81.0 (Oct 8).

## VS Code extension

- **New host (Jan).** The extension shows the Blazor WebAssembly UI in a webview and starts a
  self-contained LSP server (`Dependinator.Lsp`). The two talk JSON-RPC with chunked messages
  (compressed from Feb), a ready handshake and progress reporting. The same UI still runs in the
  browser and in the Blazor Server host.
- **Diagram ⇄ code (Jan–Feb).** An editor toolbar button opens the diagram at the node for the
  current file and line. Nodes and the Dependencies dialog open the source in an editor beside the
  diagram. Node file spans are cached, falling back to the parent's span.
- **Packaging (Jan).** First VSIX release workflow, `build-ext` / `install-ext` scripts and
  version bumping.
- **Live models (Jul).** A source change watcher re-parses when files change, and the diagram
  goes to the current file's node on first open. From Aug the extension opens the workspace
  solution.
- **VS Code Marketplace (Jul 14).** Published, and auto-published on every push to `main`, with
  an extension README, a demo gif and a publisher logo.
- **Commands and status (Sep 30).** Reveal Current File in Diagram (Ctrl+Alt+D, also in the
  editor context menu), Search Nodes and Re-parse Solution. A `dependinator.followActiveEditor`
  setting makes the diagram follow the active editor, except editors it opened itself. A
  status-bar item spins while parsing.
- **Bicep (Oct 9).** `.bicep` entry points are listed as workspace models and count as model
  sources.

## Parsing

- **Parsing moved to Core (Jan).** A new `Dependinator.Core` library (parsing, models, utilities)
  lets parsing run in the LSP process and in the Blazor Server host. The parser returns a flat
  list of items.
- **Roslyn source parser (Feb).** Parses `.sln` sources with Roslyn instead of compiled
  assemblies. It covers types, members and base types; links from fields, method parameters and
  method bodies; an Externals node for referenced code; type descriptions from XML doc comments;
  and file spans for navigation. It moved into its own `Dependinator.Roslyn` project on Feb 24.
- **Wider C# coverage (Mar–Jun).** Generic call parameters and calls inside lambdas (Mar).
  Source-generated types such as Razor components, which can be opened in the editor (Apr).
  Property bodies and static property references (Jun). Parsing became parallel (Apr).
- **Descriptions (Jul 1).** Namespaces (from namespace comments), the solution (from
  `README.md`), assemblies (`AssemblyDescription`) and Externals get descriptions, and nodes show
  them.
- **Simpler assembly nodes (Jul).** An assembly with a single namespace shows that namespace's
  nodes directly (pass-through). Assembly names read `name.sub (dll)`, and nodes get a dll/exe
  property.
- **Inheritance dependencies (Jul 19)** are their own line kind.
- **Reflection parser shelved (Jul 23).** The old Mono.Cecil/reflection parser moved to the
  inactive `Dependinator.Reflection` project.
- **Parse options (Sep).** Test projects can be included. A failed parse shows an error, which
  from Oct offers Retry and Include test projects.
- **Parser dispatch (Oct 9).** `ParserService` hands a path to the first `ISourceParser` whose
  `CanParse(path)` matches. Infrastructure node types and parser-supplied node icons were added.
- **Bicep parser (Oct 9–11).** `Dependinator.Bicep`, built on `Azure.Bicep.Core`, turns an Azure
  deployment (an entry-point `.bicep` plus its local modules) into a model:
  - resources get Azure icons and are nested under their scope target;
  - modules take the icon of their principal resource;
  - auxiliary resources are drawn as rows;
  - configuration lines are hidden by default.

  It ships with the `examples/bicep` "webshop" deployment and a Bicep demo model.

## Viewing, navigation and analysis

- **Search (Jun).** A fuzzy search dialog (Ctrl+F) with go-to-node. Since Oct the node whose name
  exactly matches the query ranks first.
- **Look and feel (Jun–Jul).** Lighter toolbars and theme, a new app icon, coloured node icons per
  kind (class, interface, enum, record), descriptions on nodes, a first-time demo message and a
  help page.
- **Line aggregation (Jul, Sep).** In Jul, lines between cousins were folded into their parents,
  lines and sources could be split, and a line stayed visible while either end was visible. On
  Sep 14 this was reworked: funnel aggregation became the default, with line-split depth and
  explorer focus as opt-ins. Direct lines are dotted.
- **Dependencies explorer.**
  - Expand all (Feb).
  - Reworked UI with link counts (Jul 9).
  - Minimises on an outside click (Sep).
  - Indirect rows with hop counts (Oct 3).
  - Go-to-source/target on lines, placed below the app bar (Oct 7).
- **Performance.** Smoother auto zoom (Mar). Rendering and tile-cache optimisations with
  size-based eviction (Jul 24–25).
- **UX review (Sep 30 – Oct 5).** Details in [UX-REVIEW-SUMMARY.md](UX-REVIEW-SUMMARY.md):
  - keyboard shortcuts (undo/redo, Escape, zoom, pan, Home to fit, Ctrl+F, Alt+Up), a dark theme
    (System/Light/Dark), a contextual right-click menu and mode banners;
  - view history (Back/Forward) kept apart from undo, a breadcrumb, Zoom Out One Level and Fit;
  - dimming of unrelated lines, line filters (externals, inheritance, members, minimum link
    count) and a legend;
  - cycle detection with a panel and red lines;
  - a path finder ("why does A depend on B");
  - a first-run tour in place of the welcome dialog;
  - share links to a node or to the current view;
  - architecture rules ("X must not use Y"), with violations drawn orange;
  - a minimap with a draggable viewport frame;
  - group selection (Shift+click) and rubber-band selection (Shift+drag).
- **Line and container visuals (Oct 6).** All lines are drawn at one width, and the selection's
  lines widen by link count. Container backgrounds now show in light mode and fade out while
  zooming in. Lines to hidden nodes are dash-dotted. Line-split and zoom-to-node moved off the
  node toolbar.
- **Settings.** Invert the mouse-wheel zoom direction (Sep).

## Editing and design

- **Layout editing (Feb).**
  - Snap to grid when moving and resizing.
  - Reset the layout of children or descendants.
  - Grow and shrink nodes.
  - Line segments, with points added or removed where clicked.
  - Nodes drawn over lines.

  The initial layout was improved in Jul.
- **Hidden nodes (Mar).** A menu toggle shows or hides hidden nodes. Hiding became undoable on
  Oct 11.
- **Edit mode (Apr).** Edit buttons show only in edit mode. Since Sep 30 the mode is persisted,
  off by default, and required for every model change.
- **Design features (Jul 5–18).**
  - Node icons and icon colours, container background colours, and Azure/AWS/Google cloud icons.
  - Manually added nodes (rename, cascade delete).
  - Notes with a notes sidebar, and a right-click menu.
  - New empty models.
  - Links created by hovering and dragging, nodes created by dragging out a line, and manual
    links can be deleted.
  - Models can be deleted, and the diagram can be exported as an image.
- **Design layer (Oct 11).**
  - Zones: labelled background areas.
  - Line labels, with a description under the label.
  - Display aliases, shown in labels and search.
  - A View › Rows filter that leaves out whole kinds of rows.

## Models, cloud sync and sign-in

- **Cloud sync API (Feb 27).** A new Azure Functions `Api` project, deployed with the Static Web
  App, stores and lists models in blob storage. It came with a sync UI, persisted sync state and
  API tests.
- **Sync features (Mar).** Conflict handling, auto sync, readable blob names, a cloud models
  submenu and recent list, and sync from the Blazor Server host. VS Code syncs through the LSP,
  either to production or to local Azurite.
- **Clerk sign-in (Mar 21).** All hosts sign in with Clerk, and the API validates Clerk JWTs via
  JWKS. VS Code signs in through a local Clerk page and keeps the token in secrets. Tokens last
  180 days (Jun), and auto login got faster (Jul).
- **"Device sync" (Jul).** Login/Logout became Enable/Disable device sync, the texts say device
  sync, and the user storage key is hashed again.
- **Model handling.**
  - Saves are debounced and model locking lives in `ModelMgr` (Mar).
  - New empty model, delete model and reload (Jul).
  - Manage Models dialog with workspace, recent, cloud and design sections (Sep 30).
  - A cloud status popover, parse progress showing the file and elapsed time, and a summary
    after each refresh (Sep 30).
- **Demo models.** Example was renamed Demo (Mar). The demo models are generated by the
  `DemoGen` tool (`./scripts/gen-demo`, Jul 13), regenerated from the solution in Oct, and joined
  by a Bicep demo.

## Code base and architecture

- **Project layout.**
  - `Dependinator.Core` (Jan).
  - Dotted project and namespace names (Feb 26).
  - `Dependinator` renamed to `Dependinator.UI` (Mar 27).
  - Sources in `src/` (Jun 22) and tests in `tests/` (Jun 27), with solution folders.
  - Scripts in `scripts/` (Jul 24).
  - New parser projects: `Dependinator.Roslyn` (Feb), `Dependinator.Reflection` (Jul) and
    `Dependinator.Bicep` (Oct).
- **Namespace refactors.** Modeling, Diagrams, Tiles and Commands namespaces (Mar). In Jul 10–12
  every project was cleaned up namespace by namespace: interaction services split out, `async
  void` removed, SVG/CSS numbers formatted culture-invariant.
- **Architecture tests (Jun 22).** NetArchTest enforces `Hosts → UI → Core → Shared`.
- **Result unions (Sep 12).** On the .NET 11 preview SDK, `Result`/`Result<T>` became C# 15 union
  types and replaced `R`/`R<T>` everywhere, with CS8509 as an error. The GA follow-up is in
  [NET11-UPGRADE.md](NET11-UPGRADE.md).
- **Packages.** MudBlazor 9.5 (May). `./scripts/updatepackages` also handles npm packages (Jun).

## Testing, CI/CD and releases

- **Playwright e2e tests (Jun).** `Dependinator.E2E.Tests` runs against the live app. Support
  added for it:
  - data-testids and an app-ready signal;
  - a fixed demo solution;
  - Azurite seeding and sign-in for sync tests;
  - traces, viewed with `./scripts/trace`;
  - `./scripts/e2e` with a browser choice, and `./scripts/test`.

  The suite now has 74 tests. Sep–Oct went into stabilising it: capped parallelism,
  verify-and-retry page objects and a 15 s expect timeout. There are about 760 unit tests across
  the other test projects.
- **CI (Jun 20).** Unit-test and e2e workflows run on feature-branch pushes. CI/CD runs them
  before deploying; the full gate covers all browsers plus sync.
- **Release automation (Jul 21–23).** `CHANGELOG.md` is generated from commit messages, a push to
  `main` creates a release, and the CI/CD and extension workflows were unified.
- **Release flow (Oct 6).** Dependabot PRs target `dev`, grouped. `./scripts/release` merges `dev`
  into `main` with changelog bullets. Each release bumps the minor version.
- **Security (Oct 8).** CodeQL alerts fixed and the workflow's `GITHUB_TOKEN` permissions limited.
- **Demo recording (Jul).** `./scripts/record-demo` re-records the README demo gif.

## Docs and presentation

- Extension and repo READMEs (Jun–Jul), a help page (Jun 30; `/help` route in Oct), and an About
  box that explains dependinator.com and shows the versions.
- A Marketplace listing with the demo gif (Jul).
- Oct 8: the README was reworked around users, with a contributing guide, issue templates,
  community files, GitHub Discussions for questions and social preview tags.
- Bicep in the help page and READMEs with a screenshot, and architecture diagrams of the webshop
  example (Oct 9–11).
- Internal notes moved to `docs/notes/` (Oct 8).
