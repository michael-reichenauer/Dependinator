# Dependinator

Dependinator visualizes code structure and dependencies as an interactive,
navigable map, making complex architectures easier to understand, analyze, and
refactor — without leaving VS Code.

## Features

- **Interactive dependency map** — explore your codebase as a zoomable,
  navigable diagram instead of scrolling through files.
- **Drill down and back up** — open nodes to see their internals and follow
  dependencies between components.
- **Runs inside VS Code** — opens in an editor tab; no separate app to launch.
- **Optional cloud sync** — sign in to sync your views across devices.

## Getting started

1. Install the extension.
2. Open the command palette (`Ctrl+Shift+P` / `Cmd+Shift+P`) and run
   **Dependinator: Open**, or click the Dependinator icon in the editor title
   bar.
3. The dependency map opens in a new tab. Click nodes to drill in, and drag /
   zoom to navigate.

## Commands

| Command | Description |
| --- | --- |
| `Dependinator: Open` | Open the Dependinator dependency map. |
| `Dependinator: Install in Dev Container` | Jump to the Extensions view to install the extension inside a dev container. |

## Settings

- `dependinator.cloudSync.baseUrl` — the API endpoint used for cloud sync.
  Defaults to the production service; other entries are for local/development
  use. Sign-in is handled separately by the extension.

## Notes
- The installed extension bundles a self-contained language server, so a local
  `dotnet` install is not required.

## Links

- Repository: https://github.com/michael-reichenauer/Dependinator
- Contributing / building the extension: see [DEVELOPMENT.md](DEVELOPMENT.md)

## License

MIT — see [LICENSE](LICENSE).
