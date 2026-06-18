---
name: ui-check
description: Verify Dependinator UI changes in a real browser. Use after changing UI code (Dependinator.UI, Dependinator.Web, Dependinator.Wasm) to visually/behaviorally check the app, and to run the Playwright e2e test suite.
allowed-tools: Bash(playwright-cli:*) Bash(./e2e:*) Bash(curl:*)
---

# Checking Dependinator UI changes in a browser

## 1. Make sure the app is running

Check first — the user often already has `./watch` running in a terminal:

```bash
curl -sf -o /dev/null http://localhost:5000 && echo running || echo "not running"
```

Start options (all serve the same Blazor Server app on http://localhost:5000):

- `./watch` — UI only, hot reload. Sufficient for almost all UI checks.
- `./watch-sync` — additionally starts Azurite + Azure Functions API (port 7071)
  for cloud-sync features. Needs `func` and `azurite` installed.
- `./e2e` starts/stops its own app in test mode; it errors if something is already
  running on 5000 (stop `./watch` first), so tests run against the known demo model.

If you start the app yourself, run it in the background and stop it when done.
The Wasm host (`./run`, also port 5000 — never run both at once) is rarely
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
  runs without the cloud-sync API (plain ./watch) — it is not a bug.

## 3. Run the e2e test suite

After UI-affecting changes, run:

```bash
./e2e                # Playwright smoke tests on chromium (default)
./e2e -b firefox     # specific browser: chromium | firefox | webkit
./e2e -a             # all three browsers (use before releases)
./e2e -s             # also start Azurite + Functions (7071) for sync tests
```

Tests live in `Dependinator.E2E.Tests/` (xUnit + Microsoft.Playwright, see its
README.md). They are skipped in plain `dotnet test` runs unless `E2E=1` is set,
which `./e2e` does. New UI features should get a test there; use `[E2EFact]`
and extend `E2ETestBase`. Cloud-sync tests use `[SyncFact]` and only run under
`./e2e -s` (which brings up Azurite + the Functions API, like ./watch-sync).
