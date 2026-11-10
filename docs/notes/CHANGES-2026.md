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
  group and rubber-band selection), new line and container visuals, a release flow through `dev`
  and then manual previews and releases, a README and community-files rework, the Bicep parser, zones, line labels and aliases,
  node sizes by code and centrality in the automatic layout, accepted cycles, AI prompts and
  Claude's suggestions for cycles and broken rules, About Node with a question for an AI, comments improved by Claude Code, an MCP server for AI agents, AI-suggested rules and questions about the model, one default AI for all of them, C# member naming fixes, and a problem indicator with node badges.
- **Nov**: a Settings dialog, soft lines, a calmer dark theme and light by default, Copilot as a
  third AI provider in VS Code (answers, model lookups, fixes in Copilot Chat), one fix button,
  "Ask how to break it" for accepted cycles, an AI-planned tour of the codebase, AI-suggested
  line labels, Suggest rules that remembers what was dismissed, with a reason on every rule,
  Fix later for rule violations, new kinds of rule: "only X may use Y", layers, "X is
  independent" and "no cycles inside X", and member cards: types show their members like a class
  box, with a summary card on the way in, and styled hover cards for nodes and lines.

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
  solution. Since Oct 23, with `dependinator.autoRefresh.enabled` off, a
  change only puts a dot on the Re-parse button until the next parse.
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

- **C# member fixes (Oct 21).** The Roslyn parser names and types members as they are written:
  - constructors are constructor nodes with their own icon, and a static constructor has a node
    of its own;
  - a C# 14 extension block is a node named by its receiver, `extension(NodeType)`, with its
    members inside under their own names (dots in brackets no longer split a node name);
  - conversion operators are named by their target type alone (`implicitoperatorResult`), so its
    namespace no longer makes up nodes inside the type (qualified when two targets share a name);
  - links to a use of a generic (`Result<int>`) or to an extension method call, also the static
    form `Ext.Twice(x)` of a block member, target the declared node (`Result<T>`,
    `Ancestors(Node)`) rather than a made-up one;
  - a namespace with a comment above its declaration in more than one file shows all of them,
    each after its file and line, so a note about one file that reads as the namespace's
    description can be seen and moved. The same comment is shown once, a file header with a
    blank line below it (a license) is none, and a removed comment clears the description;
  - a cached model's nodes move to their new parents when it is refreshed, and a stray bracket
    in a design node's name ("Latency < 50ms") no longer joins the parts after it.

## Viewing, navigation and analysis

- **Search (Jun).** A fuzzy search dialog (Ctrl+F) with go-to-node. Since Oct the node whose name
  exactly matches the query ranks first.
- **Look and feel (Jun–Jul).** Lighter toolbars and theme, a new app icon, coloured node icons per
  kind (class, interface, enum, record), descriptions on nodes, a first-time demo message and a
  help page.
