# Change Log

What changed for the people who use Dependinator, in each release, newest first. A release includes
the previews before it. Releases with nothing users notice are left out.

## [Unreleased]

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
