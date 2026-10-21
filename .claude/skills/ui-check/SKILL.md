---
name: ui-check
description: Verify Dependinator UI changes in a real browser. Use after changing UI code (Dependinator.UI, Dependinator.Web, Dependinator.Wasm) to visually/behaviorally check the app, and to run the Playwright e2e test suite.
allowed-tools: Bash(playwright-cli:*) Bash(./scripts/e2e:*) Bash(curl:*)
---

# Checking Dependinator UI changes in a browser

## 1. Make sure the app is running

Check first — the user often already has `./scripts/watch` running in a terminal:

```bash
curl -sf -o /dev/null http://localhost:5000 && echo running || echo "not running"
```

Start options (all serve the same Blazor Server app on http://localhost:5000):

- `./scripts/watch` — Blazor Server with hot reload, plus Azurite + Azure Functions API
  (port 7071) for cloud-sync features. Needs `func` and `azurite` installed.
- `./scripts/e2e` starts/stops its own app in test mode; it errors if something is already
  running on 5000 (stop `./scripts/watch` first), so tests run against the known demo model.

If you start the app yourself, run it in the background and stop it when done.
The Wasm host (`./scripts/run`, also port 5000 — never run both at once) is rarely
needed — only for Wasm-specific behavior; it requires a slow Release publish.

## 2. Interactive browser checks (playwright-cli)

Use the `playwright-cli` skill (.claude/skills/playwright-cli) for the full
command reference. Typical flow:

```bash
playwright-cli open http://localhost:5000   # prints page snapshot with element refs
playwright-cli snapshot                     # re-read current page state
playwright-cli click <ref>                  # interact via refs from the snapshot
playwright-cli screenshot                   # written to disk, view the file
playwright-cli close
```

Notes:
- Headless only — this devcontainer has no display.
- The diagram is one big SVG (`#svgcanvas`); its content is mostly invisible to
  accessibility snapshots, so use `playwright-cli screenshot` to judge diagram
  rendering, and snapshots for toolbar/menus/dialogs (MudBlazor components).
- Search dialog opens with `playwright-cli press Control+f`.
- Cloud-sync flows require Clerk sign-in; do not try to automate sign-in.
  Anything behind login is currently out of scope for automated checks.
- A "Connection refused (127.0.0.1:7071)" snackbar is expected when the app
  runs without the cloud-sync API (e.g. `func`/`azurite` not installed) — it is not a bug.

### Driving the diagram (learned the hard way)

- **Start the app yourself:**
  `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5000 dotnet run --project src/Dependinator.Web --no-launch-profile`.
  Without Development the static web assets return 404; without `ASPNETCORE_URLS` the app
  listens on 8080.
- **Browser:** use `open --browser=chromium`; the default `chrome` channel is not installed.
- **Persistence checks:** every `open` starts a fresh context with empty IndexedDB, so checking
  that something persisted by re-opening looks like data loss. Use `reload` or `goto` in the same
  session, and confirm in `~/Dependinator.log` ("Read model …" vs "Parsing …").
- **Clicking:**
  - Select nodes with one `click <ref>`. Separate `mousedown`/`mouseup` commands take longer than
    the app's 500 ms click timeout.
  - `#svgcanvas text` locators time out on click, so click canvas points with
    `page.mouse.click(x, y)` taken from a screenshot.
  - Double-clicking the canvas creates a node, and hovering a node pops up its toolbar: move the
    mouse to empty canvas before a screenshot.
- **Navigating:**
  - Ctrl+F, type the name, click a result: the app animates to the node in ~3-4 s.
  - The first result for a prefix is often a deeper node, so check the breadcrumb.
  - To reach a container level, click a breadcrumb entry (e.g.
    `page.getByText('Dependinator.Core (dll)', { exact: true })`). It animates to that node with
    its children laid out. "Zoom to node" on the root only recentres it.
- **Zooming:** synthetic `WheelEvent`s barely register; use `page.mouse.wheel` in a `run-code`
  script with ~150 ms waits, after `page.mouse.move` (zoom centres on the pointer). Negative
  (zoom-in) deltas were not delivered in some sessions, so verify zoom by a node's
  `getBoundingClientRect().width`, not by the `#svgcanvas` viewBox.
- **run-code scripts** are `async page => { ... }` (a top-level `await page` is a SyntaxError).
  Pass them with `--filename=script.js`.
- **Menus:** the main menu is `appbar-menu` (the hamburger at the top left). View is a hover
  submenu (`menu-view`): open the hamburger, then hover. Clicking `menu-view` directly times out.
- **Theme:** `page.emulateMedia({ colorScheme: 'dark' })` switches the app (default theme System)
  without the menu.
- **README images:** `docs/images/hero.png` and `bicep.png` are 1600x1000 captures
  (`playwright-cli resize 1600 1000`):
  - hero: wheel-zoom into the solution from the start view until Dependinator.UI opens as a
    container;
  - bicep: Models menu › webshop.bicep, search "networking", then zoom into the module to one step
    before vnet opens, so all resources stay icons.

## 3. Run the e2e test suite

After UI-affecting changes, run:

```bash
./scripts/e2e                # Playwright smoke tests on chromium (default)
./scripts/e2e -b firefox     # specific browser: chromium | firefox | webkit
./scripts/e2e -a             # all three browsers (use before releases)
./scripts/e2e -s             # also start Azurite + Functions (7071) for sync tests
```

Tests live in `tests/Dependinator.E2E.Tests/` (xUnit + Microsoft.Playwright, see its
README.md). They are skipped in plain `dotnet test` runs unless `E2E=1` is set,
which `./scripts/e2e` does. New UI features should get a test there; use `[E2EFact]`
and extend `E2ETestBase`. Cloud-sync tests use `[SyncFact]` and only run under
`./scripts/e2e -s` (which brings up Azurite + the Functions API, like ./scripts/watch).