- **Softer dark theme (Nov 1).** A very dark grey canvas (#101113) instead of near black,
  pastel container frames instead of full-strength colors (the red frame read as an error), and
  container fills a small step lighter than the canvas instead of near-black stains; off-white
  text. Lighter greys were tried and looked misty.
- **Light theme by default (Nov 1).** View › Theme starts on Light instead of System (light
  still looks better, and is what most users expect); System and Dark stay as options. A stored
  Light or Dark choice is kept; the old stored System counts as unset.
- **Settings dialog (Nov 1).** Menu › Settings … opens one dialog for the preferences set once:
  Appearance (theme, and layout density, which had no UI before), Diagram (dim unrelated lines,
  line widths by link count, problem and hidden node badges), Mouse (invert scroll zoom), This
  model (include test projects) and AI (the former Settings › AI dialog). They left the View and
  Settings menus, which keep the toggles flipped many times a session (edit mode, panels, the
  Lines and Rows filters). An AI menu's "AI settings …" opens the dialog at its AI section.
- **Soft lines (Nov 1).** Many lines made busy diagrams overwhelming: resting lines are now drawn
  at 40%, the selected node's lines at full strength and the unrelated ones at 10%; cycle, rule,
  path and explorer lines stay at full strength. Resting the mouse on a node icon or member row
  lights its lines the same way (a hover preview, CSS only, no redraw). Settings › Soft lines
  turns it off. Tried and dropped: opacity by link count (the long crossing lines are the heavy
  ones) and fading only lines that leave their container.
  Since Nov 4 a selected container lights only the lines leading out of it; every line inside it
  stays dimmed until a child is selected or hovered.
- **Hover cards for nodes and lines (Nov 6).** The plain browser tooltips were replaced by a
  styled card in the app's theme: a node's kind and container, description, how many nodes it
  uses and is used by (a member counts as its type), its members and its problems (compile errors,
  cycles, broken rules); a line's ends, link count, first three links and whether it is part of a
  cycle or breaks a rule. The tiles no longer carry any tooltip text (the SVG `<title>`s are gone,
  `data-name` stays for the e2e locators): the card is built only when the mouse rests on an item
  (`TooltipService`, `initTooltips` in jsInterop.js). Chosen over a larger card with top
  dependencies and the source file.
  Nov 10: the card reads like code. "Interface in Namespace" instead of a dot, a member as it is
  declared (`Task<Rect?> SelectAreaAsync(AreaSelectionPurpose purpose)`, the name in bold, wrapping
  between parameters; `param name type` in Bicep), code comments flow instead of keeping the source
  file's line breaks, and an open container larger than 400 px on screen gets no card (`data-open`),
  since the user is then looking at its children; resting on its name, or on an open type card's
  header (`data-tip-label`, `data-header-height`), still shows it.
- **Breadcrumb only for a selected node (Nov 1).** The bar under the app bar shows only while a
  node is selected, to keep the diagram clear; the "you are here" chain under the view center
  still drives Escape and the context menu, but is no longer shown on its own.
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
  node toolbar. Since Oct 23 the link-count widths are off by default and turned on with
  View › Line Widths by Link Count (explorer lines included): a selected node's thick lines
  looked heavy when nobody asked.
- **Accepted cycles (Oct 19).** A cycle that is intended can be accepted with a reason. It moves
  to a collapsed Accepted section of the cycles panel and its lines are no longer drawn red. The
  acceptance is saved with the model and can be undone. The cycle is reported again if a node
  joins it. An acceptance whose cycle is gone is kept, marked "no longer a cycle", until the user
  removes it.
- **AI prompt for a cycle (Oct 19).** A copy button in each cycle's review copies a prompt that asks an AI chat
  whether the cycle is intended (with a reason to accept it with) or should be broken, and how. It
  lists the members with their descriptions and the links in each direction, grouped per member or,
  when there are many, per type. In the web host and VS Code it also shows the source of the
  directions made by only one or two types, the ones most likely to be broken or explained. The
  first step towards suggesting reasons with Claude directly.
- **Ask Claude (Oct 20).** Each cycle has one Review button; its dialog copies the AI prompt and
  asks Claude for a verdict on the cycle with it: an accepting verdict fills in the reason, a
  breaking one shows the change to make, and nothing is saved until the user decides. "Ask again"
  keeps every answer, so the user can pick one when they differ (a borderline cycle), and accepting
  anyway keeps the suggested fix in the reason. The answer of another AI chat to the copied prompt
  can be pasted back into the review and is read like Claude's, so the whole review works without an
  API key, also in the browser app; when asking Claude fails (no key, a key not accepted, no credits
  left, which now says so plainly), the review offers that route instead. The cycles panel can be
  folded down to its title bar,
  like the dependencies explorer (and is, on a narrow screen, by a click in the diagram). It uses the user's own Anthropic API key
  (VS Code secret storage via a new command, or `ANTHROPIC_API_KEY` for the web host). In VS Code
  the key is asked for on first use, and again when Claude does not accept it, with the dialog
  pointing at the input box at the top of the window, since the user is watching the dialog. When
  Claude suggests breaking the cycle, the main button copies a fix prompt for Claude Code (or another
  AI that can change the code) instead, and accepting stays possible as "Accept anyway". "Fix later"
  moves such a cycle to a To fix section with the planned change (saved with the model like an
  acceptance); its lines stay red, and its fix prompt can be copied from there. A refresh message
  now also tells what changed about cycles ("1 cycle to fix is gone, 1 new cycle"), and accepted or
  to-fix entries whose cycle is gone or grew (a node joined) are marked so, and listed first. The request
  runs in the LSP or the web host through Anthropic's C# SDK (new `Dependinator.Claude` library),
  never in the browser app.
  On Oct 25 the review dialog's two AI routes became one "Get an AI opinion" section: "Ask Claude"
  (API key, a few cents) and "Use your AI chat" (free) side by side, each saying what happens, since
  a bare copy icon next to "Ask Claude" read as a rival feature. The chat route shows its steps at
  once (copy, open claude.ai, paste back), a pasted answer is read without a button, and answers
  are labeled by source ("Claude · Break", "Chat · Accept"), so the other route reads as a second
  opinion.
- **AI review of broken rules, Show prompt and Claude Code (Oct 26).** Each dependency that
  breaks an architecture rule has a Review button, with the same AI opinion section as a cycle:
  Claude (or an AI chat's pasted answer) suggests fixing it, with a fix prompt for Claude Code, or
  allowing it, with a reason. An allowed dependency moves to the rule's collapsed Allowed list,
  is no longer drawn or counted as a violation, and is saved with the model (undoable); removing
  the rule takes it along. "Show prompt" in both reviews shows the prompt every route uses before
  it is sent or copied. A hidden, experimental third route asks Claude Code (`claude -p`) on the
  user's computer with their own Claude login instead of an API key: it is unclear whether
  Anthropic allows that for a third-party tool, so it is off until turned on under Settings › Use
  Claude Code (Experimental), after a warning. (First built behind an unlisted VS Code setting and
  an environment variable; moved to the Settings menu the same day, while Michael is the only user.)
  The same day Claude Code got read-only tools in the model's folder, to check the excerpts
  against the code, and "Fix with Claude Code": when an answer suggests breaking a cycle or fixing a
  rule, Claude Code makes the change in the model's folder (it may edit files there, not run
  commands), after a warning when the folder has uncommitted changes; the dialog shows its report
  and the changed files, and re-parses.
- **About Node (Oct 27).** "About Node …" in the node menu and the right-click menu opens a dialog
  with the node's comment from the code, what it contains, and how many types outside it it uses
  and are used by it, plus a question about it ("What is this and why is it here?" until the user
  writes another) for Claude, Claude Code or an AI chat. The prompt has the node and its parent with
  their comments, its children, the types it uses and that use it (most links first), and a type's
  source or a container's files. The answer is shown, not saved, and can be copied. Claude answers
  on Claude Sonnet 5.5 here (`IClaudeService.AskQuicklyAsync`), faster and cheaper than the Opus
  used for verdicts.
- **Improve Comments (Oct 27).** "Improve Comments …" in the node menu (one or several selected
  nodes) and in the About dialog has Claude Code add or rewrite the comments of namespaces and types,
  of the nodes alone or of everything inside them, which the diagram shows as descriptions. The
  prompt (`CommentsPrompt`) lists each one with where its comment goes, its comment now and what it
  uses and is used by, and says what a good comment says and where the parser reads it (a namespace
  comment in one file only). It runs like Fix with Claude Code, on the current branch (edits, no
  commands), with a warning above 150 namespaces and types; without the Claude Code route the prompt
  is copied for any coding agent. Claude Code's run limit went from 15 to 30 minutes for it.
  Since Nov 4 it also covers projects: a dll's description, its `[assembly: AssemblyDescription]`
  attribute (or the project file's `<Description>`), is added or rewritten like a comment.
- **MCP server (Oct 27).** `Dependinator.Mcp` (the official MCP C# SDK, Streamable HTTP) gives AI
  agents read-only tools over the open diagram: `get_model`, `list_children`, `search_nodes`,
  `get_dependencies`, `find_paths`, `list_cycles`, `list_rules` and `check_rule`, which counts what
  a rule would break before it is added. The tools ask the UI through `[Rpc] IModelQueryService`
  (`Diagrams/Ai/ModelQueries.cs`), so the answers come from the model the user sees. The language
  server runs it on a loopback port with a bearer token; the extension registers it for Copilot and,
  with "Dependinator: Connect Claude Code", for Claude Code. The dev host serves it at `/mcp`.
- **Suggest rules (Oct 27).** "Suggest rules …" in the Rules panel asks Claude, Claude Code or the
  user's AI chat for up to 8 architecture rules. The prompt (`SuggestRulesPrompt`) lists the
  containers level by level (up to 120) and the link counts between those listed deepest (up to
  150), plus the existing rules. The Claude Code route gets the app's own MCP server, so it checks
  candidates with `check_rule` before answering (13 checks for this repository, 25 s). Dependinator
  re-checks every suggestion and shows "kept today", "N broken" or why it cannot be added; the user
  ticks which to add.
- **Problem indicator (Oct 21).** A warning count in the app bar, like the VS Code status bar,
  shows that the model has open cycles and broken architecture rules without opening their
  panels; it is hidden while there are none. Its tooltip names them, and a click lists them, one
  row per kind, each opening its panel. Accepted cycles, cycles to fix later and rules nobody
  breaks don't count. Since the same day it also counts compile errors, which can silently drop
  nodes and links: the C# parser reports them per project and the Bicep parser per file, briefly
  (the editor lists each error), shown as an error count with one row per project that shows its
  node. They are saved with the model, so a model opened from the cache shows them before its
  refresh. Compile warnings are left out for now: many projects have warnings nobody fixes. A
  project that fails to parse is listed too. Showing them exposed 61 false errors in Dependinator
  itself: the C# parser ran the Functions and Regex source generators a second time.
  Nodes in the diagram carry a badge with the number of problems in or inside them (red with
  compile errors, orange otherwise), so the badges lead from the top level down to where the
  problems are; "Show Problem Badges" in the View menu turns them off.
- **Cycle navigation (Oct 22).** Clicking a cycle in the cycles panel zooms to the cycle's own
  nodes inside their opened container, instead of to the container as an icon. It also picks the
  cycle: its lines stay red while the other cycles' lines fade, and lines not touching its nodes
  fade as for a selection of them, until the row is clicked again.
- **Rule navigation (Oct 29).** A rule's two node names in the Rules panel are links that go to
  and select the node, and a broken dependency frames both of its classes, with the orange line
  between them (before, it went to the source class only).
- **Suggest rules beside the diagram (Oct 29).** Suggest rules moved from a dialog over the
  diagram into the Rules panel, which switches to a suggestions view with a back arrow. Its node
  names go to their node, each rule has its own Add button (plus "Add all"), and the checks follow
  the model as rules are added or it is re-parsed. The view stays alive while hidden, so an answer
  that takes a while still arrives after going back to the rules or closing the panel.
- **Ask about the model (Oct 29).** A question box in a panel beside the diagram (app bar button,
  View › Ask About the Model): Claude Code looks the answer up with the app's own MCP model tools
  and reads the code, and every node name in its answer is a link that goes to the node, with the
  nodes it is about listed below and "Show all" framing them. The prompt (`AskModelPrompt`) holds
  only the model's top level and the question; the tools fetch the rest. Answers stay while the
  panel is closed, until another model opens (roadmap idea 5, phase 1).
- **Ask with the API key (Oct 29).** The Ask panel's first route is now Claude with the user's
  own API key: `ModelToolsAgent` in `Dependinator.Claude` runs the tool-use loop itself (Sonnet
  5.5, at most 15 turns, the last without tools) and gets the very same tools as an MCP client of
  the app's own server (`McpAgentTools`), so nothing is defined twice. Both routes show the
  lookups live ("Searched for …", reported by `ModelQueryService`), and an API answer shows what
  it cost (phase 2).
- **Ask follow-ups, and read the code (Oct 29).** A question in the Ask panel follows up on the
  earlier ones: the prompt carries the last three questions and answers until the conversation is
  cleared. A new `read_source` model tool reads a node's code with line numbers (through
  `IWorkspaceFileService`, so in VS Code over the tunnel), for the API route, Copilot and Claude
  Code alike. A name an answer writes partly (`PromptParts.ReadSourcesAsync`) links too when only
  one node ends so. `./scripts/ask-eval` asks a set of questions about this repository
  (`scripts/ask-eval.json`) in the dev host and scores the answers (phase 3).
- **One default AI, Settings › AI (Oct 30).** The places that ask an AI share one provider choice:
  `AiProvider` lists the providers (Claude, Claude Code, the AI chat) with what each can do (an
  answer, model lookups, code changes), and Settings › AI picks the default and turns on the
  experimental Claude Code route (it replaced the "Use Claude Code" menu toggle). The review dialogs
  show the providers side by side only until one is picked, which becomes the default; after that
  one button asks it, and the menu next to it (`AiProviderMenu`) asks another once. The Ask panel's
  route buttons, cost line and Show prompt became one chip naming the provider, with the same menu.
  Another provider (Copilot, Codex, Gemini) is now one more `AiProvider` entry plus its route in
  `IAiService`.
- **Copilot, a third AI provider (Nov 2).** In VS Code the review dialogs, About Node and Suggest
  rules can ask a model VS Code offers to extensions (its Language Model API, `vscode.lm`):
  Copilot's, or one picked in Settings › AI. Core's `[Rpc] IVsCodeModelService` goes to the LSP,
  which asks the extension (`languageModels.ts`); the answer's JSON schema is the input schema of
  an `answer` tool the model must call, since the API has no structured output. VS Code asks the
  user once whether Dependinator may use its model. Answers only for now: no model tools (the Ask
  panel) and no code changes (phase 1).
- **Copilot looks things up (Nov 2).** The Ask panel and Suggest rules can ask Copilot too: the
  LSP runs the tool loop (`VsCodeToolsAgent`, up to 15 turns) with the same model tools as the
  API key route (`McpAgentTools`), and the extension asks the model one turn at a time; the answer
  is still the `answer` tool's input, the only tool allowed on the last turn (phase 2).
- **Pick the Claude model (Nov 10).** Settings › AI has a Model picker under Claude and under
  Claude Code, like Copilot's. Claude's lists the models the API key can ask, live from Anthropic's
  Models API (`IClaudeService.GetModelsAsync`), so a new model needs no release; "Automatic" keeps
  Opus 5.5 for reviews and Sonnet 5.5 for questions and the Ask panel, and a picked model answers
  everything. Claude Code's offers its aliases (fable, opus, sonnet, haiku) or a typed model name,
  passed as `claude --model`; "Automatic" leaves it to Claude Code's own setting. The Ask panel's
  cost is priced by the model that answered (`ClaudeModels`), and unknown for one without a price.
- **Ask about the selected nodes (Nov 10).** The Ask panel follows the selection: chips above the
  question name the selected nodes, "with everything inside" or alone, and the prompt tells the AI
  what "it" and "these" mean (`AskContext`); without chips the question is about the whole model.
  The example questions fit what is asked about, grouped as Understand, Change and Improve
  (`AskExamples`), with "More examples" and a lightbulb that shows them again later. The node menus
  have "Ask About Node …", and About Node sends its example questions to the panel instead of
  asking in the dialog, and has "Copy prompt" for one's own AI chat.
- **Fix with Copilot (Nov 2).** The cycle and rule reviews offer "Fix with Copilot", and Improve
  comments "Improve with Copilot", in VS Code where Copilot is offered: the prompt goes to Copilot
  Chat in agent mode (`IVsCodeModelService.OpenAgentChatAsync`, the extension runs
  `workbench.action.chat.open`), which edits with its own tools and Keep/Undo review. Nothing comes
  back to the dialog; the extension's file watcher re-parses (phase 3). The same day the review
  dialogs' fix buttons became one, like the ask button (`AiFixButton`): "Fix with" the default (or
  the first that can change code), the others and "Copy fix prompt" in the menu beside it.
- **Ask how to break an accepted cycle (Nov 3).** Under an AI answer that suggests accepting a
  cycle, "Ask how to break it" (with the provider menu beside it) asks again for a change only
  (`CyclePrompt.Build(..., acceptedAnswer)`, an `AiFollowUp` of `AiOpinion`); its Break answer
  brings the usual fix button. Fix later shows beside Accept too. No fix runs on an accepting
  answer itself, since it has no change for the user to read first.
- **Tour this codebase (Nov 3).** Help › Tour This Codebase … opens a panel (`TourPanel`) where
  any AI provider plans a tour of 5–8 key nodes in reading order (`TourPrompt`, the model outline
  of Suggest rules without the externals; the chat route reads "Stop:" lines back). Dependinator
  leaves out the stops that are no nodes, and Start tour plays it in a callout like the tips
  (`TourCallout`), zooming to and selecting each stop's node, with Back and Next. Phase 1: the tour
  is not saved yet.
- **Suggest rules remembers (Nov 4).** A suggested rule can be dismissed (`DismissedRule`, saved
  with the model; removing a rule dismisses it too), and later answers leave out the dismissed
  rules and those an earlier answer suggested: the prompt lists them, and Dependinator drops
  repeats itself ("2 more were left out"). The prompt asks for the rules clearly worth keeping,
  at most 8, and an answer with none is fine. An added rule keeps the AI's why as its reason
  (`ArchitectureRule.Reason`), shown and editable in the Rules panel, whose "Not suggested again"
  list takes a dismissal back.
- **Fix later for rule violations (Nov 4).** The rule review dialog has Fix later beside Allow,
  like the cycle review: the dependency moves to the rule's To fix list (`AllowedViolation.IsToFix`,
  saved with the model) with the AI's suggested change as the plan. It stays drawn as breaking the
  rule but is no longer counted as a problem; its row copies a fix prompt for the plan, reviews it
  again, edits the plan or reports it again.
