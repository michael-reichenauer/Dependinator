# Dependinator

Dependinator is a tool for visualizing and exploring software dependencies. This repo contains the shared UI and core logic plus multiple hosts (Blazor Server, Blazor WebAssembly) and an Azure Functions API used for cloud sync.

## Solution structure

`Dependinator.sln` (targeting `net10.0`, SDK pinned in `global.json`) stays at the repo root; production project folders live under `src/` and test projects under `tests/`. Dependency direction is `Hosts → UI → Core → Shared` (with `Roslyn → Core`), enforced by `tests/Dependinator.Architecture.Tests/`.

**Hosts:**
- `src/Dependinator.Web/`: Blazor Server host for local development.
- `src/Dependinator.Wasm/`: Blazor WebAssembly host (Azure Static Web Apps target) and Web UI for the VS Code extension.
- `src/Dependinator.Lsp/`: LSP server executable.
- `src/Api/`: Azure Functions API for cloud sync.

**Libraries:**
- `src/Dependinator.UI/`: Shared UI (`App/`, `Diagrams/`, `Modeling/`).
- `src/Dependinator.Core/`: Core parsing, domain logic, models, and utilities.
- `src/Dependinator.Roslyn/`: Roslyn-based parsing.
- `src/Shared/`: Shared DTOs/models between client and API.

**Tests:**
- `tests/Dependinator.UI.Tests/`, `tests/Dependinator.Core.Tests/`, `tests/Dependinator.Roslyn.Tests/`, `tests/Api.Tests/`: xUnit unit tests.
- `tests/Dependinator.Architecture.Tests/`: NetArchTest layering guards.
- `tests/Dependinator.E2E.Tests/`: Playwright UI tests (run via `./e2e`).

**VS Code extension** (not part of `Dependinator.sln`):
- `src/DependinatorVsCode/`: TypeScript extension packaging the web UI + language server.

## Prerequisites

- **.NET SDK 10.0.101** (pinned in `global.json`).
- **Node.js + npm** — for the VS Code extension and SWA CLI.
- **Azurite** (`npm i -g azurite`) — local Azure Storage emulator, needed by `./watch` and `./run`.
- **Azure Functions Core Tools** (`func`) — needed by `./watch` and `./run`.
- **SWA CLI** (`npm i -g @azure/static-web-apps-cli`) — needed by `./run`.
- **Playwright browsers** — for `./e2e`.

The devcontainer (`.devcontainer/`) provisions the .NET SDK, Node, `func`, and Playwright browsers out of the box. **Azurite** and the **SWA CLI** are not preinstalled — add them with the `npm i -g` commands above if you need `./watch` or `./run`.

## Quick start
- Build: `./build`
- Run server (live dev, with cloud sync): `./watch`
- Run WASM + API + Azurite locally: `./run`
- Unit tests: `dotnet test Dependinator.sln`
- UI/e2e tests: `./e2e` (chromium; `-a` for all browsers)
- All tests (unit + e2e): `./test`

## Cloud sync

Cloud sync uses [Clerk](https://clerk.com) for authentication (magic links / email OTP). The API validates Clerk-issued JWTs via JWKS.

### Azure Static Web Apps deployment

- See `swa-cli.config.json` for the `dependinator-test` configuration.
- Required Static Web App application settings:
  - `CloudSync__ClerkIssuer`
  - `CloudSync__ContainerName`
  - `CloudSync__MaxUserQuotaBytes`
  - `CloudSync__StorageConnectionString`

### VS Code extension

- The `dependinator.cloudSync.baseUrl` setting controls which API endpoint the extension uses.
- The extension serves a local Clerk sign-in page and stores the session JWT in VS Code secrets.
