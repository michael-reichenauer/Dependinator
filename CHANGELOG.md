# Change Log

What changed for the people who use Dependinator, in each release, newest first. A release includes
the previews before it. Releases with nothing users notice are left out.

**Unreleased** lists what is not yet in an official release: changes already out in a preview
(pre-release) and changes still waiting for the next preview. They move under a version heading
with the next official release.

## [Unreleased]

## [v0.87.435765] - 2026-10-30

### New

- Previews of the VS Code extension can be installed from the Marketplace: **Switch to Pre-Release Version** on the extension's page in VS Code gets each preview as it comes out, and **Switch to Release Version** goes back
- A dependency that breaks an architecture rule can be reviewed like a cycle: ask Claude, Claude Code or your own AI chat whether to fix it or allow it; fixing gives a prompt for an AI that can change the code, and allowing keeps the dependency with a reason under its rule, where it is no longer counted as a problem
- **Settings › AI** picks the AI that every AI feature asks: **Claude** (your own API key), **Claude Code** (experimental, turned on there after a warning) or **your AI chat** (copy the prompt, paste the answer back); the first time you ask, the choices are shown and the one you pick becomes the default, and the **▾** menu beside the button asks another one once
- **Claude Code** (experimental) asks Claude Code on this computer with your own Claude login instead of an API key; it reads the code in the model's folder to check its answer
- **Fix with Claude Code** in the cycle and rule reviews lets Claude Code make the suggested change in the model's folder (edits only, no commands), after a warning about uncommitted changes; it shows the running time with **Stop**, then a summary and the changed files, and re-parses
- **AI agents can ask about the diagram**: the extension runs an MCP server that Copilot's agent mode lists as "Dependinator", and **Dependinator: Connect Claude Code** adds it to Claude Code for the folder; its tools find nodes and look up their dependencies, paths between them, cycles and architecture rules, and check a rule before it is added, while the diagram is open
- **Suggest rules …** in the Rules panel asks Claude, Claude Code or your own AI chat to suggest architecture rules for the model; Dependinator checks each one and shows what it would break today, and you tick the ones to add (Claude Code can check its own candidates through Dependinator's MCP server first)
- **About Node …** in the node menu shows a node's comment, what it contains and how many types it uses and is used by, with a question about the node ("What is this and why is it here?" by default) for Claude, Claude Code or your own AI chat
- **Ask About the Model** (View menu or the app bar) opens a panel for questions about the model: Claude or Claude Code looks the answer up with Dependinator's tools and reads a node's code when needed, the nodes in the answer are links to the diagram (**Show all** zooms to them), each lookup shows as a line while it works, and follow-up questions see the conversation until it is cleared
- **Improve Comments …** in the node menu and the **About Node** dialog lets Claude Code add or rewrite the comments of namespaces and types, of one or several nodes or of everything inside them, which the diagram shows as descriptions; without Claude Code the prompt is copied for any coding agent
- **Help › What's New** opens this change log

### Improved

- **Show prompt** in the cycle and rule reviews shows the prompt before it is sent or copied
- The two node names of a rule in the Rules panel go to and select their node, a broken dependency frames both of its classes with the line between them, and long rule names wrap instead of being clipped

### Fixed

- Code excerpts in AI prompts no longer start a line early or drop the type's closing line
- **Show Source Code** on a Razor component opens its `.razor` file, and its `@code` members open at their own line (re-parse to pick this up)

## [v0.86.429455] - 2026-10-26

### Improved

- The cycle review offers its two AI routes side by side under **Get an AI opinion**: **Ask Claude** (your own API key, a few cents) and **Use your AI chat** (free), each saying what happens; the AI chat route is laid out as steps, a pasted answer is read at once, and answers show where they came from ("Claude · Break", "Chat · Accept"); a failed ask offers the AI chat route instead
- **Help** is the last button in the app bar, after the problem and hidden nodes indicators

### Fixed

- The hidden nodes count in the app bar is drawn in the app bar's color like the other icons, instead of in red

## [v0.85.425921] - 2026-10-23

### New

- Each node can be dimmed or hidden on its own (**Dim Node**, **Hide Node**, **Show Node** in the node menu): a dimmed node and its lines are drawn faintly, a hidden one is left out, and what is inside follows; **View › Reveal Hidden Nodes** shows the hidden nodes for a moment without changing them
- A count in the toolbar shows how many nodes are dimmed or hidden, and a click (or **View › Show Hidden Nodes List**) lists them, each to dim, hide, show or go to, with a filter and **Show All**; going to a hidden node from the list, search, a link or a cycle reveals hidden nodes
- A grey badge on a node counts the children hidden in it (**View › Show Hidden Node Badges**), and **List Hidden Children** in the node menu lists just those
- In VS Code with `dependinator.autoRefresh.enabled` off, a dot on the Re-parse button shows that source files changed since the last parse
- **Help › Report a Bug** and **Help › Suggest an Improvement** open a new GitHub issue with the version and environment filled in

### Improved

- Panning, zooming and navigating save only the view, not the whole model, so moving around is lighter, and a model opens at the view it was last left at

### Changed

- Selected lines, and the lines in the dependency explorer, stay thin rather than widening with their number of links; **View › Line Widths by Link Count** turns the widths back on

### Fixed

- The diagram fills the window after an iPad is rotated, instead of being cut off on the right

## [v0.84.424430] - 2026-10-22

### New

- Cycles can be reviewed in a dialog: accept a cycle as intended with a reason (shown with it and saved with the model), or mark it to **Fix later**; an accepted cycle is reported again if another node joins it
- **Copy AI prompt** in the cycle review copies a prompt with the cycle and the source behind it for any AI chat, whose answer can be pasted back; **Open claude.ai** opens claude.ai to paste it into
- **Ask Claude** in the cycle review asks Claude, with your own API key, whether a cycle is intended or should be broken, and **Copy fix prompt** copies a prompt for breaking it; in VS Code the key is set with **Dependinator: Set Claude API Key**
- A problem indicator in the app bar counts open cycles, broken architecture rules and compile errors (one row per C# project or Bicep file), and lists them on a click
- Nodes show a badge with the number of problems in or inside them, red for compile errors and orange for cycles and broken rules; **View › Show Problem Badges** turns them off
- A refactor that breaks or changes an accepted cycle is pointed out

### Improved

- C# members are named and typed as they are written: constructors have their own icon, a static constructor has a node of its own, a C# 14 extension block shows as an `extension(Type)` node, and conversion operators and indexers no longer make up extra nodes
- Links to generic types and to extension methods reach the declared node
- A namespace commented in several files shows each comment with its file and line, and a license header is no longer taken for a namespace description
- Picking a cycle frames its nodes and dims the lines and cycles unrelated to it, without thickening its lines
- The cycles panel can be minimized like the dependencies explorer
- The VS Code tab of the diagram shows the Dependinator icon

### Changed

- The version is now `major.minor.build`, shown with its build time in the **About** dialog, and previews of the next release are published on GitHub

### Fixed

- Clicking a line selects it reliably in Safari and other WebKit browsers, a click close to a hovered line no longer misses, and a click on a selected line's dashed outline no longer deselects it
- C# source generators no longer cause false compile errors
- A design node name with a stray bracket, such as "Latency < 50ms", displays correctly

## [v0.83.0] - 2026-10-19

### Improved

- Nodes are sized by their amount of code and how connected they are, so the central parts of a model are drawn larger and small utilities stay small
- Containers are laid out with fewer line crossings and straighter lines, and large containers' layouts are refined in the background while the app is idle
- Larger nodes open as containers earlier while zooming in
- Saved models are laid out again with the new layout; containers you arranged yourself are kept
- **Reset Children Layout** and **Reset Descendants** can be undone, and a reset container follows the automatic layout again after a re-parse

### Fixed

- A node framed by **Show node** stays small enough to be drawn as an icon

## [v0.82.0] - 2026-10-14

### New

- Azure Bicep support: a deployment, an entry-point `.bicep` file and its local modules, opens as a dependency map, with resources drawn with Azure icons and nested under their deployment scope, such as a resource group; a module takes the icon of its main resource
- Bicep parameters, variables, outputs and auxiliary resources are shown as rows, and configuration lines are hidden by default (**View › Lines**)
- In VS Code, Bicep deployments are listed as workspace models, are re-parsed when a `.bicep` file changes, and the current Bicep file can be revealed in the diagram
- The web app's **Models** menu has a Bicep demo, a large example deployment
- Zones: labelled background areas for grouping nodes
- Line labels with an optional description, edited from the toolbar, the context menu or by double-clicking a line
- Display aliases show a node under a friendlier name in the diagram and in search
- **View › Rows** leaves out whole kinds of rows

### Improved

- Hiding nodes can be undone
- Search ranks the node whose name matches the query exactly first
- The first-run tour's zoom step points to **Settings › Invert Scroll Zoom**

## [v0.80.0] - 2026-10-08

### Improved

- Lines in the dependency explorer offer go to source and go to target
- The dependency explorer sits below the app bar and the breadcrumb, with the node's name in its subtitle, and no longer has a row button for showing a direct line
- The web app has a help page at `/help`