- **Rule kinds (Nov 4).** Besides "X must not use Y", the Rules panel adds "only X may use Y"
  (nothing outside both may use Y) and layers, picked top first (each layer may use only the
  layers below it). `ArchitectureRule.Kind` and `LayerNames`, saved with the model; an unknown kind
  reads as "must not use". Reviews, prompts, Fix later and the MCP `list_rules` text know the
  kinds. Suggest rules still suggests only "must not use".
- **One-node rules (Nov 4).** "X is independent" (nothing in X may use the rest of the solution,
  only externals) and "no cycles inside X" (the cycles the Cycles panel finds at or inside X,
  each of their links a violation with the usual review, Allow and Fix later; cycles accepted or
  marked to fix in the Cycles panel are left out, decisions both share).
- **Suggest rules proposes every kind (Nov 4).** The AI picks the kind that says a rule best
  (one layers rule rather than several "must not use", "is independent" for a shared part),
  answering `{kind, nodes, why}`; pasted answers read every form. Dependinator checks each kind
  itself, and dismissals remember every kind (`ModelDto.KindDismissedRules`). The MCP
  `check_rule` takes a kind (and layers), so an agent can test any kind of rule. A new kind, "the
  parts of X are independent" (`SiblingsIndependent`: no child of X may use another child), keeps
  features or modules apart. Name-pattern rules were postponed.
