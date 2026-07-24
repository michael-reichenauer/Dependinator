# Scripts

Development and maintenance scripts for Dependinator. All scripts can be run
from anywhere (they `cd` to the repo root themselves), e.g. `./scripts/watch`.

## Build & run

| Script | Description |
| --- | --- |
| `build` | Build the Blazor Server host (`Dependinator.Web`). |
| `watch` | Live dev: Azurite + Azure Functions host in the background, then `dotnet watch` on `Dependinator.Web` (port 5000). Requires `func` and `azurite`. |
| `run` | Publish the Wasm host in Release and serve it via the SWA CLI with Azurite + Functions — closest local approximation of the deployed Azure Static Web Apps setup. Requires `func`, `azurite`, and `swa`. |
| `log` | View the app log (`~/Dependinator.log`) in `lnav`. |

## Test

| Script | Description |
| --- | --- |
| `test` | Full suite: unit tests (`dotnet test Dependinator.sln`), then the e2e tests. Arguments are forwarded to `e2e` (e.g. `./scripts/test -s -t`). |
| `e2e` | Playwright UI tests. Starts the app itself in test mode (embedded demo model); `-b <browser>`, `-a` (all browsers), `-s` (cloud-sync stack), `-t` (traces). See `tests/Dependinator.E2E.Tests/README.md`. |
| `trace` | Serve a Playwright trace from `./scripts/e2e -t` in the Trace Viewer on port 9322 (`./scripts/trace`, `./scripts/trace 6`, or a path). |

## VS Code extension

| Script | Description |
| --- | --- |
| `build-ext` | npm install + patch-version bump + package the extension into a `.vsix`. |
| `install-ext` | `build-ext`, then install the `.vsix` into the local VS Code. |

## Assets & maintenance

| Script | Description |
| --- | --- |
| `gen-demo` | Regenerate the embedded demo model (`src/Dependinator.Wasm/wwwroot/demo.model`) by parsing `Dependinator.sln`. |
| `record-demo` | Re-record the VS Code extension demo gif (`src/DependinatorVsCode/resources/demo.gif`); scenario in `record-demo.mjs`. Needs `ffmpeg` + global `@playwright/cli`. |
| `import-icons` | Re-import curated Azure/AWS/Google icons into `src/Dependinator.UI/Diagrams/Icons/Library/`; curated lists in `cloud-icons/*.manifest`. |
| `updatepackages` | List outdated/vulnerable NuGet + npm packages; `-u` upgrades non-major, `-m` also major. |
| `installdevtools` | One-time devcontainer setup (timezone, apt tools, wasm-tools workload, claude, gmd). |

## Helper files (not run directly)

- `_stack.sh` — shared Azurite + Azure Functions helpers, sourced by `run`, `watch`, and `e2e`.
- `import-cloud-icons.mjs` — implementation behind `import-icons`.
- `record-demo.mjs` — the scripted demo scenario used by `record-demo`.
- `cloud-icons/` — curated icon manifests for `import-icons`.
