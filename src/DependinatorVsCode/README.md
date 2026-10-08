# Dependinator

See your C#/.NET codebase as an interactive dependency map, right inside VS Code.
Dependinator parses your solution and draws it as a zoomable map: projects,
namespaces, types and members are nested nodes, dependencies are the lines
between them. Zoom out for the big picture, zoom in to see how a component works
inside, and jump from any node straight to its source.

![The Dependinator map of a solution: projects as containers, their namespaces and types inside, and dependencies as lines between them](https://github.com/michael-reichenauer/Dependinator/raw/HEAD/docs/images/hero.png)

Dependinator is in beta: the core works end to end, but expect rough edges and
the occasional breaking change between versions. Feedback shapes what gets built
next, so please [report bugs and ideas](https://github.com/michael-reichenauer/Dependinator/issues/new/choose).

## Why

- **Get oriented fast.** Joining a project or opening an old codebase? See how
  it is put together before reading a single file.
- **Refactor with confidence.** Find the tangles, see what depends on what, and
  check that a change lands where you expect.
- **Keep an architecture picture that stays true.** The map is generated from
  the code and refreshes as you edit. Annotate it with notes, icons and
  hand-drawn nodes, and it still reflects reality tomorrow.

## Features

- **Interactive map.** Pan, zoom and double-click to drill into any node. The
  breadcrumb and search (`Ctrl+F`) take you anywhere in one step.
- **Dependencies explorer.** Pick a node and browse everything it uses and
  everything that uses it, down to member level.
- **Jump to code.** Open the source of a node or dependency in the editor. The
  map can also follow the file you are editing.
- **Automatic refresh.** The map updates when source files change (configurable).
- **Edit mode.** Arrange nodes, pick icons and colors, add notes, draw your own
  nodes and links, or sketch an architecture from an empty model. Every edit can
  be undone.
- **Icon library** with curated Azure, AWS and Google Cloud service icons.
- **Light and dark themes** (following VS Code), a mini-map, a legend, and
  export of the diagram as an image.
- **Multiple models** per workspace, with optional sync across devices and the
  companion web app at [dependinator.com](https://dependinator.com).

## Requirements

- A workspace containing a **.NET solution (`.sln`)** with C# projects.
  Dependinator parses the solution to build the dependency map.
- No local `dotnet` installation is needed; the extension bundles a
  self-contained language server.

## Getting started

1. Install the extension and open a folder containing a `.sln` file.
2. Open the command palette (`Ctrl+Shift+P` / `Cmd+Shift+P`) and run
   **Dependinator: Open**, or click the Dependinator icon
   <img src="https://github.com/michael-reichenauer/Dependinator/raw/HEAD/src/DependinatorVsCode/resources/icon-toolbar.png" alt="Dependinator title-bar icon" height="16" align="top" />
   in the editor title bar.
3. The dependency map opens in a new tab. Zoom into nodes (or double-click
   one) to drill in, drag to pan, and press `Ctrl+F` to find a node by name.

The **Help** button in the Dependinator app bar opens the
[user guide](https://dependinator.com/help) with detailed usage instructions,
navigation tips, and keyboard/mouse controls.

![Navigating to a node with search and exploring its dependencies in the Dependencies explorer](https://github.com/michael-reichenauer/Dependinator/raw/HEAD/src/DependinatorVsCode/resources/demo.gif)

## Commands

| Command | Description |
| --- | --- |
| `Dependinator: Open` | Open the Dependinator dependency map (also the icon in the editor title bar of C# and solution files, and the status bar item). |
| `Dependinator: Reveal Current File in Diagram` | Show the node for the file at the cursor (`Ctrl+Alt+D`, `Cmd+Alt+D` on macOS; also in the editor's right-click menu). |
| `Dependinator: Search Nodes` | Open the diagram's node search. |
| `Dependinator: Re-parse Solution` | Parse the solution again now. |
| `Dependinator: Install in Dev Container` | Jump to the Extensions view to install the extension inside a dev container. |

## Settings

| Setting | Description |
| --- | --- |
| `dependinator.followActiveEditor` | Show the node for the file you switch to, so the diagram follows your work (default: `true`). |
| `dependinator.autoRefresh.enabled` | Automatically refresh the diagram when workspace source files change (default: `true`). |
| `dependinator.autoRefresh.delaySeconds` | Delay after the last source file change before the diagram is refreshed (default: `3`). |

## Device sync and the web app

Device sync is optional. When you enable it (from the Dependinator app bar),
your models and customized views are kept updated on all devices that have
sync enabled. Without signing in, everything works locally.

The companion web app at [dependinator.com](https://dependinator.com) lets
you view your models outside VS Code, in a desktop or mobile browser,
wherever you are. Enable device sync with the same account in both the
extension and the web app, and the models you create in VS Code are there
too. Editing in the web app works as well, though viewing is its primary
use case; parsing code and creating models is what the extension is for.

## Feedback and issues

Found a bug or have a feature request? Please
[open an issue](https://github.com/michael-reichenauer/Dependinator/issues/new/choose).
For a problem, include the Dependinator version and anything in the
"Dependinator" channel of the Output panel. Questions and ideas are welcome in
[Discussions](https://github.com/michael-reichenauer/Dependinator/discussions).

## Links

- Web app (view your models anywhere): https://dependinator.com
- User guide: https://dependinator.com/help
- Repository: https://github.com/michael-reichenauer/Dependinator
- Discussions: https://github.com/michael-reichenauer/Dependinator/discussions
- Contributing / building the extension: see [DEVELOPMENT.md](DEVELOPMENT.md)

## License

MIT, see [LICENSE](LICENSE).