- **AI-suggested line labels (Nov 4).** The line label dialog asks any AI provider for a label
  and description (`LineLabelPrompt`: the line's ends, its links and their code, its label now
  and the labels on other lines, for a matching style), with up to three other labels to pick
  from (`LineLabelSuggestion`; the chat route reads "Label:" lines back). Empty fields are filled
  at once, else "Use suggestion" fills them; nothing is saved before Save.
- **Member cards (Nov 4).** An open type shows its members like a class box: a card with a
  section per kind (Fields, Constructors, Properties, Events, Methods; Bicep's Parameters,
  Variables, Resources, Outputs), public members first, flowed into as many columns as the box
  takes (`MemberCard`). A type of only members was shaped like its card, so the card filled it;
  since Nov 7 types are squares again, like their icon, so lines end at the icon (the card flows
  its columns to the square; layout version 7).
  Zooming in, the type is first an icon, then a summary card (kind, counts, the first members and
  "+ N more", `NodeViewPolicy.IsMemberSummary`) until the rows are readable, then the full card.
  Lines land on the member rows (in at the left, out at the right), calls inside one type show
  only for the selected or hovered member, and rows can no longer be dragged or resized. A Bicep
  module gets the card left of its resources. The layout version is 2, so saved models lay their
  containers out again.
