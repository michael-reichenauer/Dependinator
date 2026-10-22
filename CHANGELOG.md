# Change Log

What changed for the people who use Dependinator, in each release, newest first. A release includes
the previews before it. Releases with nothing users notice are left out.

## [Unreleased]

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
