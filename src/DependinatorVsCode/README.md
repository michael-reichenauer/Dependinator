# Dependinator

Dependinator visualizes the structure and dependencies of your C#/.NET
codebase as an interactive, navigable map, making complex architectures easier
to understand, analyze, and refactor — without leaving VS Code.

> ⚠️ **Beta** — Dependinator is under active development and published as an
> early preview for a small group of testers. Expect rough edges, missing
> features, and breaking changes between versions. It is not yet a finished
> product.

![Dependinator in action: zooming into the dependency map and navigating to a node with search](https://github.com/michael-reichenauer/Dependinator/raw/HEAD/src/DependinatorVsCode/resources/demo.gif)

## Features

- **Interactive dependency map** — explore your codebase as a zoomable,
  navigable diagram instead of scrolling through files.
- **Drill down and back up** — open nodes to see their internals
  (namespaces, types, members) and follow dependencies between components.
- **Jump to code** — navigate from a node or dependency straight to the
  corresponding source location in the editor.
- **Automatic refresh** — the map updates automatically when you change
  source files (configurable).
- **Runs inside VS Code** — opens in an editor tab; no separate app to launch.
- **Optional cloud sync** — sign in to sync your customized views across
  devices.

## Requirements

- A workspace containing a **.NET solution (`.sln`)** with C# projects —
  Dependinator parses the solution to build the dependency map.
- No local `dotnet` installation is needed; the extension bundles a
  self-contained language server.

## Getting started

1. Install the extension and open a folder containing a `.sln` file.
2. Open the command palette (`Ctrl+Shift+P` / `Cmd+Shift+P`) and run
   **Dependinator: Open**, or click the Dependinator icon
   <img src="https://github.com/michael-reichenauer/Dependinator/raw/HEAD/src/DependinatorVsCode/resources/icon-toolbar.png" alt="Dependinator title-bar icon" height="16" align="top" />
   in the editor title bar.
3. The dependency map opens in a new tab. Click nodes to drill in, and drag /
   zoom to navigate.

Need more guidance? Click the **Help** button in the Dependinator app bar —
it opens a page with detailed usage instructions, navigation tips, and
keyboard/mouse controls.

## Commands

| Command | Description |
| --- | --- |
| `Dependinator: Open` | Open the Dependinator dependency map. |
| `Dependinator: Install in Dev Container` | Jump to the Extensions view to install the extension inside a dev container. |

## Settings

| Setting | Description |
| --- | --- |
| `dependinator.autoRefresh.enabled` | Automatically refresh the diagram when workspace source files change (default: `true`). |
| `dependinator.autoRefresh.delaySeconds` | Delay after the last source file change before the diagram is refreshed (default: `3`). |

## Cloud sync

Cloud sync is optional. When you sign in (from the Dependinator app bar),
your customized diagram layouts and views are synced across devices via the
Dependinator cloud service. Without signing in, everything works locally.

## Feedback & issues

Found a bug or have a feature request? Please open an issue at
https://github.com/michael-reichenauer/Dependinator/issues

## Links

- Repository: https://github.com/michael-reichenauer/Dependinator
- Contributing / building the extension: see [DEVELOPMENT.md](DEVELOPMENT.md)

## License

MIT — see [LICENSE](LICENSE).