- **Member signatures and private rows (Nov 4).** Member rows show their signature after the
  name (`(string query): Task<int>`, `: string`; Bicep parameters and outputs their type) and
  their comment after it, in the row instead of squeezed under it. A large type (more than 12
  members, public and private) folds its private rows into a "+ N private" row; a click shows
  them ("hide N private" folds them again), saved with the model, and the view keeps the row
  under the pointer. Lines from folded rows end at that row, and going to a private member (search,
  a link, the editor) opens the private rows. Layout version 3.
- **One UML card (Nov 6).** An open type is now drawn as the card itself: its box gets the
  summary card's header (icon, name, kind) and the member rows run from its top left under it,
  with signatures and lines, instead of a container with a smaller card centered inside and the
  name below. Nested types sit inside the card below the rows. The summary card shares the frame
  and header and shows signatures too, so zooming in just fills the card. Layout version 4.
  Every type card has the same lavender header and border (a custom color still wins), signatures
  follow the name UML style (`Url: string`), private rows are muted with a small lock on their
  icon (first a "private" tag on the right, replaced the same day at Michael's pick), and the
  summary card lists nested types last and shows its counts line only when rows are left out.
- **Report a bug / suggest an improvement (Oct 23).** Two Help menu items open a new GitHub
  issue from the repo's issue forms, with the version and environment (host, build, VS Code
  version and OS or browser, language, window size) filled in. VS Code opens the page through the
  extension host, like Help; bug reports get the `bug` label from their form.
- **What's New (Oct 27).** A Help menu item opens `CHANGELOG.md` on GitHub (main, so the
  latest release's notes plus what is unreleased at that point).
- **Settings.** Invert the mouse-wheel zoom direction (Sep).

## Editing and design

- **Layout editing (Feb).**
  - Snap to grid when moving and resizing.
  - Reset the layout of children or descendants.
  - Grow and shrink nodes.
  - Line segments, with points added or removed where clicked.
  - Nodes drawn over lines.

  The initial layout was improved in Jul. Since Oct 14 a reset can be undone and hands the
  container back to the automatic layout (it stayed "user-arranged" before).
- **Node sizes and straighter lines (Oct 14).** The automatic layout sizes each node by its
  code (declaration lines, summed up the tree) and how central it is among its siblings, in four
  steps (60, 100, 140, 180), so the big and coordinating parts stand out and small utilities
  stay small. Bigger nodes open as containers earlier while zooming in. Nodes are centered in
  their columns and pulled toward their neighbors from both sides, so lines run straighter.
  A thorough layout pass orders nodes for fewer line crossings and routes long lines around
  nodes; it runs when a container is first shown, or for a large container in idle moments
  afterwards (refined in place, never while the view is zoomed into it). Models saved with the
  older layout are laid out again, except containers the user arranged.
- **Hidden nodes (Mar).** A menu toggle shows or hides hidden nodes. Hiding became undoable on
  Oct 11. Since Oct 23 each node is dimmed (faint) or hidden (left out) on its own, its
  descendants following it, and View › Reveal Hidden Nodes shows hidden ones faintly while it is on.
  A count in the toolbar opens the hidden nodes list (switch dim/hide, go to, show); going to a
  hidden node reveals hidden nodes. A grey badge on a node counts the children the user hid, and
  its menu lists them.
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
  void` removed, SVG/CSS numbers formatted culture-invariant. On Nov 3 the `Diagrams` root went
  from 64 files to 19: the features moved into a folder each (`Cycles`, `Rules`, `Lines`,
  `Navigation`, `Tours`, `Ask`, `NodeHelp`), and `Interaction` was split into input and `Editing`.
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
- **E2E flake root causes (Oct 23).** A review of the failed CI runs found the flakes came from
  load, not from single tests, and three app-side sources were removed: tests no longer load
  Clerk and Google Fonts from the internet (a signed-out Clerk stub per test), runs without sync
  start the app with cloud sync off, and the first test warms the app up. Panning and zooming
  save a small view record instead of the whole model, and tests wait for the app's
  `data-save-pending` mark instead of sleeping before a reload. CI no longer traces every test (that
  doubled the suite time): it runs untraced and re-runs only the failed tests with tracing
  (`./scripts/e2e -r`).
- **CI (Jun 20).** Unit-test and e2e workflows run on feature-branch pushes. CI/CD runs them
  before deploying; the full gate covers all browsers plus sync. Since Oct 19 the browsers run
  as parallel jobs (the serial run had grown to ~15 min of a 20 min gate).
- **Release automation (Jul 21–23).** `CHANGELOG.md` is generated from commit messages, a push to
  `main` creates a release, and the CI/CD and extension workflows were unified.
- **Release flow (Oct 6).** Dependabot PRs target `dev`, grouped. `./scripts/release` merges `dev`
  into `main` with changelog bullets. Each release bumps the minor version.
- **Release workflow (Oct 22).** gmd's process: nothing is published by a push or a merge. A
  manual Release workflow makes a preview from `dev` (pre-release site, GitHub pre-release) or a
  release from `main` (it merges `dev` into `main`, raises the minor on `dev`, deploys production,
  publishes the Marketplace and the GitHub release), after the full gate. The notes are written by
  hand or by Claude (the `release` skill) in `CHANGELOG.md`'s `## [Unreleased]`, grouped New,
  Improved, Changed, Fixed. One version `major.minor.<minutes since 2026-01-01>` everywhere
  (`docs/RELEASING.md`).
- **Marketplace pre-releases (Oct 26).** Previews are also published to the VS Code Marketplace's
  pre-release track, which users opt into with **Switch to Pre-Release Version**. The version
  grows across previews and releases, so no odd/even minor is needed.
- **Security (Oct 8).** CodeQL alerts fixed and the workflow's `GITHUB_TOKEN` permissions limited.
  Vulnerable transitive packages (MessagePack, System.Security.Cryptography.Pkcs/Xml) pinned to
  patched versions until their parent packages catch up (Oct 19).
- **Demo recording (Jul).** `./scripts/record-demo` re-records the README demo gif.
- **Claude Code cloud sessions (Oct 21).** Sessions started from the web or the phone run in a
  fresh VM without the devcontainer:
  - `./scripts/cloud-setup` (run by a SessionStart hook) installs the build and test toolchain;
  - the project knowledge that had lived in local memory moved to `.claude/rules/`, so every
    session has it;
  - feature branches may now be pushed without asking.
- **Automatic issue triage (Oct 23).** A Claude scheduled task triages new issues of every type:
  labels and a verdict, a comment with its reasoning, and a fix PR into dev for small clear bugs.
  `issue-triage.yml` starts it through its API trigger as soon as an issue is opened; its own
  schedule catches anything a fire missed.
- **Branch graph on the web (Nov 9).** `branch-graph.yml` publishes gmd's graph of all active
  branches as a page on GitHub Pages (https://michael-reichenauer.github.io/Dependinator/), to
  follow the branches and pull requests from a phone or tablet. It runs the latest gmd release's
  `gmd --web-log` on every push, pull request change and hourly.

## Docs and presentation

- Extension and repo READMEs (Jun–Jul), a help page (Jun 30; `/help` route in Oct), and an About
  box that explains dependinator.com and shows the versions.
- A Marketplace listing with the demo gif (Jul).
- Oct 8: the README was reworked around users, with a contributing guide, issue templates,
  community files, GitHub Discussions for questions and social preview tags.
- Bicep in the help page and READMEs with a screenshot, and architecture diagrams of the webshop
  example (Oct 9–11).
- Internal notes moved to `docs/notes/` (Oct 8).
- **Architecture overview (Oct 22).** `docs/ARCHITECTURE.md` describes the app for the team and
  its managers in seven generated diagrams (context, hosts, code structure, runtime, VS Code,
  cloud sync, delivery), with design decisions, a reading guide and open risks.
